import { register } from '../shared/api-registry.js';
import { loadAndMergeApis } from '../shared/utils.js';
import {configureGlobalLuaProviders} from "../shared/monaco-lua-service.js";
// **插件自动识别自己的 ID**
const CONTEXT_ID = import.meta.url;

// 使用一个 Set 来防止同一个插件被重复配置
const configuredPlugins = new Set();

async function configure(monaco) {
    // 确保该语言的全局服务已经初始化
    await configureGlobalLuaProviders(monaco);
    
    // 如果这个 URL 对应的插件已经配置过，则跳过
    if (configuredPlugins.has(CONTEXT_ID)) {
        console.log(`[Plugin Loader] 插件 '${CONTEXT_ID}' 已配置，跳过。`);
        return;
    }

    const manifestUrl = new URL('api-manifest-main.json', import.meta.url).href;
    const apiData = await loadAndMergeApis(manifestUrl);

    // 使用自己的 URL 作为 contextId 进行注册
    register(CONTEXT_ID, apiData);

    // 标记为已配置
    configuredPlugins.add(CONTEXT_ID);
    console.log(`[Plugin Loader] 插件 '${CONTEXT_ID}' 配置完成。`);
}

export default { configure };