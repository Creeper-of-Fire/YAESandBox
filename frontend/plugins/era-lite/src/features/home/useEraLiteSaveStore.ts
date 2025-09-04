// src/stores/useEraLiteGlobalScopeStore.ts
import {defineStore} from 'pinia';
import {markRaw, readonly, type Ref, ref} from "vue";
import type {IScopedStorage, SaveSlot} from "@yaesandbox-frontend/core-services/playerSave";
import {createScopedPersistentState} from "@yaesandbox-frontend/core-services/playerSave";

// 定义初始化所需的依赖项接口
interface SaveStoreDependencies
{
    scopedStorage: IScopedStorage;
    activeSlot: Readonly<Ref<SaveSlot | null>>;
}

export const useEraLiteSaveStore = defineStore('era-lite-save-store', () =>
{
    // --- 内部状态：被动等待注入 ---
    const isInitialized = ref(false);
    let scopedStorage: IScopedStorage | null = null;
    let activeSlot: Readonly<Ref<SaveSlot | null>> | null = null;

    /**
     * 【核心】初始化方法。
     * 这个方法必须由一个处于 Vue setup 上下文中的 Composable (即 useEraLiteGameMenu) 调用。
     * 它负责将上下文相关的服务和状态注入到这个 Store 中。
     */
    function initialize(dependencies: SaveStoreDependencies)
    {
        if (isInitialized.value)
        {
            console.warn("useEraLiteSaveStore has already been initialized. Re-initializing is not allowed.");
            return;
        }

        // 使用 markRaw 避免 Vue 对复杂的 service 对象进行不必要的响应式代理
        scopedStorage = markRaw(dependencies.scopedStorage);
        activeSlot = dependencies.activeSlot;

        isInitialized.value = true;
        console.log("useEraLiteSaveStore initialized successfully.");
    }

    /**
     * 创建一个预配置的、与当前存档槽绑定的持久化状态。
     * 只有在 Store 被 initialize 之后才能调用。
     */
    function createState<T>(fileName: string, initialState: T)
    {
        if (!isInitialized.value || !scopedStorage || !activeSlot)
        {
            throw new Error(
                `useEraLiteSaveStore.createState() was called before the store was initialized. ` +
                `Ensure that useEraLiteGameMenu() is called in a parent component's setup.`
            );
        }

        return createScopedPersistentState<T>(fileName, {
            initialState,
            scopedStorage: scopedStorage, // 使用注入的服务
            activeSlot: activeSlot,       // 使用注入的响应式状态
        });
    }

    return {
        isInitialized: readonly(isInitialized),
        initialize,
        createState,
    };
});