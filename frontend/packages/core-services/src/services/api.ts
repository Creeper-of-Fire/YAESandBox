import type {TokenResolver} from "../injectKeys.ts";
import type {ApiRequestOptions} from "../types";

/**
 * 获取后端的基地址。
 * - 如果在宿主环境中，它会读取注入的 `window.__BACKEND_URL__`。
 * - 如果在同源Web环境中，它会返回一个空字符串。
 * @returns {string} 后端的基地址或空字符串。
 */
export const getBaseUrl = (): string => {
    return (window as any).__BACKEND_URL__ || '';
};

/**
 * 根据运行环境，将相对路径转换为可用的URL。
 * @param {string} relativePath - 像 '/api/data' 或 '/hubs/chat' 这样的相对路径。
 * @returns {string} - 在宿主环境中是完整的绝对URL，在同源Web环境中是原始的相对URL。
 */
export const getAbsoluteUrl = (relativePath: string): string => {
    const baseUrl = getBaseUrl();

    // 如果 baseUrl 存在, 我们构建一个完整的URL。
    // URL构造函数能优雅地处理斜杠问题。
    if (baseUrl) {
        return new URL(relativePath, baseUrl).href;
    }

    // 如果 baseUrl 是空字符串 (标准Web环境), 我们直接返回相对路径。
    // 浏览器会自动将其解析到当前域。
    return relativePath;
};