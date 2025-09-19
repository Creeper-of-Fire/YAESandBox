import type { RawGameObjectData } from './types';
import { GameObjectRender } from './GameObjectRender.ts';
import { gameObjectRegistry } from './gameObjectRegistry';
import { v4 as uuidv4 } from 'uuid';

export function createGameObject(data: RawGameObjectData): GameObjectRender | null {
    const config = gameObjectRegistry[data.obj_type];
    if (!config) {
        console.warn(`No configuration found for object type: "${data.obj_type}". Skipping.`);
        return null;
    }
    const newId = uuidv4();
    return new GameObjectRender(newId,data, config);
}