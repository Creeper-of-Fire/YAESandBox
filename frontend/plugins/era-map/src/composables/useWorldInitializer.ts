import { useWorldStateStore } from '#/stores/useWorldStateStore';
import { registry } from "#/game-resource/tilesetRegistry.ts";
// @ts-ignore
import initLayoutJson from '#/assets/init_layout.json';
import type { FullLayoutData } from '#/game-resource/types';

/**
 * 负责初始化整个应用核心世界状态的 Composable。
 * 它保证了资源加载和状态初始化的原子性。
 */
export function useWorldInitializer() {
    const worldState = useWorldStateStore();

    async function initialize() {
        if (worldState.isLoaded) {
            console.log("World state is already loaded. Skipping initialization.");
            return;
        }

        console.log("Starting world initialization...");
        try {
            // 1. 确保核心资源 (如图集) 已加载
            await registry();

            // 2. 从骨架文件加载初始世界状态
            // TODO: 未来这里将是加载存档的入口
            // 逻辑会变成: if (hasSaveData) { hydrateFromSave(); } else { loadFromSkeleton(); }
            worldState.loadInitialState(initLayoutJson as FullLayoutData);

            console.log("World initialization complete.");
        } catch (error) {
            console.error("Failed during world initialization:", error);
            // 这里可以设置一个全局的错误状态
        }
    }

    // 也可以返回一个卸载/清理函数
    function reset() {
        // ... 清理 worldState store 的逻辑
        console.log("World state has been reset.");
    }

    return {
        initialize,
        reset,
        isLoaded: worldState.isLoaded, // 透传加载状态
    };
}