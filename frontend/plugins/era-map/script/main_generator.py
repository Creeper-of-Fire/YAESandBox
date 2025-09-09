import numpy as np

from core_types import GenerationContext
from io_and_vis import export_context_to_json, Visualizer
from modifiers import (
    create_initial_suitability, apply_central_attraction, apply_perlin_noise,
    apply_repulsion_from_objects, apply_visual_jitter,
    create_grime_base_map_from_occlusion, blend_layers,
    create_social_comfort_map, create_grime_influence_map,
    bind_floating_objects_to_grid,reserve_grid_margin
)
from placement_strategies import (
    place_by_weighted_sampling, place_chairs_around_tables,
    place_grime, place_characters_by_preference
)


# 这是一个更高级的“管道”函数，它组合了多个修改器和放置器
def generate_tables_with_repulsion(
        ctx: GenerationContext,
        num_tables: int,
        table_size: tuple[int, int],
        center_strength: float,
        noise_scale: float,
        noise_strength: float,
        repulsion_sigma: float,
        repulsion_strength: float
) -> GenerationContext:
    print("--- 开始生成桌子 ---")

    # 1. 创建并修改适宜度地图
    ctx.layers['table_suitability'] = create_initial_suitability(ctx.grid_width, ctx.grid_height)
    ctx = apply_central_attraction(ctx, 'table_suitability', center_strength)
    ctx = apply_perlin_noise(ctx, 'table_suitability', noise_scale, noise_strength)

    # 2. 逐个放置桌子，并在每一步后应用排斥
    newly_placed_tables = []
    initial_object_count = len(ctx.objects)

    for i in range(num_tables):
        print(f"放置桌子 {i + 1}/{num_tables}...")
        # 复制一份当前的适宜度图，以免被排斥永久修改
        temp_suitability_map = ctx.layers['table_suitability'].copy()

        # 创建一个临时上下文，用于单次放置
        temp_ctx = GenerationContext(
            grid_width=ctx.grid_width,
            grid_height=ctx.grid_height,
            layers={'temp_suitability': temp_suitability_map},
            objects=[],  # 放置器只会看到这个
            occupancy_grid=ctx.occupancy_grid.copy()  # 使用当前的占用情况
        )

        temp_ctx = apply_repulsion_from_objects(temp_ctx, 'temp_suitability', newly_placed_tables, repulsion_sigma, repulsion_strength)

        # 放置一个桌子
        temp_ctx = place_by_weighted_sampling(temp_ctx, 'temp_suitability', 1, "TABLE", table_size)

        if len(temp_ctx.objects) > 0:
            new_table = temp_ctx.objects[0]
            newly_placed_tables.append(new_table)
            ctx.objects.append(new_table)
            ctx.update_occupancy(new_table)
        else:
            print("空间不足，无法放置更多桌子。")
            break

    print(f"--- 桌子生成完毕，共放置 {len(newly_placed_tables)} 个 ---")
    return ctx


# 主执行流程
if __name__ == "__main__":
    # 假设 Settings 类仍然存在，用于集中管理参数
    from prototype import Settings  # 沿用你之前的Settings类

    settings = Settings()

    # 1. 初始化生成上下文
    init_grid = np.empty((settings.GRID_WIDTH, settings.GRID_HEIGHT), dtype=object)
    context = GenerationContext(
        grid_width=settings.GRID_WIDTH,
        grid_height=settings.GRID_HEIGHT,
        layers={},
        objects=[],
        occupancy_grid=init_grid
    )
    context = reserve_grid_margin(context, margin_width=1)

    # 2. 运行桌子生成管道
    context = generate_tables_with_repulsion(
        ctx=context,
        num_tables=settings.NUM_TABLES,
        table_size=settings.TABLE_SIZE,
        center_strength=settings.CENTER_ATTRACTION_STRENGTH,
        noise_scale=settings.NOISE_SCALE,
        noise_strength=settings.NOISE_STRENGTH,
        repulsion_sigma=settings.TABLE_REPULSION_SIGMA,
        repulsion_strength=settings.TABLE_REPULSION_STRENGTH
    )
    context = place_chairs_around_tables(context, settings.CHAIRS_PER_TABLE_RANGE)

    # === 阶段三: 混沌感 ===
    context = apply_visual_jitter(context, settings.POSITION_JITTER, settings.ANGLE_JITTER_DEGREES)

    # === 阶段四: 脏污 ===
    print("\n--- 构建脏污概率图 ---")
    context = create_grime_base_map_from_occlusion(
        context, 'grime_base',
        occlusion_strength=0.5,  # 这些都可以移到Settings
        occlusion_sigma=2.0
    )
    context.layers['grime_noise'] = np.zeros((settings.GRID_WIDTH, settings.GRID_HEIGHT))  # 创建一个空层
    context = apply_perlin_noise(
        context, 'grime_noise',
        settings.GRIME_SPLATTER_NOISE_SCALE,
        strength=1.0  # 噪声强度为1，因为我们要混合它
    )
    context = blend_layers(
        context,
        target_layer='grime_base',
        source_layer='grime_noise',
        weight_target=0.6,
        weight_source=0.4
    )
    context = place_grime(
        context,
        splatter_prob_layer='grime_base',
        splatter_count=settings.GRIME_SPLATTER_COUNT,
        density_blur_sigma=settings.GRIME_DENSITY_BLUR_SIGMA,
        num_large_patches=settings.NUM_LARGE_GRIME_PATCHES,
        final_survival_prob=settings.GRIME_FINAL_SURVIVAL_PROB,
        core_influence_radius=settings.GRIME_CORE_INFLUENCE_RADIUS
    )

    # === 阶段五: 角色 ===
    print("\n--- 构建角色偏好图 ---")
    context = create_social_comfort_map(context, 'social_comfort', settings.SOCIAL_ATTRACTION_SIGMA)
    context = create_grime_influence_map(context, 'grime_influence', settings.ELF_LARGE_GRIME_MULTIPLIER)

    # 放置精灵
    context = place_characters_by_preference(
        context, "ELF", settings.NUM_ELVES,
        social_layer='social_comfort', social_weight=settings.SOCIAL_ATTRACTION_STRENGTH,
        grime_layer='grime_influence', grime_weight=settings.ELF_GRIME_REPULSION
    )
    # 放置矮人
    context = place_characters_by_preference(
        context, "DWARF", settings.NUM_DWARVES,
        social_layer='social_comfort', social_weight=settings.SOCIAL_ATTRACTION_STRENGTH,
        grime_layer='grime_influence', grime_weight=0.0  # 矮人不在乎脏污
    )
    # 放置蘑菇人
    context = place_characters_by_preference(
        context, "MUSHROOM_PERSON", settings.NUM_MUSHROOM_PEOPLE,
        social_layer='social_comfort', social_weight=settings.SOCIAL_ATTRACTION_STRENGTH,
        grime_layer='grime_influence', grime_weight=settings.MUSHROOM_GRIME_ATTRACTION
    )

    print("\n--- 所有阶段执行完毕 ---")
    print(f"最终生成对象总数: {len(context.objects)}")

    # === 阶段六: 后处理 - 网格绑定 ===
    context = bind_floating_objects_to_grid(context)

    # 导出到 JSON
    export_context_to_json(context, settings, "layout.json")

    # 可视化结果
    visualizer = Visualizer(settings)
    visualizer.plot_layout_and_layers(context)
