import { deepMerge } from './utils.js';

// 使用 Map 来存储按 contextId 分类的 API 数据
const apiStore = new Map();

/**
 * 注册一个上下文的 API 数据。
 * @param {string} contextId - 上下文的唯一标识符, e.g., 'main', 'string', 'shared'.
 * @param {Object} apiData - 该上下文解析后的 API 对象。
 */
export function register(contextId, apiData) {
    apiStore.set(contextId, apiData);
    console.log(`[API Registry] 上下文 '${contextId}' 的 API 已注册/更新。`);
}

/**
 * 根据上下文 ID 获取完整的、合并后的 API 数据。
 * 这个版本的 get 函数通过部分匹配来查找上下文，使其更加健壮。
 *
 * @param {string} contextId - 上下文的唯一标识符，通常是一个部分路径，如 '/plugins/MyPlugin/'.
 * @returns {Object} - 合并后的 API 数据。
 */
export function get(contextId) {
    let contextApi = {}; // 默认的上下文 API 为空对象

    // 如果 contextId 无效，则直接返回共享 API
    if (!contextId || typeof contextId !== 'string') {
        return {};
    }

    // 遍历 apiStore 中的所有条目，寻找匹配的键
    // storedKey 是注册时使用的完整 URL，如 'http://.../plugins/MyPlugin/main.js?import'
    // contextId 是查找时使用的部分路径，如 '/plugins/MyPlugin/'
    for (const [storedKey, apiData] of apiStore.entries()) {
        if (storedKey.includes(contextId)) {
            contextApi = apiData;
            break; // 找到第一个匹配项后就停止遍历
        }
    }

    // // 如果遍历完后仍然没有找到匹配项，可以打印一个警告
    // if (Object.keys(contextApi).length === 0) {
    //     console.warn(`[API Store] 未找到与 '${contextId}' 匹配的 API 上下文。`);
    // }

    return contextApi;
}