import type { RawGameObjectData } from './types';
import { GameObject } from './GameObject';
import { gameObjectRegistry } from './gameObjectRegistry';

export function createGameObject(data: RawGameObjectData): GameObject | null {
    const config = gameObjectRegistry[data.obj_type];
    if (!config) {
        console.warn(`No configuration found for object type: "${data.obj_type}". Skipping.`);
        return null;
    }
    return new GameObject(data, config);
}