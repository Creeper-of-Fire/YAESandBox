from typing import Optional

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

    # 使用字典来存储任意命名的数据层
    # 键是字符串 (e.g., 'suitability', 'grime_potential')
    # 值是 numpy 数组
    layers: Dict[str, np.ndarray]

    # 存储所有已生成的游戏对象
    objects: List[GameObject]

    # 逻辑网格，用于快速碰撞检测，存储 GameObject 的引用
    occupancy_grid: np.ndarray

    # 辅助方法，用于在放置对象后更新 occupancy_grid
    def update_occupancy(self, obj: GameObject):
        if obj.grid_pos is not None and obj.grid_size is not None:
            x, y = obj.grid_pos
            w, h = obj.grid_size
            self.occupancy_grid[x:x+w, y:y+h] = obj


# 定义“修改器”和“放置策略”的函数签名
# 它们都接收一个上下文，并返回一个新的（或修改过的）上下文
ModifierFunc = Callable[['GenerationContext'], 'GenerationContext']
PlacementFunc = Callable[['GenerationContext'], 'GenerationContext']