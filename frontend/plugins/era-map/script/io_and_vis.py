import json
import math

import numpy as np
import matplotlib.pyplot as plt
import matplotlib.patches as patches
import random
from typing import List

from core_types import GenerationContext, GameObject
from prototype import Settings

# =============================================================================
# 1. 数据导出 (Data Export)
# =============================================================================

def  export_context_to_json(context: GenerationContext, settings: Settings, filename="init_layout.json"):
    """将生成上下文中的所有游戏对象和数据场导出为前端可以使用的JSON文件。"""

    # --- 序列化 Fields ---
    fields_data = {}
    for field_name, field_array in context.fields.items():
        fields_data[field_name] = field_array.tolist()

    # --- 序列化 Particles ---
    particles_data = {}
    for particle_name, particle_layer in context.particles.items():
        particles_data[particle_name] = {
            "type": particle_layer.type,
            "seed": particle_layer.seed,
            "densityGrid": particle_layer.density_grid.tolist(),
            "particleConfig": particle_layer.particle_config
        }

    # --- 序列化 Objects ---
    objects_data = []
    for obj in context.objects:
        obj_dict = {
            "obj_type": obj.obj_type,
            # 将 numpy 数组转换为列表以便 JSON 序列化
            "visual_pos": obj.visual_pos.tolist(),
            "visual_angle": obj.visual_angle,
            # 添加 uid
            "uid": obj.uid
        }

        # 可选属性：只有存在时才添加
        if obj.grid_pos is not None:
            obj_dict["grid_pos"] = obj.grid_pos.tolist()
        if obj.grid_size is not None:
            obj_dict["grid_size"] = obj.grid_size.tolist()

        objects_data.append(obj_dict)


    # --- 构建最终的JSON结构 ---
    meta_data = {
        "gridWidth": settings.GRID_WIDTH,
        "gridHeight": settings.GRID_HEIGHT,
    }

    output_data = {
        "meta": meta_data,
        "objects": objects_data,
        "fields": fields_data,
        "particles": particles_data,
    }

    try:
        with open(filename, 'w', encoding='utf-8') as f:
            json.dump(output_data, f, indent=2)
        print(f"--- 布局成功导出到文件: {filename} ---")
    except Exception as e:
        print(f"!!! 导出到JSON时发生错误: {e} !!!")

# =============================================================================
# 2. 可视化 (Visualization)
# =============================================================================

class Visualizer:
    def __init__(self, settings: Settings):
        self.settings = settings
        self.colors = settings.COLORS # 沿用Settings中的颜色配置

    def _draw_objects(self, ax: plt.Axes, objects: List[GameObject]):
        """在给定的 Axes 上绘制所有游戏对象。"""
        # 为了正确的绘制顺序，我们先绘制脏污，再绘制家具，最后绘制角色

        # 脏污
        grime_small = [o for o in objects if o.obj_type == "GRIME_SMALL"]
        grime_large = [o for o in objects if o.obj_type == "GRIME_LARGE"]

        # 环境装置类别
        fixtures = [o for o in objects if o.obj_type in ["WINDOW", "TORCH"]]
        characters = [o for o in objects if o.obj_type in ["ELF", "DWARF", "MUSHROOM_PERSON"]]

        if grime_small:
            positions = np.array([o.visual_pos for o in grime_small])
            ax.scatter(positions[:, 0], positions[:, 1], s=0.2*20, c=self.colors["GRIME_SMALL"], alpha=0.6, marker='o', edgecolors='none', zorder=0)

        if grime_large:
            for obj in grime_large:
                size = random.uniform(0.8, 1.5) * 5
                ax.scatter(obj.visual_pos[0], obj.visual_pos[1], s=size*20, c=self.colors["GRIME_LARGE"], alpha=0.6, marker='o', edgecolors='none', zorder=1)

        # 家具 (桌子和椅子)
        furniture = [o for o in objects if o.obj_type in ["TABLE", "CHAIR"]]
        for obj in furniture:
            if obj.grid_size is not None:
                w, h = obj.grid_size
                vx, vy = obj.visual_pos
                # matplotlib 的 Rectangle angle 是逆时针的
                rect = patches.Rectangle((vx, vy), w, h, linewidth=1, edgecolor='black', facecolor=self.colors.get(obj.obj_type), angle=obj.visual_angle, rotation_point='center', zorder=10)
                ax.add_patch(rect)

        # 3. 环境装置 (窗户和火把) (z-order: 15)
        for obj in fixtures:
            if obj.grid_pos is not None and obj.grid_size is not None:
                x, y = obj.grid_pos
                w, h = obj.grid_size
                # 使用一个简单的正方形来表示它们
                rect = patches.Rectangle((x, y), w, h, linewidth=1.5, edgecolor='#333333', facecolor=self.colors.get(obj.obj_type), zorder=15)
                ax.add_patch(rect)
                # 为火把添加一个发光效果
                if obj.obj_type == 'TORCH':
                    glow = patches.Rectangle((x, y), w, h, linewidth=0, facecolor=self.colors.get(obj.obj_type), alpha=0.4, zorder=14)
                    glow.set_transform(ax.transData + plt.matplotlib.transforms.Affine2D().scale(2.5).translate(-w*0.75, -h*0.75))
                    ax.add_patch(glow)

        # 角色
        for obj in characters:
            size = 0.8 # 默认角色大小
            ax.scatter(obj.visual_pos[0], obj.visual_pos[1], s=size*100, c=self.colors.get(obj.obj_type), alpha=1.0, marker='o', edgecolors='black', linewidth=1.5, zorder=20)
            # 添加首字母标签
            ax.text(obj.visual_pos[0], obj.visual_pos[1], obj.obj_type[0], ha='center', va='center', color='white', fontsize=8, weight='bold')

    def plot_layout_and_layers(self, context: GenerationContext):
        """
        动态绘制最终布局以及所有在上下文中找到的数据层。
        """
        s = self.settings

        # 1. 自动发现在 layers 和 fields 中所有可绘制的数据
        drawable_data = {**context.layers, **context.fields}
        drawable_layers = {
            name: layer for name, layer in drawable_data.items()
            if isinstance(layer, np.ndarray) and layer.ndim == 2
        }
        num_layers = len(drawable_layers)

        if num_layers == 0:
            print("--- 可视化: 未找到任何可绘制的数据。只显示最终布局。---")
            fig, ax_main = plt.subplots(figsize=(12, 8))
            ax_main.set_title("Final Generated Layout")
            self._setup_main_ax(ax_main)
            self._draw_objects(ax_main, context.objects)
            plt.tight_layout()
            plt.show()
            return

        # 2. 动态计算子图布局
        # 我们希望层视图占据左侧约 1/3 的空间，主视图占据右侧 2/3
        # 我们将层视图排列成 N 行 2 列的网格 (如果层不多)
        layer_cols = 2 if num_layers > 2 else 1
        layer_rows = math.ceil(num_layers / layer_cols)

        # 创建一个复杂的 Figure 布局
        fig = plt.figure(figsize=(20, 5 + layer_rows * 2)) # 动态调整高度
        # 使用 GridSpec 进行更灵活的布局
        # 左侧给3个单位宽度，右侧给6个单位宽度
        gs = fig.add_gridspec(layer_rows, layer_cols + 3)

        # 主布局图占据右侧所有行和后3个单位的列
        ax_main = fig.add_subplot(gs[:, layer_cols:])
        ax_main.set_title("Final Generated Layout")
        self._setup_main_ax(ax_main)

        # 在绘制对象之前，先绘制光照图作为背景
        if 'light_level' in context.fields:
            light_map = context.fields['light_level']
            # 使用 imshow 绘制光照图。设置 zorder=-1 确保它在最底层。
            # extent 参数确保图像的坐标与网格对齐。
            ax_main.imshow(
                light_map.T,
                cmap='inferno',
                origin='lower',
                alpha=0.6,  # 半透明，这样还能看到下面的网格线
                extent=(0, s.GRID_WIDTH, 0, s.GRID_HEIGHT),
                interpolation='bicubic', # 使用更平滑的插值
                zorder=-1
            )


        self._draw_objects(ax_main, context.objects)

        # 3. 遍历并绘制所有数据层
        layer_items = sorted(drawable_layers.items()) # 排序以保证每次绘制顺序一致

        for i, (layer_name, layer_data) in enumerate(layer_items):
            row = i // layer_cols
            col = i % layer_cols
            ax_layer = fig.add_subplot(gs[row, col])

            ax_layer.set_title(layer_name, fontsize=10)

            # 如果层名称包含 'light' 或 'temperature' 等，使用更合适的色图
            if 'light' in layer_name.lower():
                cmap = 'inferno'
            elif 'temp' in layer_name.lower():
                cmap = 'coolwarm'
            else:
                cmap = 'viridis'

            # 注意 imshow 需要转置 (T) 才能匹配我们的 (width, height) 坐标系
            im = ax_layer.imshow(layer_data.T, cmap=cmap, origin='lower', interpolation='nearest')
            ax_layer.set_xticks([])
            ax_layer.set_yticks([])
            # 添加一个颜色条来显示数值范围
            fig.colorbar(im, ax=ax_layer, fraction=0.046, pad=0.04)

        plt.tight_layout(pad=1.5, h_pad=2.0)
        plt.show()

    def _setup_main_ax(self, ax: plt.Axes):
        """辅助函数，用于统一设置主布局图的样式。"""
        s = self.settings
        ax.set_xlim(0, s.GRID_WIDTH)
        ax.set_ylim(0, s.GRID_HEIGHT)
        ax.set_xticks(np.arange(0, s.GRID_WIDTH + 1, 1))
        ax.set_yticks(np.arange(0, s.GRID_HEIGHT + 1, 1))
        ax.grid(which='major', color='#CCCCCC', linestyle='-', linewidth=0.5)
        ax.set_xticklabels([])
        ax.set_yticklabels([])
        ax.minorticks_off()
        ax.set_aspect('equal', adjustable='box')
        ax.invert_yaxis()