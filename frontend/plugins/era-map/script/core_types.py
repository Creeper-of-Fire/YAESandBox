from typing import Optional, Any, cast

import numpy as np
import matplotlib.pyplot as plt
import matplotlib.patches as patches
from perlin_noise import PerlinNoise
import random
import time
from scipy.ndimage import gaussian_filter
import json
from dataclasses import dataclass, field
from typing import List, Dict, Tuple, Callable

@dataclass
class ParticleLayer:
    type: str  # e.g., "GRIME_PARTICLE"
    seed: int
    density_grid: np.ndarray
    # 我们用一个字典来存储前端生成粒子所需的其他配置
    # TODO 应该是在前端定义才对，这里先不管
    particle_config: Dict[str, Any]


@dataclass
class GameObject:
    obj_type: str
    visual_pos: np.ndarray  # 使用 numpy 数组以支持向量操作
    visual_angle: float = 0.0

    # 逻辑属性，对于非网格对齐物体可以是 None
    grid_pos: Optional[np.ndarray] = None
    grid_size: Optional[np.ndarray] = None

    # 唯一ID，用于引用
    uid: int = field(default_factory=lambda: id(GameObject))

    # 为了简化，我们可以添加一些辅助属性
    @property
    def center_visual_pos(self) -> np.ndarray:
        if self.grid_size is not None:
            return self.visual_pos + self.grid_size / 2
        return self.visual_pos

# 这是我们所有状态的容器
@dataclass
class GenerationContext:
    grid_width: int
    grid_height: int

    # 用于生成过程中的临时数据层 (e.g., 'table_suitability', 'grime_probability')
    layers: Dict[str, np.ndarray]

    # 用于最终导出的、描述世界状态的持久化数据场 (e.g., 'light_level', 'temperature')
    fields: Dict[str, np.ndarray]

    particles: Dict[str, ParticleLayer]

    # 存储所有已生成的游戏对象
    objects: List[GameObject]

    # 逻辑网格，用于快速碰撞检测，存储 GameObject 的引用
    occupancy_grid: np.ndarray

    # 辅助方法，用于在放置对象后更新 occupancy_grid
    def update_occupancy(self, obj: GameObject):
        if obj.grid_pos is not None and obj.grid_size is not None:
            x, y = obj.grid_pos
            w, h = obj.grid_size
            for i in range(x, x + w):
                for j in range(y, y + h):
                    # 确保坐标在边界内
                    if 0 <= i < self.grid_width and 0 <= j < self.grid_height:
                        # self.occupancy_grid[i, j] 是一个列表
                        cell_list = cast(List[GameObject], self.occupancy_grid[i, j])
                        cell_list.append(obj)

def convert_objects_to_particles(
        ctx: GenerationContext,
        object_type_to_convert: str,
        target_particle_type: str,
        seed: int,
        particle_config: Dict[str, Any]
) -> GenerationContext:
    """
    查找指定类型的GameObject，将它们转换为一个ParticleLayer，
    并从主对象列表中移除它们。
    """
    print(f"--- 开始将 '{object_type_to_convert}' 对象转换为粒子层 '{target_particle_type}' ---")

    # 1. 初始化一个空的密度网格
    density_grid = np.zeros((ctx.grid_width, ctx.grid_height), dtype=int)

    # 2. 筛选出需要转换的对象和需要保留的对象
    objects_to_convert = []
    objects_to_keep = []
    for obj in ctx.objects:
        if obj.obj_type == object_type_to_convert:
            objects_to_convert.append(obj)
        else:
            objects_to_keep.append(obj)

    if not objects_to_convert:
        print(f"--- 未找到类型为 '{object_type_to_convert}' 的对象，跳过粒子转换。---")
        return ctx

    # 3. 遍历需要转换的对象，填充密度网格
    #    我们使用对象的 grid_pos，因此这个函数应该在 bind_floating_objects_to_grid 之后调用
    for obj in objects_to_convert:
        if obj.grid_pos is not None:
            gx, gy = obj.grid_pos
            # 确保坐标在网格范围内
            if 0 <= gx < ctx.grid_width and 0 <= gy < ctx.grid_height:
                density_grid[gx, gy] += 1
        else:
            # 这是一个安全警告，理论上不应该发生
            print(f"警告: 对象 (uid={obj.uid}, type={obj.obj_type}) 没有 grid_pos，无法转换为粒子。")

    # 4. 创建新的 ParticleLayer 实例
    new_particle_layer = ParticleLayer(
        type=target_particle_type,
        seed=seed,
        density_grid=density_grid,
        particle_config=particle_config
    )

    # 5. 将新的粒子层添加到上下文中
    ctx.particles[target_particle_type] = new_particle_layer

    # 6. 更新上下文中的对象列表，移除已转换的对象
    original_count = len(ctx.objects)
    ctx.objects = objects_to_keep
    converted_count = original_count - len(ctx.objects)

    print(f"--- 转换完成: {converted_count} 个对象被转换为粒子。剩余对象: {len(ctx.objects)} ---")

    return ctx

# 定义“修改器”和“放置策略”的函数签名
# 它们都接收一个上下文，并返回一个新的（或修改过的）上下文
ModifierFunc = Callable[['GenerationContext'], 'GenerationContext']
PlacementFunc = Callable[['GenerationContext'], 'GenerationContext']