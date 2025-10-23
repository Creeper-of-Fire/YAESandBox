// src/saves/useComponentSaveStore.ts

import {
    createAndProvideApiGameSaveService,
    createScopedSaveStoreFactory,
    type IGameSaveService
} from "@yaesandbox-frontend/core-services/player-save";
import {useProjectUniqueName} from "@yaesandbox-frontend/core-services/inject-key";

// 1. 为编辑器的 Pinia save store 定义一个唯一的 key
export const useComponentSaveStore = createScopedSaveStoreFactory('component-editor-save-store');

/**
 * 【组件编辑器创建器】
 * 这是一个简化的工厂函数，负责为组件编辑器组装并提供单例的存档服务。
 */
export function createAndProvideComponentSaveService(): IGameSaveService
{
    // 获取当前插件的唯一名称，用于隔离不同插件的存档
    const projectUniqueName = useProjectUniqueName()

    // 获取我们为编辑器定义的 Pinia store
    const saveStore = useComponentSaveStore();

    // 创建并 provide 存档服务实例
    return createAndProvideApiGameSaveService({
        uniqueName: projectUniqueName,
        stateFactory: saveStore.asScopedStateFactory,
    });
}