import { instanceToPlain, plainToInstance } from 'class-transformer';
import { GameMap } from '#/game-logic/GameMap.ts';

/**
 * 封装了 class-transformer 的核心功能，用于世界状态的序列化与水合。
 */
class SerializationService {

    /**
     * 将 GameMap 的类实例“脱水”为可存储的纯 JavaScript 对象。
     * @param gameMapInstance - GameMap 类的实例。
     * @returns 一个可以被 JSON.stringify 的纯对象。
     */
    public dehydrate(gameMapInstance: GameMap): Record<string, any> {
        return instanceToPlain(gameMapInstance, {
            excludeExtraneousValues: true, // 确保只序列化有 @Expose() 标记的属性
        });
    }

    /**
     * 将纯 JavaScript 对象“水合”为带有方法和原型的 GameMap 类实例。
     * @param plainObject - 从存档中读取的纯对象。
     * @returns 一个功能完整的 GameMap 实例。
     */
    public hydrate(plainObject: Record<string, any>): GameMap {
        return plainToInstance(GameMap, plainObject, {
            excludeExtraneousValues: true,
        });
    }
}

export const serializationService = new SerializationService();