import random
from typing import Tuple, Optional, Set, List, Union

import numpy as np

from core_types import GenerationContext, GameObject
from modifiers import safe_normalize

def _resolve_prob_map(ctx: GenerationContext, layer_source: Union[str, np.ndarray]) -> Optional[np.ndarray]:
    """根据输入是字符串还是numpy数组，返回概率图数据。"""
    if isinstance(layer_source, str):
        if layer_source not in ctx.layers:
            print(f"警告: 概率层 '{layer_source}' 不存在。")
            return None
        return ctx.layers[layer_source]
    elif isinstance(layer_source, np.ndarray):
        # 安全检查：确保传入的数组维度与上下文匹配
        if layer_source.shape != (ctx.grid_width, ctx.grid_height):
            print(f"警告: 传入的概率图numpy数组维度 ({layer_source.shape}) 与网格维度 ({ctx.grid_width}, {ctx.grid_height}) 不匹配。")
            return None
        return layer_source
    else:
        print(f"警告: 未知的层源类型: {type(layer_source)}。")
        return None


def place_grid_objects_from_layer(
        ctx: GenerationContext,
        layer_source: Union[str, np.ndarray],
        num_to_place: int,
        obj_type: str,
        grid_size: Tuple[int, int],
        blocked_by: Set[str],
        max_attempts_multiplier: int = 100
)  -> Tuple[GenerationContext, List[GameObject]]:
    """
    使用真·加权采样在网格上放置物体，并进行碰撞检测。
    """
    placed_objects = []
    prob_map = _resolve_prob_map(ctx, layer_source)
    if prob_map is None:
        return ctx, placed_objects
    w, h = grid_size

    # 预先计算所有不可放置的位置
    valid_mask = np.ones_like(prob_map, dtype=bool)
    for x in range(ctx.grid_width):
        for y in range(ctx.grid_height):
            is_blocked = False
            # 检查目标区域内的每一个格子
            for i in range(x, x + w):
                for j in range(y, y + h):
                    # 检查该格子的对象列表
                    for occupant in ctx.occupancy_grid[i, j]:
                        if occupant.obj_type in blocked_by:
                            is_blocked = True
                            break
                    if is_blocked: break
                if is_blocked: break

            if is_blocked:
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
        placed_objects.append(new_obj)

        # 重要：更新 masked_probs 以防止在同一区域重复放置
        masked_probs[x:x + w, y:y + h] = 0

        placed_count += 1

    return ctx, placed_objects

def place_one_grid_object_from_layer(
        ctx: GenerationContext,
        layer_source: Union[str, np.ndarray],
        obj_type: str,
        grid_size: Tuple[int, int],
        blocked_by: Set[str]
) -> Tuple[GenerationContext, Optional[GameObject]]:
    """
    使用加权采样放置单个网格对齐的物体，并返回这个物体。
    如果无法放置，则返回 None。
    """
    prob_map = _resolve_prob_map(ctx, layer_source)
    if prob_map is None:
        return ctx, None

    w, h = grid_size

    valid_mask = np.ones_like(prob_map, dtype=bool)
    for x in range(ctx.grid_width):
        for y in range(ctx.grid_height):
            is_blocked = False
            # 检查目标区域内的每一个格子
            for i in range(x, x + w):
                for j in range(y, y + h):
                    # 检查该格子的对象列表
                    for occupant in ctx.occupancy_grid[i, j]:
                        if occupant.obj_type in blocked_by:
                            is_blocked = True
                            break
                    if is_blocked: break
                if is_blocked: break

            if is_blocked:
                valid_mask[x, y] = False

    masked_probs = prob_map * valid_mask
    flat_map = masked_probs.flatten()
    map_sum = np.sum(flat_map)

    if map_sum < 1e-9:
        print(f"警告: 没有有效的放置位置了，无法放置 {obj_type}。")
        return ctx, None

    normalized_probs = flat_map / map_sum
    chosen_index = np.random.choice(len(normalized_probs), p=normalized_probs)
    x, y = np.unravel_index(chosen_index, prob_map.shape)

    grid_pos = np.array([x, y])
    new_obj = GameObject(
        obj_type=obj_type,
        visual_pos=grid_pos.astype(float),
        grid_pos=grid_pos,
        grid_size=np.array(grid_size)
    )
    ctx.objects.append(new_obj)
    ctx.update_occupancy(new_obj)

    return ctx, new_obj

def place_floating_objects_from_layer(
        ctx: GenerationContext,
        layer_source: Union[str, np.ndarray],
        num_to_place: int,
        obj_type: str,
        blocked_by: Set[str],
        max_attempts_multiplier: int = 5  # TODO 目前没有用上？还需要吗？
) -> Tuple[GenerationContext, List[GameObject]]:
    """
    根据概率层，通过加权采样放置指定数量的非网格对齐的浮动对象。
    """
    placed_objects = []
    prob_map_original = _resolve_prob_map(ctx, layer_source)
    if prob_map_original is None:
        return ctx, []
    prob_map = prob_map_original.copy()

    # 已经被占用的格子不能放置
    # 1. 创建一个有效位置的掩码
    valid_mask = np.ones_like(prob_map, dtype=bool)
    w, h = ctx.grid_width, ctx.grid_height

    # 2. 遍历每一个格子，检查是否被指定的 blocker 占用
    for x in range(w):
        for y in range(h):
            is_blocked = False
            for occupant in ctx.occupancy_grid[x, y]:
                if occupant.obj_type in blocked_by:
                    is_blocked = True
                    break
            if is_blocked:
                valid_mask[x, y] = False

    # 3. 将掩码应用到概率图上，无效位置的概率将变为 0
    prob_map *= valid_mask

    if np.sum(prob_map) < 1e-9:
        print(f"警告: {obj_type} 没有有效的放置位置。")
        return ctx,placed_objects

    flat_map = prob_map.flatten()
    normalized_probs = flat_map / np.sum(flat_map)

    # 为了避免在完全相同的位置生成多个，我们可以选择不放回采样，
    # 或者像之前一样用一个集合来记录。这里我们选择一次性采样多个。
    # `replace=True` 允许在同一个格子附近生成多个对象，这对于脏污是合理的。
    chosen_indices = np.random.choice(
        len(normalized_probs),
        size=num_to_place,  # 直接采样目标数量
        p=normalized_probs,
        replace=True
    )

    placed_count = 0
    for index in chosen_indices:
        ix, iy = np.unravel_index(index, prob_map.shape)

        # 在选定的格子内添加随机偏移
        pos_x = ix + random.uniform(0, 1)
        pos_y = iy + random.uniform(0, 1)

        new_obj = GameObject(
            obj_type=obj_type,
            visual_pos=np.array([pos_x, pos_y])
        )
        ctx.objects.append(new_obj)
        placed_objects.append(new_obj)
        placed_count += 1

    print(f"成功放置了 {placed_count}/{num_to_place} 个 {obj_type}。")
    return ctx,placed_objects