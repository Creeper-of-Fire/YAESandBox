import {computed, markRaw, readonly, type Ref, ref} from "vue";
import {defineStore} from "pinia";
import type {IScopedStorage} from "../storage/IScopedStorage.ts";
import type {SaveSlot} from "../storage/ISaveSlotManager.ts";
import {createScopedPersistentState} from "./createScopedPersistentState.ts";

export interface IPersistentState<T>
{
    state: Ref<T>;
    isReady: Readonly<Ref<boolean>>;
}


// 定义初始化所需的依赖项接口
export interface SaveStoreDependencies
{
    scopedStorage: IScopedStorage;
    activeSlot: Readonly<Ref<SaveSlot | null>>;
}

/**
 * 【抽象契约】定义了一个能够创建与当前存档槽绑定的持久化状态的工厂。
 * 任何 Pinia Store 或其他状态管理器只要实现了这个接口，就可以被我们的系统使用。
 */
export interface IScopedStateFactory
{
    readonly isInitialized: Readonly<Ref<boolean>>;

    initialize(dependencies: SaveStoreDependencies): void;

    createState<T>(fileName: string, initialState: T): IPersistentState<T>;
}


/**
 * 【通用工厂】创建一个 Pinia Store 定义，该 Store 实现了 IScopedStateFactory 接口。
 * @param storeId - 为这个 Store 指定一个唯一的 ID，例如 'era-lite-save-store'。
 * @returns 一个标准的 Pinia Store 定义 (StoreDefinition)。
 */
export function createScopedSaveStoreFactory(storeId: string)
{
    return defineStore(storeId, () =>
    {
        // --- 内部状态：被动等待注入 ---
        const isInitialized = ref(false);
        let scopedStorage: IScopedStorage | null = null;
        let activeSlot: Readonly<Ref<SaveSlot | null>> | null = null;

        function initialize(dependencies: SaveStoreDependencies)
        {
            if (isInitialized.value)
            {
                console.warn(`Pinia store with id "${storeId}" has already been initialized.`);
                return;
            }
            scopedStorage = markRaw(dependencies.scopedStorage);
            activeSlot = dependencies.activeSlot;
            isInitialized.value = true;
        }

        function createState<T>(fileName: string, initialState: T)
        {
            if (!isInitialized.value || !scopedStorage || !activeSlot)
            {
                throw new Error(
                    `Store "${storeId}": createState() was called before initialization.`
                );
            }
            return createScopedPersistentState<T>(fileName, {
                initialState,
                scopedStorage: scopedStorage,
                activeSlot: activeSlot,
            });
        }

        const asScopedStateFactory = computed((): IScopedStateFactory => ({
            isInitialized: readonly(isInitialized), // 在对象内部，isInitialized 保持 Ref<boolean> 形态
            initialize,
            createState,
        }));

        return {
            isInitialized: readonly(isInitialized),
            initialize,
            createState,

            // 提供一个符合 DI 接口的“适配器”属性
            asScopedStateFactory,
        };
    });
}
