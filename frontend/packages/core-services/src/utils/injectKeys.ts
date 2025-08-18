// src/utils/injectionKeys.ts

// 定义选中项的类型
import type {InjectionKey, Ref} from "vue";

/**
 * 用于在应用中注入是否为暗黑主题的状态
 */
export const IsDarkThemeKey: InjectionKey<Ref<boolean>> = Symbol('IsDarkThemeKey');

export type ApiRequestOptions = {
    readonly method: 'GET' | 'PUT' | 'POST' | 'DELETE' | 'OPTIONS' | 'HEAD' | 'PATCH';
    readonly url: string;
    readonly path?: Record<string, any>;
    readonly cookies?: Record<string, any>;
    readonly headers?: Record<string, any>;
    readonly query?: Record<string, any>;
    readonly formData?: Record<string, any>;
    readonly body?: any;
    readonly mediaType?: string;
    readonly responseHeader?: string;
    readonly errors?: Record<number, string>;
};

/**
 * 定义 Token 解析器函数的类型。
 * 这是一个异步函数，接收 ApiRequestOptions 参数，返回一个 Promise，该 Promise 解析为 token 字符串。
 */
export type TokenResolver = (options: ApiRequestOptions) => Promise<string>;

/**
 * 用于在整个应用中注入 TokenResolver 的 InjectionKey。
 */
export const TokenResolverKey: InjectionKey<TokenResolver> = Symbol('TokenResolver');