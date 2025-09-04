import {useGameMenu} from '#/share/useGameMenu';
import {ApiProjectMetaStorage, ApiSaveSlotManager, ApiScopedStorage} from "@yaesandbox-frontend/core-services/playerSave";
import {PluginUniqueNameKey} from "@yaesandbox-frontend/core-services/injectKeys";
import {inject} from "vue";
import {useEraLiteSaveStore} from "#/features/home/useEraLiteSaveStore.ts";

/**
 * 这是 Era-Lite 应用专用的 GameMenu Composable 工厂。
 * 它必须在 Vue setup 上下文中运行，负责组装所有依赖项，
 * 并调用通用的 useGameMenu 来创建一个与当前项目绑定的实例。
 */
export function useEraLiteGameMenu()
{
    // 1. 从 Vue 上下文获取 project unique name
    const projectUniqueName = inject(PluginUniqueNameKey);
    if (!projectUniqueName)
    {
        throw new Error("无法得到 project unique name。useEraLiteGameMenu() 可能运行在非Setup上下文或对应的provide失效。");
    }

    // 2. 实例化所有需要的服务
    // 注意：ApiScopedStorage 是无状态的，可以作为单例。
    // 但为了清晰，我们在这里创建它。
    const scopedStorage = new ApiScopedStorage();
    const saveSlotManager = new ApiSaveSlotManager(projectUniqueName);
    const projectMetaStorage = new ApiProjectMetaStorage(projectUniqueName, scopedStorage);

    // 3. 创建通用的 gameMenu 实例
    const gameMenu = useGameMenu({
        saveSlotManager,
        projectMetaStorage,
    });

    // 4. 获取并初始化 Save Store
    const saveStore = useEraLiteSaveStore();
    if (!saveStore.isInitialized)
    {
        saveStore.initialize({
            scopedStorage: scopedStorage,
            activeSlot: gameMenu.activeSlot, // 将响应式的 activeSlot 传入
        });
    }

    // 5. 返回 gameMenu 实例供上层 UI (如主菜单) 使用
    return gameMenu;
}