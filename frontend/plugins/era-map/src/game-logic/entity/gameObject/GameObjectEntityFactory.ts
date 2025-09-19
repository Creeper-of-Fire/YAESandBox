import type {RawGameObjectData} from '#/worldGeneration/types.ts';
import {gameObjectRegistry} from '#/game-logic/entity/gameObject/render/gameObjectRegistry.ts';
import {GameObjectRender} from '#/game-logic/entity/gameObject/render/GameObjectRender.ts';
import {GameObjectEntity} from './GameObjectEntity.ts';
import {v4 as uuidv4} from 'uuid';

/**
 * 从原始JSON数据创建一个完整的 ObjectEntity 实例。
 * 这个函数是连接初始数据和我们丰富逻辑模型的桥梁。
 * @param data - 来自 init_layout.json 的单个对象数据。
 * @returns 一个新的 ObjectEntity 实例，或在配置不存在时返回 null。
 */
export function createGameObjectEntity(data: RawGameObjectData): GameObjectEntity | null {
    const config = gameObjectRegistry[data.obj_type];
    if (!config) {
        console.warn(`No configuration found for object type: "${data.obj_type}". Skipping.`);
        return null;
    }

    // 1. 为新实体生成一个唯一的、持久的ID
    const newId = uuidv4();

    // 2. 创建其对应的渲染数据载体 (GameObjectRender)
    const renderInfo = new GameObjectRender(newId, data, config);

    // 3. 创建逻辑实体 (GameObjectEntity)，并将渲染载体注入其中
    return new GameObjectEntity(newId, data.obj_type, renderInfo);
}