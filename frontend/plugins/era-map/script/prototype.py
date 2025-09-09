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
    GRIME_SPLATTER_COUNT = 5000  # 小脏污的数量
    GRIME_SPLATTER_NOISE_SCALE = 0.5  # 飞溅物噪声更细碎

    # 第2遍: 大脏污核心
    GRIME_DENSITY_BLUR_SIGMA = 1.5  # 计算密度的模糊半径
    NUM_LARGE_GRIME_PATCHES = 5  # 生成的大块脏污核心数量

    # 第3遍: 最终固化
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

    # --- 光源 ---
    NUM_WINDOWS = 10
    WINDOW_SIZE = (1, 1) # 假设窗户是1x1
    NUM_TORCHES = 15
    TORCH_SIZE = (1, 1)

    # --- 可视化 ---
    COLORS = {
        "TABLE": "#8B4513",  # 棕色
        "CHAIR": "#D2691E",  # 巧克力色
        "GRIME_LARGE": "#556B2F",  # 暗橄榄绿
        "GRIME_SMALL": "#6B8E23",  # 橄榄褐
        "ELF": "#98FB98",  # 淡绿色
        "DWARF": "#F4A460",  # 沙棕色
        "MUSHROOM_PERSON": "#DC143C",  # 猩红色
        "WINDOW": "#87CEEB",  # 天蓝色
        "TORCH": "#FFA500",   # 橙色
    }