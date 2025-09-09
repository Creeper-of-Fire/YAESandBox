import random
from typing import List

import numpy as np

from core_types import GenerationContext, GameObject
from io_and_vis import export_context_to_json, Visualizer
from modifiers import (
    apply_perlin_noise,
    apply_visual_jitter,
    bind_floating_objects_to_grid, reserve_grid_margin, apply_influence_to_layer, combine_layers, create_uniform_layer,
    apply_influence_from_points, adjust_layer_contrast, create_layer_from_coordinates, ensure_layer_exists, safe_normalize
)
from placement_strategies import (
    place_floating_objects_from_layer, place_one_grid_object_from_layer, place_grid_objects_from_layer
)
from prototype import Settings


# 这是一个更高级的“管道”函数，它组合了多个修改器和放置器
def table_generation_pipeline(ctx: GenerationContext, settings: Settings) -> GenerationContext:
    """
    使用原子化操作和迭代排斥来生成桌子。
    """
    print("\n--- [管道] 开始生成桌子 ---")

    # --- 阶段 1: 创建初始的全局适宜度地图 ---
    # 1.1: 创建一个全1的基础层
    ctx = create_uniform_layer(ctx, 'table_base_suitability', 1.0)

    # 1.2: 创建中心吸引力层
    center_point = (ctx.grid_width / 2, ctx.grid_height / 2)
    ctx = apply_influence_from_points(
        ctx, 'center_attraction', [center_point],
        sigma=max(ctx.grid_width, ctx.grid_height)  # sigma 较大以产生缓和的梯度
    )

    # 1.3: 将基础层和吸引力层混合
    ctx = combine_layers(
        ctx, 'table_suitability_with_attraction',
        'table_base_suitability', 'center_attraction',
        mode='weighted_sum',
        weight_a=1.0,  # 基础权重
        weight_b=settings.CENTER_ATTRACTION_STRENGTH  # 吸引力权重
    )

    # 1.4: 应用柏林噪声增加随机性
    # 我们把噪声作为一个独立的层，再混合进去，这样更清晰
    ctx = apply_perlin_noise(
        ctx,
        'table_noise',
        settings.NOISE_SCALE,
        1.0)
    ctx = combine_layers(
        ctx, 'initial_table_suitability',
        'table_suitability_with_attraction', 'table_noise',
        mode='multiply'  # 使用乘法混合噪声
    )

    # --- 阶段 2: 迭代放置桌子 ---
    placed_tables = []
    for i in range(settings.NUM_TABLES):
        print(f"放置桌子 {i + 1}/{settings.NUM_TABLES}...")

        # 1. 准备本次迭代的概率图
        current_suitability_map_name = 'current_table_suitability'
        # 总是从最原始的适宜度图开始，因为我们会应用所有的桌子
        ctx.layers[current_suitability_map_name] = ctx.layers['initial_table_suitability'].copy()

        # 2. 如果已经有桌子了，就直接在当前概率图上应用排斥力
        if placed_tables:
            ctx = apply_influence_to_layer(
                ctx,
                target_layer_name=current_suitability_map_name,  # 直接修改当前适宜度图
                source_objects=placed_tables,
                sigma=settings.TABLE_REPULSION_SIGMA,
                mode='multiply_inverse'  # 使用新的、清晰的排斥模式
            )

        # 2.3: 放置一个桌子
        ctx, new_table = place_one_grid_object_from_layer(
            ctx, 'current_table_suitability',
            "TABLE", settings.TABLE_SIZE,
            blocked_by={"TABLE", "WALL_RESERVED"}
        )

        if new_table:
            placed_tables.append(new_table)
        else:
            print("空间不足，无法放置更多桌子。")
            break

    print(f"--- 桌子生成完毕，共放置 {len(placed_tables)} 个 ---")
    return ctx


def chair_placement_pipeline(ctx: GenerationContext, settings: Settings) -> GenerationContext:
    """
    数据驱动的椅子放置管道。
    """
    print("\n--- [管道] 开始放置椅子 ---")

    # 1. 找出所有桌子
    tables = [obj for obj in ctx.objects if obj.obj_type == "TABLE"]
    if not tables:
        print("没有桌子，跳过椅子放置。")
        return ctx

    # 2. 计算所有可能的椅子放置点
    all_chair_spots = []
    for table in tables:
        if table.grid_pos is None or table.grid_size is None: continue

        tx, ty = table.grid_pos
        tw, th = table.grid_size

        candidate_spots = []
        # 沿长边生成候选点
        if tw >= th:  # 长边是水平的
            for i in range(tw): candidate_spots.append((tx + i, ty - 1))  # 上方
            for i in range(tw): candidate_spots.append((tx + i, ty + th))  # 下方
        else:  # 长边是垂直的
            for i in range(th): candidate_spots.append((tx - 1, ty + i))  # 左侧
            for i in range(th): candidate_spots.append((tx + tw, ty + i))  # 右侧

        # 过滤掉已经被占用的点
        for spot_x, spot_y in candidate_spots:
            if 0 <= spot_x < ctx.grid_width and 0 <= spot_y < ctx.grid_height:
                all_chair_spots.append((spot_x, spot_y))

    # 3. 基于这些点创建一张椅子适宜度地图
    ctx = create_layer_from_coordinates(
        ctx,
        'chair_suitability_map',
        all_chair_spots
    )

    # 4. 使用通用的放置函数，在适宜度地图上放置椅子
    # 我们需要计算总共要放多少椅子
    num_chairs_total = sum(random.randint(*settings.CHAIRS_PER_TABLE_RANGE) for _ in tables)

    # 我们使用 place_grid_objects_from_layer (之前叫 place_by_weighted_sampling)
    ctx, _ = place_grid_objects_from_layer(
        ctx,
        layer_source='chair_suitability_map',
        num_to_place=num_chairs_total,
        obj_type="CHAIR",
        grid_size=settings.CHAIR_SIZE,
        blocked_by={"TABLE", "CHAIR", "WALL_RESERVED"}
    )

    print("--- 椅子放置完毕 ---")
    return ctx


def grime_generation_pipeline(ctx: GenerationContext, settings: Settings) -> GenerationContext:
    """
    使用原子化操作管道生成脏污的完整流程。
    """
    print("\n--- [管道] 开始生成脏污 ---")

    furniture_objects = [obj for obj in ctx.objects if obj.obj_type in ["TABLE", "CHAIR"]]

    # 步骤 1: 创建物体遮蔽层。桌椅等家具为其周围赋予“脏污潜力”。
    ctx = apply_influence_to_layer(
        ctx,
        target_layer_name='furniture_occlusion',
        source_objects=furniture_objects,
        sigma=2.0  # 可移至 Settings
    )

    # 步骤 2: 创建一个独立的柏林噪声层
    ctx = apply_perlin_noise(
        ctx,
        target_layer_name='grime_splatter_noise',
        scale=settings.GRIME_SPLATTER_NOISE_SCALE,
        strength=1.0  # strength 为1.0表示纯噪声
    )

    # 步骤 3: 将遮蔽层和噪声层混合，得到基础的脏污潜力图
    ctx = combine_layers(
        ctx,
        target_layer_name='grime_base_probability',
        layer_a_name='furniture_occlusion',
        layer_b_name='grime_splatter_noise',
        mode='weighted_sum',
        weight_a=0.6,  # 遮蔽影响占 60%
        weight_b=0.4  # 随机噪声占 40%
    )

    # 步骤 4: 使用基础概率图，放置大脏污核心
    ctx, large_grime_patches = place_floating_objects_from_layer(
        ctx,
        layer_source='grime_base_probability',
        num_to_place=settings.NUM_LARGE_GRIME_PATCHES,
        obj_type='GRIME_LARGE',
        blocked_by=set()
    )

    # 步骤 5: 基于大脏污核心创建“吸引”影响图
    ctx = apply_influence_to_layer(
        ctx,
        target_layer_name='grime_core_influence',
        source_objects=large_grime_patches,
        sigma=settings.GRIME_CORE_INFLUENCE_RADIUS
    )

    # 步骤 6: 再次组合，将核心吸引力加入基础概率，得到最终的小脏污概率图
    ctx = combine_layers(
        ctx,
        target_layer_name='grime_small_final_prob',
        layer_a_name='grime_base_probability',
        layer_b_name='grime_core_influence',
        mode='add'  # 使用加法，让两个影响都能独立生效
    )

    # 步骤 6.5: 锐化最终的概率图，让脏污更集中在“热点”区域
    ctx = adjust_layer_contrast(
        ctx,
        layer_name='grime_small_final_prob',
        exponent=2.0  # 值为2表示平方，可以尝试3或4以获得更强的聚集效果
    )

    # 步骤 7: 根据最终概率图，放置大量小脏污
    ctx, _ = place_floating_objects_from_layer(
        ctx,
        layer_source='grime_small_final_prob',
        num_to_place=settings.GRIME_SPLATTER_COUNT,
        obj_type='GRIME_SMALL',
        blocked_by=set()
    )

    print("--- [管道] 脏污生成完毕 ---")
    return ctx


def lighting_pipeline(ctx: GenerationContext, settings: Settings) -> GenerationContext:
    """
    一个集成的光照生成管道，使用迭代式放置，逐个安放窗户和火把，
    以实现更智能、更均匀的布局。
    """
    print("\n--- [集成管道] 开始生成光照系统 (迭代式) ---")
    w, h = ctx.grid_width, ctx.grid_height

    # --- 阶段 1: 确保全局光照图存在 ---
    ctx = ensure_layer_exists(ctx, 'global_light_map', fill_value=0.0)

    # =================================================
    # --- 阶段 2: 迭代式放置窗户 ---
    # =================================================
    print("--- [光照] 阶段 2: 迭代式放置窗户 ---")

    # 2.1: 预先计算不变量：边缘位置图和对称吸引力图
    edge_coords = []
    margin = 0
    for x in range(margin, w - margin):
        edge_coords.append((x, margin)); edge_coords.append((x, h - 1 - margin))
    for y in range(margin + 1, h - 1 - margin):
        edge_coords.append((margin, y)); edge_coords.append((w - 1 - margin, y))
    ctx = create_layer_from_coordinates(ctx, 'window_edge_suitability', edge_coords)

    symmetry_points = [(margin, h/2), (w-1-margin, h/2), (w/2, margin), (w/2, h-1-margin)]
    ctx = apply_influence_from_points(ctx, 'symmetry_attraction', symmetry_points, sigma=w/4)
    ctx = combine_layers(ctx, 'window_base_prob', 'window_edge_suitability', 'symmetry_attraction', mode='multiply')

    placed_windows = []
    for i in range(settings.NUM_WINDOWS):
        # 1. 从上下文中获取当前状态数据
        current_light_map = ctx.layers['global_light_map']
        window_base_prob = ctx.layers['window_base_prob']

        # 2. 在局部变量中执行纯计算，不修改上下文
        #    这是“显式传递”思想的核心
        normalized_light = safe_normalize(current_light_map)
        darkness_map = 1.0 - normalized_light
        final_window_placement_prob = window_base_prob * darkness_map

        # 2.4: 放置 **一个** 窗户
        ctx, new_window = place_one_grid_object_from_layer(
            ctx, final_window_placement_prob, "WINDOW", settings.WINDOW_SIZE,
            blocked_by={"TABLE", "CHAIR"}
        )

        # 2.5: 如果成功放置，立刻更新全局光照图；否则，跳出循环
        if new_window:
            print(f"  - 放置窗户 {i+1}/{settings.NUM_WINDOWS}...")
            placed_windows.append(new_window)
            ctx = apply_influence_to_layer(
                ctx, 'global_light_map', [new_window], # 注意：只传入新窗户
                sigma=8.0, strength_multiplier=lambda o: 0.4, mode='add'
            )
        else:
            print("  - 空间不足，无法放置更多窗户。")
            break
    print(f"--- 共放置了 {len(placed_windows)} 个窗户 ---")

    # =================================================
    # --- 阶段 3: 迭代式放置火把 ---
    # =================================================
    print("--- [光照] 阶段 3: 迭代式放置火把 ---")

    # 3.1: 预先计算火把的基础吸引力图 (墙壁)
    wall_placeholders = []
    for x in range(w):
        for y in range(h):
            if any(o.obj_type == "WALL_RESERVED" for o in ctx.occupancy_grid[x, y]):
                wall_placeholders.append(GameObject("WALL_RESERVED", np.array([x, y], dtype=float)))
    ctx = apply_influence_to_layer(ctx, 'wall_attraction_map', wall_placeholders, sigma=6.0,strength_multiplier=lambda o: 0.3)

    placed_torches = []
    for i in range(settings.NUM_TORCHES):
        # 1. 获取状态
        current_light_map = ctx.layers['global_light_map']
        wall_attraction_map = ctx.layers['wall_attraction_map']

        # 2. 纯计算
        normalized_light = safe_normalize(current_light_map)
        darkness_map_after_windows = 1.0 - normalized_light
        final_torch_placement_prob_raw = wall_attraction_map * darkness_map_after_windows
        # adjust_layer_contrast 也可以被重构为一个纯函数
        # 但为了最小化改动，我们暂时可以继续使用它，因为它至少会覆写目标层，而不是创建新层
        ctx.layers['temp_torch_prob'] = final_torch_placement_prob_raw
        ctx = adjust_layer_contrast(ctx, 'temp_torch_prob', exponent=2.0)
        final_torch_placement_prob = ctx.layers['temp_torch_prob']

        # 3. 传递临时数据
        ctx, new_torch = place_one_grid_object_from_layer(
            ctx, final_torch_placement_prob, "TORCH", settings.TORCH_SIZE,
            blocked_by={"TABLE", "CHAIR", "WINDOW"}
        )

        # 3.5: 如果成功放置，立刻更新全局光照图；否则，跳出循环
        if new_torch:
            print(f"  - 放置火把 {i+1}/{settings.NUM_TORCHES}...")
            placed_torches.append(new_torch)
            ctx = apply_influence_to_layer(
                ctx, 'global_light_map', [new_torch], # 注意：只传入新火把
                sigma=4.0, strength_multiplier=lambda o: 1.2, mode='add'
            )
        else:
            print("  - 空间不足，无法放置更多火把。")
            break

    print(f"--- 共放置了 {len(placed_torches)} 个火把 ---")
    print("--- [集成管道] 光照系统生成完毕 ---")
    return ctx


def character_placement_pipeline(ctx: GenerationContext, settings: Settings) -> GenerationContext:
    """
    负责生成所有角色的放置管道。
    """
    print("\n--- [管道] 开始放置角色 ---")

    social_sources = [o for o in ctx.objects if o.obj_type in ['TABLE', 'CHAIR']]
    grime_sources = [o for o in ctx.objects if 'GRIME' in o.obj_type]

    # --- 准备基础偏好图 (只需要创建一次) ---
    # 社交吸引图
    ctx = apply_influence_to_layer(
        ctx, 'social_comfort_map',
        source_objects=social_sources,
        sigma=settings.SOCIAL_ATTRACTION_SIGMA
    )
    # 脏污影响图
    ctx = apply_influence_to_layer(
        ctx, 'grime_influence_map',
        source_objects=grime_sources,
        sigma=2.0,
        strength_multiplier=lambda o: settings.ELF_LARGE_GRIME_MULTIPLIER if 'LARGE' in o.obj_type else 1.0
    )

    # --- 逐个种族进行放置 ---
    # 定义角色配置
    character_configs = [
        {"type": "ELF", "count": settings.NUM_ELVES, "social": 1.0, "grime": settings.ELF_GRIME_REPULSION},
        {"type": "DWARF", "count": settings.NUM_DWARVES, "social": 1.0, "grime": 0.0},
        {"type": "MUSHROOM_PERSON", "count": settings.NUM_MUSHROOM_PEOPLE, "social": 1.0, "grime": settings.MUSHROOM_GRIME_ATTRACTION}
    ]

    for config in character_configs:
        char_type = config["type"]
        preference_map_name = f"{char_type.lower()}_preference_map"

        # 1. 为该种族混合专属的偏好图
        ctx = combine_layers(
            ctx,
            target_layer_name=preference_map_name,
            layer_a_name='social_comfort_map',
            layer_b_name='grime_influence_map',
            mode='weighted_sum',
            weight_a=config["social"],
            weight_b=config["grime"]
        )
        # 裁剪掉负值，因为概率不能为负
        ctx.layers[preference_map_name] = np.clip(ctx.layers[preference_map_name], 0, None)

        # 2. (可选) 锐化偏好，让他们的选择更“坚定”
        ctx = adjust_layer_contrast(ctx, preference_map_name, exponent=1.5)

        # 3. 使用通用的浮动对象放置函数
        ctx, _ = place_floating_objects_from_layer(
            ctx,
            layer_source=preference_map_name,
            num_to_place=config["count"],
            obj_type=char_type,
            blocked_by={"TABLE", "WALL_RESERVED"}
        )

    return ctx


def create_list_filled_grid(width: int, height: int) -> np.ndarray:
    grid = np.empty((width, height), dtype=object)
    for i in range(width):
        for j in range(height):
            empty_list: List[GameObject] = []
            grid[i, j] = empty_list
    return grid


# 主执行流程
if __name__ == "__main__":
    # 假设 Settings 类仍然存在，用于集中管理参数
    from prototype import Settings  # 沿用你之前的Settings类

    settings = Settings()

    # 1. 初始化生成上下文
    init_grid = create_list_filled_grid(settings.GRID_WIDTH, settings.GRID_HEIGHT)
    context = GenerationContext(
        grid_width=settings.GRID_WIDTH,
        grid_height=settings.GRID_HEIGHT,
        layers={},
        fields={},
        objects=[],
        occupancy_grid=init_grid,
    )
    context = reserve_grid_margin(context, margin_width=1)

    # 2. 运行桌子生成管道
    context = table_generation_pipeline(context, settings)
    context = chair_placement_pipeline(context, settings)

    # === 阶段三: 混沌感 ===
    context = apply_visual_jitter(context, settings.POSITION_JITTER, settings.ANGLE_JITTER_DEGREES)

    # 添加窗户和火把
    context = lighting_pipeline(context, settings)

    # === 阶段四: 脏污 ===
    print("\n--- 构建脏污概率图 ---")
    context = grime_generation_pipeline(context, settings)

    # === 阶段五: 角色 ===
    print("\n--- 构建角色偏好图 ---")
    context = character_placement_pipeline(context, settings)

    print("\n--- 所有阶段执行完毕 ---")
    print(f"最终生成对象总数: {len(context.objects)}")

    # === 阶段六: 后处理 - 网格绑定 ===
    context = bind_floating_objects_to_grid(context)

    # 将最终的光照图从临时层提升为永久场
    if 'global_light_map' in context.layers:
        from modifiers import promote_layer_to_field

        context = promote_layer_to_field(context, 'global_light_map', 'light_level')

    # 导出到 JSON
    export_context_to_json(context, settings, "layout.json")

    # 可视化结果
    visualizer = Visualizer(settings)
    visualizer.plot_layout_and_layers(context)
