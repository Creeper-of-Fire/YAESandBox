import {defineStore} from 'pinia';
import {computed, type Ref, ref, watch} from 'vue';
import {GameMap} from '#/game-logic/GameMap.ts';
import {LogicalObjectLayer} from "#/game-logic/entity/gameObject/render/LogicalObjectLayer.ts";
import {useEraMapSaveStore} from "#/saves/useEraMapSaveStore.ts";
import { serializationService } from '#/services/SerializationService.ts';

const WORLD_STATE_STORAGE_KEY = 'world-state';
export const useWorldStateStore = defineStore(WORLD_STATE_STORAGE_KEY, () =>
{
    // --- State ---
    const logicalGameMap: Ref<GameMap | null> = ref(null);
    const error = ref<string | null>(null);

    // --- Getter ---
    const allObjects = computed(() =>
    {
        if (!logicalGameMap.value) return [];
        const objectLayer = logicalGameMap.value.layers.find(l => l instanceof LogicalObjectLayer);
        return objectLayer ? (objectLayer as LogicalObjectLayer).objects : [];
    });

    // --- Save Store Integration ---
    const saveStore = useEraMapSaveStore();
    // 创建一个响应式的、能被自动保存的状态
    const {state: savedPlainWorld, isReady} = saveStore.createState<Record<string, any> | null>(WORLD_STATE_STORAGE_KEY, null);

    // --- Actions ---

    /**
     * 【核心】用一个已存在的 GameMap 实例来设置当前世界状态。
     * 这是唯一修改 logicalGameMap 的入口。
     * @param gameMapInstance - 一个完整的 GameMap 实例。
     */
    function setWorldState(gameMapInstance: GameMap) {
        logicalGameMap.value = gameMapInstance;
        error.value = null;
        console.log("World state has been set.", logicalGameMap.value);
    }

    /**
     * 清理世界状态，用于退出存档等场景。
     */
    function clearWorldState() {
        logicalGameMap.value = null;
        error.value = null;
        savedPlainWorld.value = null; // 同时清空存档数据
        console.log("World state has been cleared.");
    }

    /**
     * 【水合】从纯对象数据加载世界，将其转换为类实例后设置状态。
     * @returns {boolean} - 返回 true 表示成功，false 表示失败。
     */
    function hydrateFromSave(plainData: Record<string, any>): boolean { // <-- 返回布尔值
        try {
            const gameMapInstance = serializationService.hydrate(plainData);
            setWorldState(gameMapInstance);
            console.log("World state hydrated successfully from saved data.");
            return true; // <-- 成功时返回 true
        } catch (e) {
            console.error("Failed to hydrate world state from saved data:", e);
            error.value = `加载存档失败: ${(e as Error).message}`;
            logicalGameMap.value = null; // 清理可能存在的半成品状态
            return false; // <-- 失败时返回 false
        }
    }

    /**
     * 【脱水】将当前世界状态转换为纯对象以供保存。
     */
    function dehydrateForSave()
    {
        if (logicalGameMap.value)
        {
            savedPlainWorld.value = serializationService.dehydrate(logicalGameMap.value);
            console.log("World state dehydrated for saving.");
        }
    }

    watch(logicalGameMap, dehydrateForSave, { deep: true });

    /**
     * 将AI提案应用到指定对象上。
     * 它只接收最终计算好的、完整的属性对象，并直接替换掉旧的。
     * @param payload - 包含目标对象ID和要应用的【全新】属性对象。
     */
    function applyProposal(payload: {
        targetObjectId: string;
        newProperties: Record<string, any>;
    })
    {
        if (!logicalGameMap.value) {
            console.error("Cannot apply proposal: World state is not loaded.");
            return;
        }

        const targetObject = logicalGameMap.value.findObjectById(payload.targetObjectId);

        if (targetObject) {
            // 直接、完整地替换 `properties`
            // 这保证了数据流的单向性和可预测性。
            targetObject.properties = payload.newProperties;
            console.log(`Properties for object ${payload.targetObjectId} have been updated.`, targetObject);
        } else {
            console.error(`Cannot apply proposal: Object with ID ${payload.targetObjectId} not found.`);
        }
    }

    return {
        // State
        logicalGameMap,
        error,
        // Save System Related
        isSaveSystemReady: isReady,
        savedPlainWorld,
        // Getter
        allObjects,
        // Actions
        setWorldState,
        clearWorldState,

        hydrateFromSave,
        applyProposal,
    };
});