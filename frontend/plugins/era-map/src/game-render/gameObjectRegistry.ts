import {type GameObjectConfig, RenderType} from './types';
import {kenney_roguelike_indoors, kenney_roguelike_rpg_pack, kenney_ting_dungeon} from './tilesetRegistry'
import {TILE_SIZE} from '../constant';

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

// 对象配置注册表
export const gameObjectRegistry: Record<string, GameObjectConfig> = {
    // --- 家具 (绑定到网格, 使用形状渲染) ---
    'TABLE': {
        gridSize: {width: 2, height: 1},
        // renderConfig: {
        //     renderType: RenderType.SHAPE,
        //     shape: 'rect',
        //     fill: COLORS.TABLE,
        //     stroke: '#000',
        // }
        renderConfig: {
            renderType: RenderType.SPRITE,
            components: [
                {
                    tilesetId: kenney_roguelike_indoors.id,
                    tileId: kenney_roguelike_indoors.tileItem.TILE_ID_TABLE_HEAD,
                    offset: {x: 0, y: 0}
                },
                {
                    tilesetId: kenney_roguelike_indoors.id,
                    tileId: kenney_roguelike_indoors.tileItem.TILE_ID_TABLE_FOOT,
                    offset: {x: 1, y: 0}
                }
            ]
        }
    },
    'CHAIR': {
        gridSize: {width: 1, height: 1},
        renderConfig: {
            renderType: RenderType.SPRITE,
            components: [
                {
                    tilesetId: kenney_roguelike_indoors.id,
                    tileId: kenney_roguelike_indoors.tileItem.big_chair,
                    offset: {x: 0, y: 0}
                },
            ]
        }
    },

    'TORCH': {
        gridSize: {width: 1, height: 1},
        renderConfig: {
            renderType: RenderType.SPRITE,
            components: [
                {
                    tilesetId: kenney_roguelike_rpg_pack.id,
                    tileId: kenney_roguelike_rpg_pack.tileItem.TILE_ID_TORCH,
                    offset: {x: 0, y: 0}
                },
            ]
        }
    },

    'WINDOW': {
        gridSize: {width: 1, height: 1},
        renderConfig: {
            renderType: RenderType.SPRITE,
            components: [
                {
                    tilesetId: kenney_roguelike_rpg_pack.id,
                    tileId: kenney_roguelike_rpg_pack.tileItem.wooden_window,
                    offset: {x: 0, y: 0}
                },
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

    // --- 角色 (独立物体, 可以用形状或图片) ---
    'ELF': {
        renderConfig: {
            renderType: RenderType.SPRITE,
            components: [
                {
                    tilesetId: kenney_ting_dungeon.id,
                    tileId: kenney_ting_dungeon.tileItem.elf,
                    offset: {x: 0, y: 0}
                }
            ]
        }
    },
    'DWARF': {
        // 其他角色先用形状代替
        renderConfig: {
            renderType: RenderType.SPRITE,
            components: [
                {
                    tilesetId: kenney_ting_dungeon.id,
                    tileId: kenney_ting_dungeon.tileItem.dwarf,
                    offset: {x: 0, y: 0}
                }
            ]
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