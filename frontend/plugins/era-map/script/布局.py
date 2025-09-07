#%%
import numpy as np
import matplotlib.pyplot as plt
import matplotlib.patches as patches
from perlin_noise import PerlinNoise
import random
import time
from scipy.ndimage import gaussian_filter
import json


# =============================================================================
# 1. 配置 (Settings)
# =============================================================================
# 我们可以调整这里的所有参数来观察不同的生成效果
class Settings:
    GRID_WIDTH = 30
    GRID_HEIGHT = 20

    # --- 阶段一: 桌子生成 ---
    NUM_TABLES = 12
    TABLE_SIZE = (2, 1)  # (width, height) in grid units

    # '适宜度地图' 参数
    CENTER_ATTRACTION_STRENGTH = 0.005  # 桌子被吸引到中心的强度
    NOISE_SCALE = 0.1  # 噪声的“缩放”级别，值越小，团块越大
    NOISE_STRENGTH = 0.6  # 噪声对最终概率的影响强度 (0 to 1)

    # 桌子间的动态排斥
    TABLE_REPULSION_STRENGTH = 0.65  # 一个桌子能降低其中心概率的程度 (0-1, 1表示降为0)
    TABLE_REPULSION_SIGMA = 5.0  # 排斥“光环”的半径 (单位:格子)

    # --- 阶段二: 椅子生成 ---
    CHAIR_SIZE = (1, 1)
    CHAIRS_PER_TABLE_RANGE = (2, 4)  # 每张桌子生成的椅子数量范围

    # --- 阶段三: 增加混沌感 ---
    POSITION_JITTER = 0.2  # 位置的随机偏移量 (单位:格子)
    ANGLE_JITTER_DEGREES = 10  # 角度的随机偏移量 (单位:度)

    # --- 阶段四: 脏污生成 ---
    # 第1遍: 小脏污种子
    GRIME_SPLATTER_COUNT = 5000  # 初始“飞溅物”种子的数量
    GRIME_SPLATTER_NOISE_SCALE = 0.5  # 飞溅物噪声更细碎

    # 第2遍: 大脏污核心
    GRIME_DENSITY_BLUR_SIGMA = 1.5  # 计算密度的模糊半径
    NUM_LARGE_GRIME_PATCHES = 5  # 生成的大块脏污核心数量

    # 第3遍: 最终固化
    GRIME_FINAL_SURVIVAL_PROB = 0.4  # 小脏污的“基础”存活率
    GRIME_CORE_INFLUENCE_RADIUS = 4.0  # 大核心能提升小脏污存活率的范围

    # --- 阶段五: 角色生成 ---
    NUM_ELVES = 30
    NUM_DWARVES = 30
    NUM_MUSHROOM_PEOPLE = 20

    # 舒适度地图权重
    SOCIAL_ATTRACTION_STRENGTH = 1.0  # 对家具的吸引力
    SOCIAL_ATTRACTION_SIGMA = 2.5  # 家具“人气”光环大小

    ELF_GRIME_REPULSION = -2.5  # 精灵对脏污的厌恶
    ELF_LARGE_GRIME_MULTIPLIER = 40.0  # 大脏污对精灵的厌恶是小脏污的5倍

    MUSHROOM_GRIME_ATTRACTION = 4.5  # 蘑菇人对脏污的喜好

    # --- 可视化 ---
    COLORS = {
        "TABLE": "#8B4513",  # 棕色
        "CHAIR": "#D2691E",  # 巧克力色
        "GRIME_LARGE": "#556B2F",  # 暗橄榄绿
        "GRIME_SMALL": "#6B8E23",  # 橄榄褐
        "ELF": "#98FB98",  # 淡绿色
        "DWARF": "#F4A460",  # 沙棕色
        "MUSHROOM_PERSON": "#DC143C",  # 猩红色
    }


# =============================================================================
# 2. 数据结构 (Data Structures)
# =============================================================================
class GameObject:
    """代表一个在世界中的物体"""

    def __init__(self, obj_type, grid_pos, grid_size, visual_pos=None, visual_angle=0.0):
        self.obj_type = obj_type

        # visual_pos 是渲染的真理
        self.visual_pos = np.array(visual_pos) if visual_pos is not None else np.array(grid_pos, dtype=float)
        self.visual_angle = visual_angle

        # grid_pos 是逻辑的真理
        # 如果提供了 grid_pos (例如家具)，直接使用
        if grid_pos is not None:
            self.grid_pos = np.array(grid_pos)
        # 如果没有提供 (例如角色、脏污)，从 visual_pos 计算得出
        else:
            self.grid_pos = np.floor(self.visual_pos).astype(int)

        self.grid_size = np.array(grid_size) if grid_size is not None else None

        self.visual_size = 0.8  # Default size for characters
        if "TABLE" in self.obj_type or "CHAIR" in self.obj_type:
            self.visual_size = None  # Furniture uses patches
        elif "GRIME_SMALL" in self.obj_type:
            self.visual_size = 0.2
        elif "GRIME_LARGE" in self.obj_type:
            self.visual_size = random.uniform(0.8, 1.5) * 5

    def __repr__(self):
        return f"{self.obj_type} at {self.grid_pos}"


# =============================================================================
# 3. 核心生成器 (The Generator Pipeline)
# =============================================================================
class LayoutGenerator:
    def __init__(self, settings):
        self.settings = settings
        self.grid = np.zeros((settings.GRID_WIDTH, settings.GRID_HEIGHT), dtype=object)
        self.objects = []
        self.suitability_map = None
        # 用于调试, 存储每个角色的舒适度图
        self.debug_comfort_maps = {}

    def _safe_normalize(self, data_map):
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

    def generate_layout(self):
        """按顺序执行生成管线的三个阶段"""
        print("--- 开始生成布局 ---")

        # 阶段一: 放置桌子
        print("阶段一: 基于概率地图放置桌子...")
        self._generate_suitability_map()
        self._place_tables()

        # 阶段二: 放置椅子
        print("阶段二: 基于规则放置椅子...")
        self._place_chairs()

        # 阶段三: 增加混沌感
        print("阶段三: 增加随机扰动...")
        self._add_jitter()

        # 阶段四: 生成脏污
        print("阶段四: 基于因果关联生成脏污...")
        self._generate_grime()

        print("阶段五: 基于偏好放置角色...")
        self._place_characters()

        print("--- 布局生成完毕 ---")
        return self.objects, self.suitability_map

    def _generate_suitability_map(self):
        """生成一个连续的'适宜度'地图，用于指导桌子放置"""
        s = self.settings
        width, height = s.GRID_WIDTH, s.GRID_HEIGHT

        # 1. 创建基础势场 (中心吸引)
        x = np.arange(width)
        y = np.arange(height)
        xx, yy = np.meshgrid(x, y)
        center_x, center_y = width / 2, height / 2
        distance_from_center = np.sqrt((xx - center_x) ** 2 + (yy - center_y) ** 2)
        # 距离中心越远，势能越高 (越不适宜)
        potential_base = distance_from_center ** 2 * s.CENTER_ATTRACTION_STRENGTH

        # 2. 创建噪声场
        noise = PerlinNoise(octaves=4, seed=int(time.time()))
        potential_noise = np.array([[noise([i * s.NOISE_SCALE, j * s.NOISE_SCALE]) for j in y] for i in x])

        # 3. 组合势场
        # 我们将噪声场归一化到 [0, 1] 范围，然后乘以其强度
        potential_noise_norm = (potential_noise - np.min(potential_noise)) / (np.max(potential_noise) - np.min(potential_noise))
        # 我们转置基础势场，使其形状与噪声场 (width, height) 匹配
        total_potential = potential_base.T + potential_noise_norm * s.NOISE_STRENGTH

        # 4. 转换为概率地图 [0, 1]
        # 势能越低，概率越高
        total_potential_norm = (total_potential - np.min(total_potential)) / (np.max(total_potential) - np.min(total_potential))
        self.suitability_map = 1.0 - total_potential_norm

    def _apply_repulsion(self, placed_object):
        """在一个已放置物体周围, 更新适宜度地图以产生排斥效果"""
        s = self.settings
        w, h = s.GRID_WIDTH, s.GRID_HEIGHT

        # 获取已放置物体的中心点
        obj_w, obj_h = placed_object.grid_size
        center_x = placed_object.grid_pos[0] + obj_w / 2
        center_y = placed_object.grid_pos[1] + obj_h / 2

        # 创建一个覆盖整个网格的坐标网格
        x_coords = np.arange(w)
        y_coords = np.arange(h)
        xx, yy = np.meshgrid(x_coords, y_coords)

        # 计算网格上每个点到物体中心的距离的平方
        distance_sq = (xx - center_x) ** 2 + (yy - center_y) ** 2

        # 使用高斯函数计算排斥的衰减因子
        sigma_sq = s.TABLE_REPULSION_SIGMA ** 2
        if sigma_sq > 0:
            # 高斯函数: exp(-d^2 / 2σ^2)
            gaussian_falloff = np.exp(-distance_sq / (2 * sigma_sq))

            # 创建一个乘数, 中心点为 (1 - strength), 远处为 1
            multiplier = 1.0 - s.TABLE_REPULSION_STRENGTH * gaussian_falloff

            # 将乘数应用到适宜度地图上 (注意转置以匹配形状)
            self.suitability_map *= multiplier.T

            # 确保概率值不会低于0
            np.clip(self.suitability_map, 0, 1, out=self.suitability_map)

    def _place_tables(self):
        """使用拒绝采样，一次一个地放置桌子"""
        s = self.settings
        placed_count = 0
        max_attempts = s.NUM_TABLES * 200  # 防止无限循环

        for _ in range(max_attempts):
            if placed_count >= s.NUM_TABLES:
                break

            # a. 随机选择一个候选位置
            x = random.randint(0, s.GRID_WIDTH - 1)
            y = random.randint(0, s.GRID_HEIGHT - 1)

            # b. 根据适宜度地图的概率决定是否接受
            probability = self.suitability_map[x, y]
            if random.random() > probability:
                continue  # 拒绝采样

            # c. 检查该位置是否可以放置一个桌子 (碰撞检测)
            can_place = True
            w, h = s.TABLE_SIZE
            for i in range(w):
                for j in range(h):
                    if (x + i >= s.GRID_WIDTH or
                            y + j >= s.GRID_HEIGHT or
                            self.grid[x + i, y + j] != 0):
                        can_place = False
                        break
                if not can_place:
                    break

            # d. 如果可以，放置桌子
            if can_place:
                table = GameObject("TABLE", (x, y), s.TABLE_SIZE)
                self.objects.append(table)
                for i in range(w):
                    for j in range(h):
                        self.grid[x + i, y + j] = table

                self._apply_repulsion(table)

                placed_count += 1

        if placed_count < s.NUM_TABLES:
            print(f"警告: 空间不足, 只成功放置了 {placed_count}/{s.NUM_TABLES} 张桌子。")

    def _place_chairs(self):
        """遍历所有桌子，根据规则在其周围放置椅子"""
        s = self.settings
        tables = [obj for obj in self.objects if obj.obj_type == "TABLE"]

        for table in tables:
            num_chairs = random.randint(*s.CHAIRS_PER_TABLE_RANGE)
            placed_chairs = 0

            # 确定候选位置 (桌子长边的两侧)
            tx, ty = table.grid_pos
            tw, th = table.grid_size
            candidate_spots = []
            # 上方
            for i in range(tw):
                candidate_spots.append((tx + i, ty - 1))
            # 下方
            for i in range(tw):
                candidate_spots.append((tx + i, ty + th))

            random.shuffle(candidate_spots)

            for spot in candidate_spots:
                if placed_chairs >= num_chairs:
                    break

                x, y = spot
                # 检查是否在边界内且为空
                if (0 <= x < s.GRID_WIDTH and
                        0 <= y < s.GRID_HEIGHT and
                        self.grid[x, y] == 0):
                    chair = GameObject("CHAIR", (x, y), s.CHAIR_SIZE)
                    self.objects.append(chair)
                    self.grid[x, y] = chair
                    placed_chairs += 1

    def _add_jitter(self):
        """为所有物体的视觉属性增加微小的随机扰动"""
        s = self.settings
        for obj in self.objects:
            # 只有拥有grid_pos的物体(家具)才进行抖动
            if obj.grid_pos is not None:
                offset = np.random.uniform(-s.POSITION_JITTER, s.POSITION_JITTER, 2)
                obj.visual_pos += offset
                obj.visual_angle = random.uniform(-s.ANGLE_JITTER_DEGREES, s.ANGLE_JITTER_DEGREES)

    def _generate_grime(self):
        s = self.settings
        w, h = s.GRID_WIDTH, s.GRID_HEIGHT

        # --- 1. 生成“前兆” (小脏污种子) ---
        # 首先, 创建一个基础概率图, 脏污倾向于在物体周围/下方
        activity_map = np.ones((w, h))
        for obj in self.objects:
            if obj.grid_pos is not None:
                # 在每个物体下创建一个“低活动”区域
                obj_w, obj_h = obj.grid_size
                center_x, center_y = obj.grid_pos[0] + obj_w / 2, obj.grid_pos[1] + obj_h / 2
                x_coords, y_coords = np.arange(w), np.arange(h)
                xx, yy = np.meshgrid(x_coords, y_coords)
                distance_sq = (xx - center_x) ** 2 + (yy - center_y) ** 2
                # 越靠近物体, 活动越少, 越容易脏 (用高斯函数创建影响)
                activity_map -= np.exp(-distance_sq / (2 * 2 ** 2)).T * 0.5

        # 创建 '低活动概率图', 活动越少的地方, 概率越高
        grime_base_prob = 1.0 - np.clip(activity_map, 0, 1)

        # 结合细碎噪声
        noise = PerlinNoise(octaves=6, seed=int(time.time()) + 1)
        splatter_noise = np.array([[noise([i * s.GRIME_SPLATTER_NOISE_SCALE, j * s.GRIME_SPLATTER_NOISE_SCALE]) for j in range(h)] for i in range(w)])

        splatter_noise_norm = self._safe_normalize(splatter_noise)

        # 最终的飞溅物概率图
        # 使用加权平均而不是乘法来组合概率图
        combined_map = (grime_base_prob * 0.6) + (splatter_noise_norm * 0.4)  # 60%权重给家具遮挡, 40%给随机噪声
        splatter_map = self._safe_normalize(combined_map)  # 重新归一化确保范围在[0,1]

        # 采样生成大量潜在粒子
        potential_splatters = []
        for _ in range(s.GRIME_SPLATTER_COUNT):
            x, y = random.uniform(0, w), random.uniform(0, h)
            prob = splatter_map[int(x), int(y)]
            if random.random() < prob:
                potential_splatters.append(np.array([x, y]))

        print(f"生成了 {len(potential_splatters)} 个潜在的飞溅物.")

        # --- 2. 寻找“聚集”并生成“核心” (大脏污) ---
        # 创建一个空的地图, 把潜在粒子“画”上去
        density_map = np.zeros((w, h))
        for p in potential_splatters:
            ix, iy = int(p[0]), int(p[1])
            if 0 <= ix < w and 0 <= iy < h:
                density_map[ix, iy] += 1

        # 使用高斯模糊来平滑密度图, 找到真正的“聚集区”
        blurred_density = gaussian_filter(density_map, sigma=s.GRIME_DENSITY_BLUR_SIGMA)

        if np.max(blurred_density) > 0:
            large_grime_prob_map = blurred_density / np.max(blurred_density)
        else:
            large_grime_prob_map = np.zeros((w, h))  # 避免除以0

        large_grime_patches = []
        for _ in range(s.NUM_LARGE_GRIME_PATCHES * 100):
            if len(large_grime_patches) >= s.NUM_LARGE_GRIME_PATCHES: break
            x, y = random.uniform(0, w), random.uniform(0, h)
            prob = large_grime_prob_map[int(x), int(y)]
            if random.random() < prob:
                patch = GameObject("GRIME_LARGE", None, None, visual_pos=(x, y))
                large_grime_patches.append(patch)
                self.objects.append(patch)

        # --- 3. 固化最终结果 ---
        final_splatters = []
        for p_pos in potential_splatters:
            # 计算基础存活率
            survival_prob = s.GRIME_FINAL_SURVIVAL_PROB

            # 检查是否在大核心影响范围内
            for core in large_grime_patches:
                dist = np.linalg.norm(p_pos - core.visual_pos)
                if dist < s.GRIME_CORE_INFLUENCE_RADIUS:
                    # 距离越近, 存活率越高
                    survival_prob += (1.0 - survival_prob) * (1.0 - dist / s.GRIME_CORE_INFLUENCE_RADIUS)

            if random.random() < survival_prob:
                splatter = GameObject("GRIME_SMALL", None, None, visual_pos=p_pos)
                final_splatters.append(splatter)
                self.objects.append(splatter)

    def _place_characters(self):
        s = self.settings
        w, h = s.GRID_WIDTH, s.GRID_HEIGHT

        # 1. 创建基础“社交地图”
        social_map = np.zeros((w, h))
        furniture = [obj for obj in self.objects if obj.obj_type in ["TABLE", "CHAIR"]]
        x_coords, y_coords = np.arange(w), np.arange(h)
        xx, yy = np.meshgrid(x_coords, y_coords)

        for obj in furniture:
            obj_w, obj_h = obj.grid_size
            center_x, center_y = obj.grid_pos[0] + obj_w / 2, obj.grid_pos[1] + obj_h / 2
            distance_sq = (xx - center_x) ** 2 + (yy - center_y) ** 2
            sigma_sq = s.SOCIAL_ATTRACTION_SIGMA ** 2
            if sigma_sq > 0:
                social_map += np.exp(-distance_sq / (2 * sigma_sq)).T

        social_map_norm = self._safe_normalize(social_map) * s.SOCIAL_ATTRACTION_STRENGTH

        # 2. 创建“脏污影响图”
        grime_map = np.zeros((w, h))
        grime_objects = [obj for obj in self.objects if "GRIME" in obj.obj_type]
        for grime in grime_objects:
            center_x, center_y = grime.visual_pos
            distance_sq = (xx - center_x) ** 2 + (yy - center_y) ** 2

            # 大脏污的影响更大
            is_large = "LARGE" in grime.obj_type
            sigma = 2.0 if is_large else 1.0
            strength = 1.0 * (s.ELF_LARGE_GRIME_MULTIPLIER if is_large else 1.0)

            sigma_sq = sigma ** 2
            if sigma_sq > 0:
                grime_map += np.exp(-distance_sq / (2 * sigma_sq)).T * strength

        grime_map_norm = self._safe_normalize(grime_map)

        # 3. 为每种角色生成舒适度图并放置
        character_configs = {
            "ELF": (s.NUM_ELVES, social_map_norm + grime_map_norm * s.ELF_GRIME_REPULSION),
            "DWARF": (s.NUM_DWARVES, social_map_norm),  # 矮人只关心社交
            "MUSHROOM_PERSON": (s.NUM_MUSHROOM_PEOPLE, social_map_norm + grime_map_norm * s.MUSHROOM_GRIME_ATTRACTION)
        }

        for char_type, (num_chars, comfort_map) in character_configs.items():
            # 确保舒适度不为负, 并归一化为概率
            comfort_map_clipped = np.clip(comfort_map, 0, np.max(comfort_map))
            prob_map = self._safe_normalize(comfort_map_clipped)

            self.debug_comfort_maps[char_type] = prob_map  # 保存以供调试

            placed_count = 0
            # 增加一个简单的占用网格, 防止角色生成在同一点
            char_grid = set()
            for _ in range(num_chars * 100):
                if placed_count >= num_chars: break

                x, y = random.uniform(0, w), random.uniform(0, h)
                ix, iy = int(x), int(y)

                if 0 <= ix < w and 0 <= iy < h and (ix, iy) not in char_grid:
                    if random.random() < prob_map[ix, iy]:
                        # 检查是否生成在家具上
                        if self.grid[ix, iy] == 0:
                            char = GameObject(char_type, None, None, visual_pos=(x, y))
                            self.objects.append(char)
                            char_grid.add((ix, iy))
                            placed_count += 1
            print(f"成功放置了 {placed_count}/{num_chars} 个 {char_type}.")

    def export_to_json(self, filename="layout.json"):
        """将所有生成的游戏对象导出为前端可以使用的JSON文件。"""
        s = self.settings

        meta_data = {
            "gridWidth": s.GRID_WIDTH,
            "gridHeight": s.GRID_HEIGHT
        }

        objects_data = []
        for obj in self.objects:
            obj_dict = {
                "obj_type": obj.obj_type,
                "grid_pos": obj.grid_pos.tolist(),
                "visual_pos": obj.visual_pos.tolist(),
                "visual_angle": obj.visual_angle
            }

            # grid_size 仍然是可选的
            if obj.grid_size is not None:
                obj_dict["grid_size"] = obj.grid_size.tolist()

            objects_data.append(obj_dict)

        output_data = {"meta": meta_data, "objects": objects_data}

        try:
            with open(filename, 'w', encoding='utf-8') as f:
                json.dump(output_data, f, indent=2)  # indent=2 is more common
            print(f"--- 统一化布局成功导出到文件: {filename} ---")
        except Exception as e:
            print(f"!!! 导出到JSON时发生错误: {e} !!!")


# =============================================================================
# 4. 可视化 (Visualization)
# =============================================================================
class Visualizer:
    def __init__(self, settings):
        self.settings = settings

    def plot(self, generator):
        objects = generator.objects
        suitability_map = generator.suitability_map
        comfort_maps = generator.debug_comfort_maps
        s = self.settings

        # 创建一个更复杂的图表布局
        fig = plt.figure(figsize=(20, 10))
        gs = fig.add_gridspec(2, 3)

        ax_main = fig.add_subplot(gs[:, 1:])
        ax_elf = fig.add_subplot(gs[0, 0])
        ax_dwarf = fig.add_subplot(gs[1, 0])

        # --- 主布局图 ---
        ax_main.set_title("Final Generated Layout")
        # (绘制网格和家具的代码)
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

        # 绘制所有对象
        self._draw_objects(ax_main, objects)

        # --- 舒适度调试图 ---
        if "ELF" in comfort_maps:
            ax_elf.set_title("Elf Comfort Map")
            im = ax_elf.imshow(comfort_maps["ELF"].T, cmap='viridis', origin='lower')
            ax_elf.set_xticklabels([])
            ax_elf.set_yticklabels([])

        if "DWARF" in comfort_maps:
            ax_dwarf.set_title("Dwarf Comfort Map (Social Only)")
            im = ax_dwarf.imshow(comfort_maps["DWARF"].T, cmap='viridis', origin='lower')
            ax_dwarf.set_xticklabels([])
            ax_dwarf.set_yticklabels([])

        plt.tight_layout()
        plt.show()

    def _draw_objects(self, ax, objects):
        s = self.settings
        # 绘制家具
        for obj in objects:
            if obj.grid_pos is not None:
                w, h = obj.grid_size
                vx, vy = obj.visual_pos
                rect = patches.Rectangle((vx, vy), w, h, linewidth=1, edgecolor='black', facecolor=s.COLORS.get(obj.obj_type), angle=obj.visual_angle, rotation_point='center', zorder=10)
                ax.add_patch(rect)

         # 绘制脏污
        for obj in objects:
            if "GRIME" in obj.obj_type:
                ax.scatter(obj.visual_pos[0], obj.visual_pos[1], s=obj.visual_size*200, c=s.COLORS.get(obj.obj_type), alpha=0.6, marker='o', edgecolors='none', zorder=0)

        # 最后绘制角色
        for obj in objects:
            if obj.obj_type in ["ELF", "DWARF", "MUSHROOM_PERSON"]:
                ax.scatter(obj.visual_pos[0], obj.visual_pos[1], s=obj.visual_size*100, c=s.COLORS.get(obj.obj_type), alpha=1.0, marker='o', edgecolors='black', linewidth=1.5, zorder=20)
                # 添加首字母标签
                ax.text(obj.visual_pos[0], obj.visual_pos[1], obj.obj_type[0], ha='center', va='center', color='white', fontsize=8, weight='bold')


# =============================================================================
# 5. 主程序入口 (Main Execution)
# =============================================================================
if __name__ == "__main__":
    settings = Settings()
    generator = LayoutGenerator(settings)
    generator.generate_layout()
    generator.export_to_json("layout.json")
    visualizer = Visualizer(settings)
    visualizer.plot(generator)