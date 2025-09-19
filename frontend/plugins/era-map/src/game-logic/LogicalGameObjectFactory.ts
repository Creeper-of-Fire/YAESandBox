import type { RawGameObjectData } from '#/game-render/types';
import { gameObjectRegistry } from '#/game-render/gameObjectRegistry';
import { GameObjectRender } from '#/game-render/GameObjectRender';
import { LogicalGameObject } from './LogicalGameObject';
import { v4 as uuidv4 } from 'uuid';

/**
 * 从原始JSON数据创建一个完整的 LogicalGameObject 实例。
 * 这个函数是连接初始数据和我们丰富逻辑模型的桥梁。
 * @param data - 来自 init_layout.json 的单个对象数据。
 * @returns 一个新的 LogicalGameObject 实例，或在配置不存在时返回 null。
 */
export function createLogicalGameObject(data: RawGameObjectData): LogicalGameObject | null {
    const config = gameObjectRegistry[data.obj_type];
    if (!config) {
        console.warn(`No configuration found for object type: "${data.obj_type}". Skipping.`);
        return null;
    }

    // 1. 为新实体生成一个唯一的、持久的ID
    const newId = uuidv4();

    // 2. 创建其对应的渲染数据载体 (GameObjectRender)
    const renderInfo = new GameObjectRender(newId, data, config);

    // 3. 创建逻辑实体 (LogicalGameObject)，并将渲染载体注入其中
    const logicalObject = new LogicalGameObject(newId, data.obj_type, renderInfo);

    return logicalObject;
}