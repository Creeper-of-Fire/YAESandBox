import json
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

def export_context_to_json(context: GenerationContext, settings: Settings, filename="layout.json"):
    """将生成上下文中的所有游戏对象导出为前端可以使用的JSON文件。"""

    meta_data = {
        "gridWidth": settings.GRID_WIDTH,
        "gridHeight": settings.GRID_HEIGHT
    }

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

    output_data = {"meta": meta_data, "objects": objects_data}

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

        # 角色
        characters = [o for o in objects if o.obj_type in ["ELF", "DWARF", "MUSHROOM_PERSON"]]
        for obj in characters:
            size = 0.8 # 默认角色大小
            ax.scatter(obj.visual_pos[0], obj.visual_pos[1], s=size*100, c=self.colors.get(obj.obj_type), alpha=1.0, marker='o', edgecolors='black', linewidth=1.5, zorder=20)
            # 添加首字母标签
            ax.text(obj.visual_pos[0], obj.visual_pos[1], obj.obj_type[0], ha='center', va='center', color='white', fontsize=8, weight='bold')

    def plot_layout_and_layers(self, context: GenerationContext):
        """
        绘制最终的布局以及一些关键的调试数据层。
        """
        s = self.settings

        fig = plt.figure(figsize=(20, 10))
        gs = fig.add_gridspec(2, 3)

        # --- 主布局图 ---
        ax_main = fig.add_subplot(gs[:, 1:])
        ax_main.set_title("Final Generated Layout")
        ax_main.set_xlim(0, s.GRID_WIDTH)
        ax_main.set_ylim(0, s.GRID_HEIGHT)
        ax_main.set_xticks(np.arange(0, s.GRID_WIDTH + 1, 1))
        ax_main.set_yticks(np.arange(0, s.GRID_HEIGHT + 1, 1))
        ax_main.grid(which='major', color='#CCCCCC', linestyle='-', linewidth=0.8)
        ax_main.set_xticklabels([])
        ax_main.set_yticklabels([])
        ax_main.minorticks_off()
        ax_main.set_aspect('equal', adjustable='box')
        ax_main.invert_yaxis()

        self._draw_objects(ax_main, context.objects)

        # --- 调试数据层图 ---
        def plot_layer(ax, layer_name, title):
            if layer_name in context.layers:
                ax.set_title(title)
                # 注意 imshow 需要转置 (T) 才能匹配我们的 (width, height) 坐标系
                im = ax.imshow(context.layers[layer_name].T, cmap='viridis', origin='lower', interpolation='nearest')
                ax.set_xticklabels([])
                ax.set_yticklabels([])
                # fig.colorbar(im, ax=ax) # 如果需要颜色条
            else:
                ax.set_title(f"{title}\n(Layer not found)")
                ax.set_xticks([])
                ax.set_yticks([])

        ax1 = fig.add_subplot(gs[0, 0])
        plot_layer(ax1, 'table_suitability', 'Table Suitability Map')

        ax2 = fig.add_subplot(gs[1, 0])
        plot_layer(ax2, 'grime_base', 'Final Grime Probability Map')

        plt.tight_layout()
        plt.show()