import {useRouter} from 'vue-router';
import {LAST_ACTIVE_SCOPE_KEY, useEraLiteSaveStore} from '#/stores/useEraLiteSaveStore';
import {useGameMenu} from '#/share/useGameMenu';

/**
 * 这是 Era-Lite 应用专用的 GameMenu Composable。
 * 它负责组装所有依赖，并调用通用的 useGameMenu。
 */
export function useEraLiteGameMenu()
{
    const router = useRouter();
    const saveStore = useEraLiteSaveStore();

    // 将 Era-Lite 的具体配置和实例传递给通用的 useGameMenu
    return useGameMenu({
        storageAdapter: saveStore.storageAdapter,
        scopeManager: saveStore.scopeManager,
        saveSlotManager: saveStore.saveSlotManager,
        router: router,
        lastActiveScopeKey: LAST_ACTIVE_SCOPE_KEY, // 使用导出的常量
        gameRouteName: 'Era_Lite_Home',             // 游戏内主页的路由名
    });
}