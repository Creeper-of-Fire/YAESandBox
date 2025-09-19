import {useWorldStateStore} from '#/stores/useWorldStateStore';
import {registry} from "#/game-resource/tilesetRegistry.ts";
// @ts-ignore
import initLayoutJson from '#/assets/init_layout.json';
import {watch} from "vue";
import {worldGenerationService} from "#/worldGeneration/WorldGenerationService.ts";

/**
 * 负责初始化整个应用核心世界状态的 Composable。
 * 它保证了资源加载和状态初始化的原子性。
 */
export function useWorldInitializer()
{
    const worldState = useWorldStateStore();

    async function initialize()
    {
        // 确保资源已加载
        await registry();

        // 等待存档系统就绪
        await new Promise<void>(resolve =>
        {
            if (worldState.isSaveSystemReady)
            {
                resolve();
            }
            else
            {
                const unwatch = watch(() => worldState.isSaveSystemReady, (ready) =>
                {
                    if (ready)
                    {
                        unwatch();
                        resolve();
                    }
                });
            }
        });

        // 决定是加载旧档还是创建新档
        if (worldState.savedPlainWorld) {
            console.log("Found existing save data, attempting to hydrate...");

            // 尝试水合
            const success = worldState.hydrateFromSave(worldState.savedPlainWorld);

            // 如果水合失败，执行回退策略
            if (!success) {
                console.warn("Hydration failed. Discarding corrupted save and creating a new world.");

                // 【可选但推荐】通知用户
                // 你可以在这里使用 Naive UI 的 notification 或 dialog
                // window.$notification.error({
                //   title: '存档加载失败',
                //   content: '你的存档文件可能已损坏，将为你创建一个新的世界。',
                //   duration: 5000
                // });

                // 清理掉损坏的存档数据
                worldState.clearWorldState(); // 这会把 store 里的 savedPlainWorld 设为 null

                // 然后像没有存档一样，创建一个新世界
                createNewWorld();
            }
        } else {
            // 分支 B: 无存档，执行“创世”流程
            console.log("No save data found, creating a new world from initial layout...");
            createNewWorld();
        }
    }

    /**
     * 将创建新世界的逻辑提取到一个辅助函数中，以便复用。
     */
    function createNewWorld() {
        try {
            const newGameMap = worldGenerationService.createFromInitialLayout(initLayoutJson);
            worldState.setWorldState(newGameMap);
            console.log("A new world has been successfully created.");
        } catch(e) {
            console.error("Fatal error: Failed even to create a new world.", e);
            worldState.error = `创建新世界时发生致命错误: ${(e as Error).message}`;
        }
    }

    // 也可以返回一个卸载/清理函数
    function reset()
    {
        // ... 清理 worldState store 的逻辑
        console.log("World state has been reset.");
    }

    return {
        initialize,
        reset
    };
}