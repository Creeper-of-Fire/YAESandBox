import random
import time
from typing import List, Callable, Tuple, Optional, Union

import numpy as np
from perlin_noise import PerlinNoise

from core_types import GenerationContext, GameObject


# --- 一些辅助函数 ---
def safe_normalize(data_map):
    """安全地将一个2D numpy数组归一化到[0, 1]范围，处理分母为零的情况。"""
    min_val = np.min(data_map)
    max_val = np.max(data_map)
    data_range = max_val - min_val

    # 使用一个小的epsilon来安全地比较浮点数
    if data_range > 1e-9:
        return (data_map - min_val) / data_range
    else:
        # 如果数组是平坦的，返回一个全零数组
        return np.zeros_like(data_map)


# --- 修改器 (Modifiers) ---

def normalize_layer(
        ctx: GenerationContext,
        layer_name: str,
        target_layer_name: Optional[str] = None
) -> GenerationContext:
    """
    显式地将一个层归一化到 [0, 1] 范围。
    这是一个独立的、意图明确的原子操作。
    """
    if layer_name not in ctx.layers:
        print(f"警告: 层 '{layer_name}' 不存在，无法归一化。")
        return ctx

    if target_layer_name is None:
        target_layer_name = layer_name

    ctx.layers[target_layer_name] = safe_normalize(ctx.layers[layer_name])
    print(f"--- 已显式归一化层 '{layer_name}' -> '{target_layer_name}' ---")
    return ctx

def apply_perlin_noise(
        ctx: GenerationContext,
        target_layer_name: str,
        scale: float,
        strength: float,
        base_layer_name: str = None
) -> GenerationContext:
    """在指定层上应用或创建一个柏林噪声层。"""
    w, h = ctx.grid_width, ctx.grid_height

    # 创建一个巨大的随机偏移量来打破采样规则性
    # 这样即使用户输入 0.5 或 1.0 这样的“魔数”也能正常工作
    random.seed(time.time())  # 确保每次运行的偏移量都不同
    offset_x = random.random() * 10000
    offset_y = random.random() * 10000

    # 使用与时间相关的种子，确保每次运行的噪声都不同
    noise_gen = PerlinNoise(octaves=4, seed=int(time.time()))

    # 在采样时应用偏移量
    noise = np.array([[noise_gen([(i * scale) + offset_x, (j * scale) + offset_y]) for j in range(h)] for i in range(w)])

    noise_norm = safe_normalize(noise)

    if base_layer_name and base_layer_name in ctx.layers:
        base_layer = ctx.layers[base_layer_name]
        ctx.layers[target_layer_name] = base_layer * (1.0 - strength + strength * noise_norm)
    else:
        # 如果没有基础层，就直接创建噪声层
        ctx.layers[target_layer_name] = noise_norm

    np.clip(ctx.layers[target_layer_name], 0, 1, out=ctx.layers[target_layer_name])
    print(f"--- 应用柏林噪声到层 '{target_layer_name}' ---")
    return ctx


def create_uniform_layer(
        ctx: GenerationContext,
        target_layer_name: str,
        value: float = 1.0
) -> GenerationContext:
    """创建一个填充了均匀值的层。"""
    w, h = ctx.grid_width, ctx.grid_height
    ctx.layers[target_layer_name] = np.full((w, h), fill_value=value)
    print(f"--- 创建了值为 {value} 的均匀层 '{target_layer_name}' ---")
    return ctx


def create_layer_from_coordinates(
        ctx: GenerationContext,
        target_layer_name: str,
        coordinates: List[Tuple[int, int]],
        value: float = 1.0,
        base_layer: Optional[np.ndarray] = None
) -> GenerationContext:
    """
    根据一个坐标列表创建一个新层。在这些坐标上的值为指定值，其他地方为0。
    """
    w, h = ctx.grid_width, ctx.grid_height
    if base_layer is not None:
        new_map = base_layer.copy()
    else:
        new_map = np.zeros((w, h))

    for x, y in coordinates:
        if 0 <= x < w and 0 <= y < h:
            new_map[x, y] = value

    ctx.layers[target_layer_name] = new_map
    print(f"--- 从 {len(coordinates)} 个坐标点创建了层 '{target_layer_name}' ---")
    return ctx


def adjust_layer_contrast(
        ctx: GenerationContext,
        layer_name: str,
        exponent: float,
        target_layer_name: str = None
) -> GenerationContext:
    """
    通过幂运算调整一个层的对比度。
    exponent > 1: 增加对比度 (使高峰更突出)
    0 < exponent < 1: 降低对比度 (使分布更平缓)
    """
    if layer_name not in ctx.layers:
        print(f"警告: 层 '{layer_name}' 不存在，无法调整对比度。")
        return ctx

    if target_layer_name is None:
        target_layer_name = layer_name

    # 确保层的值在 [0, 1] 范围内
    layer_data = safe_normalize(ctx.layers[layer_name])

    adjusted_map = np.power(layer_data, exponent)

    ctx.layers[target_layer_name] = safe_normalize(adjusted_map)  # 再次归一化
    print(f"--- 调整层 '{layer_name}' 的对比度 (指数: {exponent}) -> '{target_layer_name}' ---")
    return ctx


def apply_influence_from_points(
        ctx: GenerationContext,
        target_layer_name: str,
        points: List[Tuple[float, float]],
        sigma: float,
        strength: float = 1.0,
        mode: str = 'add'  # 'add', 'subtract', 'multiply', 'multiply_inverse'
) -> GenerationContext:
    """
    基于一组坐标点计算影响，并按指定模式将其应用到一个目标层上。
    此操作是幂等的：如果目标层不存在，会自动创建。
    """
    w, h = ctx.grid_width, ctx.grid_height

    if not points:
        return ctx

    # 1. 幂等性：按需创建目标层
    ctx = ensure_layer_exists(ctx, target_layer_name, fill_value=0.0)

    # 2. 计算新产生的影响图 (不存入 ctx.layers)
    new_influence_map = np.zeros((w, h))

    x_coords, y_coords = np.arange(w), np.arange(h)
    xx, yy = np.meshgrid(x_coords, y_coords)
    sigma_sq = sigma ** 2
    if sigma_sq <= 1e-9:
        return ctx

    for p in points:
        px, py = p
        distance_sq = (xx - px) ** 2 + (yy - py) ** 2
        new_influence_map += np.exp(-distance_sq / (2 * sigma_sq)).T * strength

    new_influence_map = safe_normalize(new_influence_map)

    # 3. 根据模式将新影响合并到目标层
    target_layer = ctx.layers[target_layer_name]
    if mode == 'add':
        target_layer += new_influence_map
    elif mode == 'subtract':
        target_layer -= new_influence_map
    elif mode == 'multiply':
        target_layer *= new_influence_map
    elif mode == 'multiply_inverse':
        target_layer *= (1.0 - new_influence_map)
    else:
        print(f"警告: 在 apply_influence_from_points 中使用了未知的模式 '{mode}'。")
        return ctx

    # 3. 将新影响合并到目标层
    ctx.layers[target_layer_name] += new_influence_map

    # 防止减法产生负值
    if mode == 'subtract' or mode == 'multiply_inverse':
        np.clip(target_layer, 0, None, out=target_layer)

    print(f"--- 向层 '{target_layer_name}' 应用了来自 {len(points)} 个点的影响 ---")
    return ctx


def apply_influence_to_layer(
        ctx: GenerationContext,
        target_layer_name: str,
        source_objects: List[GameObject],
        sigma: float,
        strength_multiplier: Callable[[GameObject], float] = lambda obj: 1.0,  # 按对象类型决定强度
        mode: str = 'add'  # 'add', 'subtract', 'multiply', 'multiply_inverse'
) -> GenerationContext:
    """
    计算一组源对象产生的影响，并按指定模式将其应用到一个目标层上。
    此操作是幂等的：如果目标层不存在，会自动创建。
    """
    if not source_objects:
        return ctx  # 如果没有源对象，什么都不做

    # 1. 幂等性：按需创建目标层
    ctx = ensure_layer_exists(ctx, target_layer_name, fill_value=(0.0 if mode == 'add' else 1.0))

    # 2. 计算新产生的影响图 (不存入 ctx.layers)
    w, h = ctx.grid_width, ctx.grid_height
    new_influence_map = np.zeros((w, h))

    x_coords, y_coords = np.arange(w), np.arange(h)
    xx, yy = np.meshgrid(x_coords, y_coords)
    sigma_sq = sigma ** 2
    if sigma_sq <= 1e-9: return ctx

    for obj in source_objects:
        center_pos = obj.center_visual_pos
        strength = strength_multiplier(obj)
        if strength == 0: continue

        distance_sq = (xx - center_pos[0]) ** 2 + (yy - center_pos[1]) ** 2
        new_influence_map += np.exp(-distance_sq / (2 * sigma_sq)).T * strength

    # 归一化新产生的影响，使其最大值为1，这样strength参数才可控
    new_influence_map = safe_normalize(new_influence_map)

    # 3. 将新影响直接合并到目标层
    target_layer = ctx.layers[target_layer_name]
    if mode == 'add':
        target_layer += new_influence_map
    elif mode == 'subtract':
        target_layer -= new_influence_map
    elif mode == 'multiply':
        target_layer *= new_influence_map
    elif mode == 'multiply_inverse':
        target_layer *= (1.0 - new_influence_map)
    else:
        print(f"警告: 在 apply_influence_to_layer 中使用了未知的模式 '{mode}'。")
        return ctx

    # 防止减法产生负值
    if mode == 'subtract' or mode == 'multiply_inverse':
        np.clip(target_layer, 0, None, out=target_layer)

    print(f"--- 向层 '{target_layer_name}' 应用了来自 {len(source_objects)} 个对象的影响 ---")
    return ctx


def apply_visual_jitter(
        ctx: GenerationContext,
        position_jitter: float,
        angle_jitter_degrees: float
) -> GenerationContext:
    """
    为所有现存对象的视觉位置和角度增加随机扰动。
    只影响有 grid_pos 的物体 (通常是家具)。
    """
    print("--- 开始应用视觉抖动 ---")

    for obj in ctx.objects:
        # 只对有实体格子的对象进行抖动，避免移动角色或脏污等浮动物体
        if obj.grid_pos is not None:
            offset = np.random.uniform(-position_jitter, position_jitter, 2)
            obj.visual_pos += offset
            obj.visual_angle = random.uniform(-angle_jitter_degrees, angle_jitter_degrees)

    print("--- 视觉抖动应用完毕 ---")
    return ctx


def combine_layers(
        ctx: GenerationContext,
        target_layer_name: str,
        layer_a_name: str,
        layer_b_name: str,
        mode: str = 'weighted_sum',  # 'add', 'multiply', 'weighted_sum'
        weight_a: float = 0.5,
        weight_b: float = 0.5
) -> GenerationContext:
    """
    将两个源层通过指定模式组合，并将结果存入目标层。
    """
    if layer_a_name not in ctx.layers or layer_b_name not in ctx.layers:
        print(f"警告: 组合操作所需的源层 ({layer_a_name} 或 {layer_b_name}) 不存在。")
        return ctx

    layer_a = ctx.layers[layer_a_name]
    layer_b = ctx.layers[layer_b_name]

    if mode == 'add':
        combined_map = layer_a + layer_b
    elif mode == 'multiply':
        combined_map = layer_a * layer_b
    elif mode == 'weighted_sum':
        # 注意：如果 a 和 b 都是 [0,1] 的概率图，加权和之后通常也希望是 [0,1]
        # 但为了通用性，我们也移除归一化，让用户显式处理
        combined_map = (layer_a * weight_a) + (layer_b * weight_b)
    else:
        print(f"警告: 未知的组合模式 '{mode}'。")
        return ctx

    ctx.layers[target_layer_name] = combined_map

    print(f"--- 组合层 '{layer_a_name}' 和 '{layer_b_name}' -> '{target_layer_name}' (模式: {mode}) ---")
    return ctx


def bind_floating_objects_to_grid(ctx: GenerationContext) -> GenerationContext:
    """
    遍历所有游戏对象，为那些只有 visual_pos 而没有 grid_pos 的对象
    （例如角色、脏污）计算并赋予一个网格坐标。

    绑定规则：简单地取 visual_pos 的整数部分 (floor)。
    """
    print("--- 开始将浮动对象绑定到网格 ---")

    updated_count = 0
    for obj in ctx.objects:
        # 如果一个对象已经有 grid_pos (如家具)，我们不应该动它。
        if obj.grid_pos is None:
            # 计算网格坐标
            grid_x = int(np.floor(obj.visual_pos[0]))
            grid_y = int(np.floor(obj.visual_pos[1]))

            # 确保坐标在网格范围内
            grid_x = max(0, min(ctx.grid_width - 1, grid_x))
            grid_y = max(0, min(ctx.grid_height - 1, grid_y))

            obj.grid_pos = np.array([grid_x, grid_y])

            updated_count += 1

    print(f"--- 网格绑定完毕，更新了 {updated_count} 个对象 ---")
    return ctx


def ensure_layer_exists(
        ctx: GenerationContext,
        layer_name: str,
        fill_value: float = 0.0
) -> GenerationContext:
    """
    确保指定的层存在。如果不存在，则创建一个以 fill_value 填充的新层。
    这是一个幂等操作。
    """
    if layer_name not in ctx.layers:
        print(f"--- 层 '{layer_name}' 不存在，正在按需创建 (填充值: {fill_value}) ---")
        w, h = ctx.grid_width, ctx.grid_height
        ctx.layers[layer_name] = np.full((w, h), fill_value=fill_value)
    return ctx


def promote_layer_to_field(
        ctx: GenerationContext,
        source_layer_name: str,
        target_field_name: str,
        remove_source: bool = True
) -> GenerationContext:
    """
    将一个临时层“提升”为一个永久的场。
    """
    if source_layer_name not in ctx.layers:
        print(f"警告: 源层 '{source_layer_name}' 不存在，无法提升为场。")
        return ctx

    print(f"--- 将层 '{source_layer_name}' 提升为场 '{target_field_name}' ---")
    ctx.fields[target_field_name] = ctx.layers[source_layer_name].copy()

    if remove_source:
        del ctx.layers[source_layer_name]

    return ctx


def reserve_grid_margin(
        ctx: GenerationContext,
        margin_width: int,
        occupant_type: str = "WALL_RESERVED"
) -> GenerationContext:
    """
    在占用网格 (occupancy_grid) 的边缘保留指定宽度的区域。
    这可以模拟墙壁，防止物体生成在地图的最边缘。

    我们通过放置一个虚拟的、不可见的 GameObject 来实现占用。
    """
    if margin_width <= 0:
        return ctx

    print(f"--- 预留 {margin_width} 格宽的边缘区域 ---")
    w, h = ctx.grid_width, ctx.grid_height

    # 创建一个虚拟的占位对象，用于标记这些格子
    # 它不会被添加到 ctx.objects 列表中，因此不会被导出或渲染
    wall_placeholder = GameObject(obj_type=occupant_type, visual_pos=np.array([-1, -1]))

    # 上边缘
    for i in range(w):
        for j in range(margin_width):
            ctx.occupancy_grid[i, j].append(wall_placeholder)
    # 下边缘
    for i in range(w):
        for j in range(h - margin_width, h):
            ctx.occupancy_grid[i, j].append(wall_placeholder)
    # 左边缘
    for i in range(margin_width):
        for j in range(margin_width, h - margin_width):  # 避免重复计算角落
            ctx.occupancy_grid[i, j].append(wall_placeholder)
    # 右边缘
    for i in range(w - margin_width, w):
        for j in range(margin_width, h - margin_width):  # 避免重复计算角落
            ctx.occupancy_grid[i, j].append(wall_placeholder)

    # (可选) 也可以在关键的数据层上直接将这些区域的概率设为0
    # 这可以提高后续采样放置器的效率，因为它们不必再考虑这些无效区域
    for layer_name, layer in ctx.layers.items():
        if isinstance(layer, np.ndarray) and layer.shape == (w, h):
            layer[0:margin_width, :] = 0
            layer[w - margin_width:w, :] = 0
            layer[:, 0:margin_width] = 0
            layer[:, h - margin_width:h] = 0

    print("--- 边缘预留完毕 ---")
    return ctx
