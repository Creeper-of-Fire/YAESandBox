import { configure as configureCore } from '../shared/monaco-lua-service.js';
// =======================================================================================
// 全局配置标志位
// IMPORTANT / 重要事项:
//
// This is a globally unique ID to prevent the configuration from running more than once.
// If you are copying this code for your own plugin, PLEASE generate a NEW UUID.
// Do not reuse this one, to avoid conflicts with other plugins.
//
// 这是一个全局唯一的ID，用于防止配置逻辑重复运行。
// 如果您正在为自己的插件复制此代码，请务必生成一个新的UUID。
// 不要重复使用此ID，以避免与其他插件发生冲突。
//
const CONFIGURED_FLAG_UUID = '__LUA_SERVICE_CONFIGURED_FLAG_644ad8e2-011f-4553-9d21-1e1a5e54b6cb';
// =======================================================================================

export default {
    configure: async (monaco) => {
        if (monaco[CONFIGURED_FLAG_UUID]) {
            console.log('[Lua Main Service] 已配置，跳过。');
            return;
        }

        // 定位主 API 清单文件
        const manifestUrl = new URL('api-manifest-main.json', import.meta.url).href;

        // 调用核心配置函数
        await configureCore(monaco, manifestUrl);

        monaco[CONFIGURED_FLAG_UUID] = true;
    }
}