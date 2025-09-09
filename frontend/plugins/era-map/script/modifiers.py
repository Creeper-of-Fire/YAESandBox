import random
from typing import List

from core_types import GenerationContext, GameObject
from perlin_noise import PerlinNoise
import numpy as np
import time


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

def create_initial_suitability(width: int, height: int) -> np.ndarray:
    """创建一个基础的适宜度地图 (全1)"""
    return np.ones((width, height))

def apply_central_attraction(ctx: GenerationContext, layer_name: str, strength: float) -> GenerationContext:
    """在指定层上应用中心吸引力。"""
    if layer_name not in ctx.layers:
        print(f"警告: 层 '{layer_name}' 不在上下文中。跳过。")
        return ctx

    w, h = ctx.grid_width, ctx.grid_height
    x = np.arange(w)
    y = np.arange(h)
    xx, yy = np.meshgrid(x, y)
    center_x, center_y = w / 2, h / 2
    distance_from_center_sq = (xx - center_x) ** 2 + (yy - center_y) ** 2

    # 吸引力应该使中心概率更高，所以我们从1中减去
    # 我们转置以匹配numpy的 (width, height) 索引
    attraction_multiplier = 1.0 - safe_normalize(distance_from_center_sq.T) * strength
    ctx.layers[layer_name] *= attraction_multiplier
    np.clip(ctx.layers[layer_name], 0, 1, out=ctx.layers[layer_name])

    return ctx

def apply_perlin_noise(ctx: GenerationContext, layer_name: str, scale: float, strength: float) -> GenerationContext:
    """在指定层上应用柏林噪声。"""
    if layer_name not in ctx.layers: return ctx

    w, h = ctx.grid_width, ctx.grid_height
    noise_gen = PerlinNoise(octaves=4, seed=int(time.time()))
    noise = np.array([[noise_gen([i * scale, j * scale]) for j in range(h)] for i in range(w)])
    noise_norm = safe_normalize(noise)

    # 将噪声作为乘数应用
    ctx.layers[layer_name] *= (1.0 - strength + strength * noise_norm)
    np.clip(ctx.layers[layer_name], 0, 1, out=ctx.layers[layer_name])

    return ctx

def apply_repulsion_from_objects(ctx: GenerationContext, layer_name: str, objects: List[GameObject], sigma: float, strength: float) -> GenerationContext:
    """根据物体列表，在指定层上应用高斯排斥。"""
    if layer_name not in ctx.layers or not objects: return ctx

    w, h = ctx.grid_width, ctx.grid_height
    x_coords, y_coords = np.arange(w), np.arange(h)
    xx, yy = np.meshgrid(x_coords, y_coords)

    total_repulsion = np.zeros((w, h))
    for obj in objects:
        center_pos = obj.center_visual_pos
        distance_sq = (xx - center_pos[0]) ** 2 + (yy - center_pos[1]) ** 2
        sigma_sq = sigma ** 2
        if sigma_sq > 0:
            gaussian_falloff = np.exp(-distance_sq / (2 * sigma_sq))
            total_repulsion += gaussian_falloff.T

    # 归一化总排斥影响，防止叠加效果过强
    total_repulsion = safe_normalize(total_repulsion)
    multiplier = 1.0 - strength * total_repulsion
    ctx.layers[layer_name] *= multiplier
    np.clip(ctx.layers[layer_name], 0, 1, out=ctx.layers[layer_name])

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

def create_grime_base_map_from_occlusion(
        ctx: GenerationContext,
        layer_name: str,
        occlusion_strength: float,
        occlusion_sigma: float
) -> GenerationContext:
    """
    创建一个基础脏污概率图。脏污倾向于在物体下方和周围聚集。
    """
    w, h = ctx.grid_width, ctx.grid_height
    activity_map = np.ones((w, h))

    x_coords, y_coords = np.arange(w), np.arange(h)
    xx, yy = np.meshgrid(x_coords, y_coords)

    for obj in ctx.objects:
        if obj.grid_pos is not None:
            center_pos = obj.center_visual_pos
            distance_sq = (xx - center_pos[0]) ** 2 + (yy - center_pos[1]) ** 2
            sigma_sq = occlusion_sigma ** 2
            if sigma_sq > 0:
                # 物体周围的“活动”减少
                activity_map -= np.exp(-distance_sq / (2 * sigma_sq)).T * occlusion_strength

    # 低活动区域 = 高脏污概率
    grime_prob = 1.0 - np.clip(activity_map, 0, 1)
    ctx.layers[layer_name] = safe_normalize(grime_prob)

    return ctx

def blend_layers(
        ctx: GenerationContext,
        target_layer: str,
        source_layer: str,
        weight_target: float,
        weight_source: float
) -> GenerationContext:
    """将两个层按权重混合到目标层中。"""
    if target_layer not in ctx.layers or source_layer not in ctx.layers:
        print("警告: 混合操作所需的层不存在。")
        return ctx

    blended_map = (ctx.layers[target_layer] * weight_target) + (ctx.layers[source_layer] * weight_source)
    ctx.layers[target_layer] = safe_normalize(blended_map)
    return ctx

def create_social_comfort_map(
        ctx: GenerationContext,
        layer_name: str,
        attraction_sigma: float
) -> GenerationContext:
    """创建基于家具吸引力的社交舒适度地图。"""
    w, h = ctx.grid_width, ctx.grid_height
    social_map = np.zeros((w, h))

    furniture = [obj for obj in ctx.objects if obj.obj_type in ["TABLE", "CHAIR"]]
    if not furniture:
        ctx.layers[layer_name] = social_map
        return ctx

    x_coords, y_coords = np.arange(w), np.arange(h)
    xx, yy = np.meshgrid(x_coords, y_coords)

    for obj in furniture:
        center_pos = obj.center_visual_pos
        distance_sq = (xx - center_pos[0]) ** 2 + (yy - center_pos[1]) ** 2
        sigma_sq = attraction_sigma ** 2
        if sigma_sq > 0:
            social_map += np.exp(-distance_sq / (2 * sigma_sq)).T

    ctx.layers[layer_name] = safe_normalize(social_map)
    return ctx


def create_grime_influence_map(
        ctx: GenerationContext,
        layer_name: str,
        large_grime_multiplier: float
) -> GenerationContext:
    """创建脏污影响图，大脏污的影响力更强。"""
    w, h = ctx.grid_width, ctx.grid_height
    grime_map = np.zeros((w, h))

    grime_objects = [obj for obj in ctx.objects if "GRIME" in obj.obj_type]
    if not grime_objects:
        ctx.layers[layer_name] = grime_map
        return ctx

    x_coords, y_coords = np.arange(w), np.arange(h)
    xx, yy = np.meshgrid(x_coords, y_coords)

    for grime in grime_objects:
        center_pos = grime.visual_pos
        distance_sq = (xx - center_pos[0]) ** 2 + (yy - center_pos[1]) ** 2

        is_large = "LARGE" in grime.obj_type
        sigma = 2.0 if is_large else 1.0 # 这可以变成参数
        strength = large_grime_multiplier if is_large else 1.0

        sigma_sq = sigma ** 2
        if sigma_sq > 0:
            grime_map += np.exp(-distance_sq / (2 * sigma_sq)).T * strength

    ctx.layers[layer_name] = safe_normalize(grime_map)
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
    ctx.occupancy_grid[0:w, 0:margin_width] = wall_placeholder
    # 下边缘
    ctx.occupancy_grid[0:w, h-margin_width:h] = wall_placeholder
    # 左边缘
    ctx.occupancy_grid[0:margin_width, 0:h] = wall_placeholder
    # 右边缘
    ctx.occupancy_grid[w-margin_width:w, 0:h] = wall_placeholder

    # (可选) 也可以在关键的数据层上直接将这些区域的概率设为0
    # 这可以提高后续采样放置器的效率，因为它们不必再考虑这些无效区域
    for layer_name, layer in ctx.layers.items():
        if isinstance(layer, np.ndarray) and layer.shape == (w, h):
            layer[0:margin_width, :] = 0
            layer[w-margin_width:w, :] = 0
            layer[:, 0:margin_width] = 0
            layer[:, h-margin_width:h] = 0

    print("--- 边缘预留完毕 ---")
    return ctx