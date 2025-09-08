import {type GameObjectConfig, RenderType} from './types';
// 全局的TILE_SIZE，现在从constant.ts导入
import {TILE_SIZE} from '../constant';
// 定义图集ID常量，避免魔法字符串
// TODO 之后可能改为配置项
const TILESET_ID_TERRAIN = 'terrain_main';
const TILESET_ID_CHARACTERS = 'characters';

// 从Python脚本的颜色配置中获取灵感
const COLORS = {
    TABLE: "#8B4513",
    CHAIR: "#D2691E",
    GRIME_LARGE: "#556B2F",
    GRIME_SMALL: "#6B8E23",
    ELF: "#98FB98",
    DWARF: "#F4A460",
    MUSHROOM_PERSON: "#DC143C",
};

// 图集中的瓦片ID
const TILE_ID_ELF = 99;
const TILE_ID_BED_HEAD = 80; // 假设床头是ID 80
const TILE_ID_BED_FOOT = 81; // 假设床脚是ID 81

// 对象配置注册表
export const gameObjectRegistry: Record<string, GameObjectConfig> = {
    // --- 家具 (绑定到网格, 使用形状渲染) ---
    'TABLE': {
        gridSize: {width: 2, height: 1},
        renderConfig: {
            renderType: RenderType.SHAPE,
            shape: 'rect',
            fill: COLORS.TABLE,
            stroke: '#000',
        }
    },
    'CHAIR': {
        gridSize: {width: 1, height: 1},
        renderConfig: {
            renderType: RenderType.SHAPE,
            shape: 'rect',
            fill: COLORS.CHAIR,
            stroke: '#000',
        }
    },

    'BED': {
        // 这个物体的逻辑边界是 2x1
        gridSize: {width: 2, height: 1},
        renderConfig: {
            renderType: RenderType.SPRITE,
            components: [
                // 第一个瓦片：床头，在物体的局部坐标 (0,0)
                {tilesetId: TILESET_ID_TERRAIN, tileId: TILE_ID_BED_HEAD, offset: {x: 0, y: 0}},
                // 第二个瓦片：床脚，在物体的局部坐标 (1,0)
                {tilesetId: TILESET_ID_TERRAIN, tileId: TILE_ID_BED_FOOT, offset: {x: 1, y: 0}}
            ]
        }
    },

    // --- 脏污 (独立物体, 使用形状渲染) ---
    'GRIME_LARGE': {
        renderConfig: {
            renderType: RenderType.SHAPE,
            shape: 'circle',
            radius: TILE_SIZE * 0.8, // 大脏污半径
            fill: COLORS.GRIME_LARGE,
        }
    },
    'GRIME_SMALL': {
        renderConfig: {
            renderType: RenderType.SHAPE,
            shape: 'circle',
            radius: TILE_SIZE * 0.15, // 小脏污半径
            fill: COLORS.GRIME_SMALL,
        }
    },

    // --- 角色 (独立物体, 可以用形状或图片) ---
    'ELF': {
        // 我们用图片来渲染精灵，展示系统的灵活性
        renderConfig: {
            renderType: RenderType.SPRITE,
            components: [
                {tilesetId: TILESET_ID_TERRAIN, tileId: TILE_ID_ELF, offset: {x: 0, y: 0}}
            ]
        }
    },
    'DWARF': {
        // 其他角色先用形状代替
        renderConfig: {
            renderType: RenderType.SHAPE,
            shape: 'circle',
            radius: TILE_SIZE * 0.4,
            fill: COLORS.DWARF,
            stroke: 'black',
        }
    },
    'MUSHROOM_PERSON': {
        renderConfig: {
            renderType: RenderType.SHAPE,
            shape: 'circle',
            radius: TILE_SIZE * 0.4,
            fill: COLORS.MUSHROOM_PERSON,
            stroke: 'black',
        }
    },
};