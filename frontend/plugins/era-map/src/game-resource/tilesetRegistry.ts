import {resourceManager} from "#/game-resource/ResourceManager.ts";
// @ts-ignore
import tilesetAssetUrl from "#/assets/tilemap.png";
// @ts-ignore
import rpgTilesetUrl from "#/assets/roguelikeSheet_transparent.png";
// @ts-ignore
import rpgTilesetIndoorUrl from "#/assets/roguelikeIndoor_transparent.png";

const TILESET_ID_TERRAIN = 'terrain_main';
const TILESET_ID_THING_A = 'rpg_thing_a'
const TILESET_ID_CHARACTERS = 'characters';
const TILESET_ID_INDOOR = 'kenney_roguelike-indoors';

export const kenney_ting_dungeon = {
    id: TILESET_ID_TERRAIN,
    url: tilesetAssetUrl,
    sourceTileSize: 16,
    columns: 12,
    spacing: 1,
    tileItem: {
        chair:73,
        elf: 99,
        dwarf:87,
    }
}

export const kenney_roguelike_rpg_pack = {
    id: TILESET_ID_THING_A,
    url: rpgTilesetUrl,
    sourceTileSize: 16,
    columns: 57,
    spacing: 1,
    tileItem: {
        TILE_ID_TORCH: 416,
        wooden_window: 217,
        stone_floor: 120,
        wooden_floor: 235,
        stone_wall: 887,
        wooden_wall: 889,
    }
}

export const kenney_roguelike_indoors = {
    id: TILESET_ID_INDOOR,
    url: rpgTilesetIndoorUrl,
    sourceTileSize: 16,
    columns: 27,
    spacing: 1,
    tileItem: {
        big_chair:98,
        TILE_ID_TABLE_HEAD: 243,
        TILE_ID_TABLE_FOOT: 245,
    }
}


export async function registry()
{
    await resourceManager.loadTileset(kenney_ting_dungeon);
    await resourceManager.loadTileset(kenney_roguelike_rpg_pack);
    await resourceManager.loadTileset(kenney_roguelike_indoors);
}