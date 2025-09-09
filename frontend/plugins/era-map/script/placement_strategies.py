from typing import Tuple
import numpy as np
import random

from scipy.ndimage import gaussian_filter

from core_types import GenerationContext, GameObject
from modifiers import safe_normalize

def place_by_weighted_sampling(
        ctx: GenerationContext,
        layer_name: str,
        num_to_place: int,
        obj_type: str,
        grid_size: Tuple[int, int],
        max_attempts_multiplier: int = 100
) -> GenerationContext:
    """
    使用真·加权采样在网格上放置物体，并进行碰撞检测。
    """
    if layer_name not in ctx.layers:
        print(f"警告: 概率层 '{layer_name}' 不存在。无法放置 '{obj_type}'。")
        return ctx

    prob_map = ctx.layers[layer_name]
    w, h = grid_size

    # 预先计算所有不可放置的位置 (因为碰撞或边界)
    valid_mask = np.ones_like(prob_map, dtype=bool)
    for x in range(ctx.grid_width):
        for y in range(ctx.grid_height):
            if np.any(ctx.occupancy_grid[x:x+w, y:y+h]) or (x+w > ctx.grid_width) or (y+h > ctx.grid_height):
                valid_mask[x, y] = False

    # 将无效位置的概率设为0
    masked_probs = prob_map * valid_mask

    placed_count = 0
    for _ in range(num_to_place * max_attempts_multiplier):
        if placed_count >= num_to_place: break

        flat_map = masked_probs.flatten()
        map_sum = np.sum(flat_map)

        if map_sum < 1e-9:
            print(f"警告: 没有有效的放置位置了。只放置了 {placed_count}/{num_to_place} 个 {obj_type}。")
            break

        # 真正的加权采样
        normalized_probs = flat_map / map_sum
        chosen_index = np.random.choice(len(normalized_probs), p=normalized_probs)
        x, y = np.unravel_index(chosen_index, prob_map.shape)

        # 创建并添加对象
        grid_pos = np.array([x, y])
        new_obj = GameObject(
            obj_type=obj_type,
            visual_pos=grid_pos.astype(float),
            grid_pos=grid_pos,
            grid_size=np.array(grid_size)
        )
        ctx.objects.append(new_obj)
        ctx.update_occupancy(new_obj)

        # 重要：更新 masked_probs 以防止在同一区域重复放置
        masked_probs[x:x+w, y:y+h] = 0

        placed_count += 1

    return ctx

def place_chairs_around_tables(
        ctx: GenerationContext,
        chairs_per_table_range: Tuple[int, int],
        chair_size: Tuple[int, int] = (1, 1)
) -> GenerationContext:
    """
    遍历所有桌子对象，并根据规则在它们周围放置椅子。
    """
    print("--- 开始放置椅子 ---")

    tables = [obj for obj in ctx.objects if obj.obj_type == "TABLE"]

    for table in tables:
        if table.grid_pos is None or table.grid_size is None:
            continue

        num_chairs_to_place = random.randint(*chairs_per_table_range)
        placed_chairs = 0

        tx, ty = table.grid_pos
        tw, th = table.grid_size

        # 动态生成所有长边的候选位置
        candidate_spots = []
        if tw >= th: # 长边是水平的
            # 上方
            for i in range(tw): candidate_spots.append((tx + i, ty - 1))
            # 下方
            for i in range(tw): candidate_spots.append((tx + i, ty + th))
        else: # 长边是垂直的
            # 左侧
            for i in range(th): candidate_spots.append((tx - 1, ty + i))
            # 右侧
            for i in range(th): candidate_spots.append((tx + tw, ty + i))

        random.shuffle(candidate_spots)

        for spot_x, spot_y in candidate_spots:
            if placed_chairs >= num_chairs_to_place:
                break

            # 检查边界和占用情况
            if (0 <= spot_x < ctx.grid_width and
                    0 <= spot_y < ctx.grid_height and
                    ctx.occupancy_grid[spot_x, spot_y] is None):

                grid_pos = np.array([spot_x, spot_y])
                new_chair = GameObject(
                    obj_type="CHAIR",
                    visual_pos=grid_pos.astype(float),
                    grid_pos=grid_pos,
                    grid_size=np.array(chair_size)
                )
                ctx.objects.append(new_chair)
                ctx.update_occupancy(new_chair)
                placed_chairs += 1

    print(f"--- 椅子放置完毕 ---")
    return ctx

def place_grime(
        ctx: GenerationContext,
        # --- 第1遍: 小脏污种子 ---
        splatter_prob_layer: str,
        splatter_count: int,
        # --- 第2遍: 大脏污核心 ---
        density_blur_sigma: float,
        num_large_patches: int,
        # --- 第3遍: 最终固化 ---
        final_survival_prob: float,
        core_influence_radius: float
) -> GenerationContext:
    """
    一个多阶段的脏污放置策略：播种 -> 聚类成核 -> 固化。
    """
    print("--- 开始生成脏污 ---")

    if splatter_prob_layer not in ctx.layers:
        print(f"警告: 脏污概率层 '{splatter_prob_layer}' 不存在。无法生成脏污。")
        return ctx

    w, h = ctx.grid_width, ctx.grid_height
    splatter_map = ctx.layers[splatter_prob_layer]

    # === 1. 生成潜在的飞溅物种子 ===
    potential_splatters = []
    for _ in range(splatter_count):
        x, y = random.uniform(0, w), random.uniform(0, h)
        prob = splatter_map[int(x), int(y)]
        if random.random() < prob:
            potential_splatters.append(np.array([x, y]))
    print(f"生成了 {len(potential_splatters)} 个潜在飞溅物。")

    # === 2. 寻找聚集并生成大脏污核心 ===
    density_map = np.zeros((w, h))
    for p in potential_splatters:
        ix, iy = int(p[0]), int(p[1])
        if 0 <= ix < w and 0 <= iy < h:
            density_map[ix, iy] += 1

    blurred_density = gaussian_filter(density_map, sigma=density_blur_sigma)
    large_grime_prob_map = safe_normalize(blurred_density)

    large_grime_patches = []
    if np.sum(large_grime_prob_map) > 1e-9:
        flat_map = large_grime_prob_map.flatten()
        normalized_probs = flat_map / np.sum(flat_map)

        chosen_indices = np.random.choice(len(normalized_probs), size=num_large_patches, p=normalized_probs)
        for index in chosen_indices:
            ix, iy = np.unravel_index(index, large_grime_prob_map.shape)
            # 添加一点随机偏移，使其不完全在格子中心
            pos_x = ix + random.uniform(-0.5, 0.5)
            pos_y = iy + random.uniform(-0.5, 0.5)
            patch = GameObject(
                obj_type="GRIME_LARGE",
                visual_pos=np.array([pos_x, pos_y])
            )
            large_grime_patches.append(patch)
            ctx.objects.append(patch)

    print(f"生成了 {len(large_grime_patches)} 个大脏污核心。")

    # === 3. 固化最终的小脏污 ===
    for p_pos in potential_splatters:
        survival_prob = final_survival_prob
        for core in large_grime_patches:
            dist = np.linalg.norm(p_pos - core.visual_pos)
            if dist < core_influence_radius:
                survival_prob += (1.0 - survival_prob) * (1.0 - dist / core_influence_radius)

        if random.random() < survival_prob:
            splatter = GameObject(
                obj_type="GRIME_SMALL",
                visual_pos=p_pos
            )
            ctx.objects.append(splatter)

    print(f"--- 脏污生成完毕 ---")
    return ctx

def place_characters_by_preference(
        ctx: GenerationContext,
        character_type: str,
        num_to_place: int,
        social_layer: str,
        social_weight: float,
        grime_layer: str,
        grime_weight: float # 正数表示吸引，负数表示排斥
) -> GenerationContext:
    """
    根据混合了社交和脏污偏好的舒适度地图来放置角色。
    """
    print(f"--- 开始放置角色: {character_type} ---")

    # 1. 创建该角色的专属舒适度地图
    social_map = ctx.layers.get(social_layer, np.zeros((ctx.grid_width, ctx.grid_height)))
    grime_map = ctx.layers.get(grime_layer, np.zeros((ctx.grid_width, ctx.grid_height)))

    comfort_map = (social_map * social_weight) + (grime_map * grime_weight)

    # 确保舒适度不为负，并归一化为概率
    comfort_map_clipped = np.clip(comfort_map, 0, None) # 裁剪掉负值
    prob_map = safe_normalize(comfort_map_clipped)

    # 将被家具占用的地方概率设为0
    prob_map[ctx.occupancy_grid != None] = 0

    # 2. 使用加权采样放置角色（非网格对齐）
    if np.sum(prob_map) < 1e-9:
        print(f"警告: {character_type} 没有有效的放置位置。")
        return ctx

    flat_map = prob_map.flatten()
    normalized_probs = flat_map / np.sum(flat_map)

    # 防止角色生成在完全相同的位置
    placed_coords = set()

    chosen_indices = np.random.choice(
        len(normalized_probs),
        size=num_to_place * 5, # 多选一些以防重复
        p=normalized_probs,
        replace=True
    )

    placed_count = 0
    for index in chosen_indices:
        if placed_count >= num_to_place:
            break

        ix, iy = np.unravel_index(index, prob_map.shape)
        if (ix, iy) in placed_coords:
            continue

        pos_x = ix + random.uniform(0, 1)
        pos_y = iy + random.uniform(0, 1)

        new_char = GameObject(
            obj_type=character_type,
            visual_pos=np.array([pos_x, pos_y])
        )
        ctx.objects.append(new_char)
        placed_coords.add((ix, iy))
        placed_count += 1

    print(f"成功放置了 {placed_count}/{num_to_place} 个 {character_type}。")
    return ctx