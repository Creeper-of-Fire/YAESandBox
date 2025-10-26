// src/utils/injectionKeys.ts

// 定义选中项的类型
import {type ComputedRef, inject, type InjectionKey, type Ref} from "vue";
import type {ApiRequestOptions, GlobalResourceItem, WorkflowConfig} from "./types";

/**
 * 用于在应用中注入是否为暗黑主题的状态
 */
export const IsDarkThemeKey: InjectionKey<Ref<boolean>> = Symbol('IsDarkThemeKey');
/**
 * 定义 Token 解析器函数的类型。
 * 这是一个异步函数，接收 ApiRequestOptions 参数，返回一个 Promise，该 Promise 解析为 token 字符串。
 */
export type TokenResolver = (options: ApiRequestOptions) => Promise<string>;

/**
 * 用于在整个应用中注入 TokenResolver 的 InjectionKey。
 */
export const TokenResolverKey: InjectionKey<TokenResolver> = Symbol('TokenResolver');

export const PluginUniqueNameKey: InjectionKey<string> = Symbol('PluginUniqueNameKey');

export type WorkflowResourceItem = GlobalResourceItem<WorkflowConfig>;

/**
 * @interface IWorkflowConfigProvider
 * @description 定义一个提供全局 WorkflowConfig 列表的异步数据源的契约。
 * 任何插件想要消费全局工作流数据，都应该依赖此接口，而不是具体的 store 实现。
 * 这确保了插件之间的低耦合。
 */
export interface IWorkflowConfigProvider
{
    /**
     * @description 包含所有全局工作流配置的只读状态。
     * Key 是工作流的 ID，Value 是 WorkflowResourceItem。
     */
    readonly state: ComputedRef<Record<string, WorkflowResourceItem>>;
    /**
     * @description 指示当前是否正在加载数据。
     */
    readonly isLoading: ComputedRef<boolean>;
    /**
     * @description 指示数据是否已成功加载至少一次。
     */
    readonly isReady: ComputedRef<boolean>;
    /**
     * @description 如果加载过程中发生错误，这里会包含错误信息。
     */
    readonly error: ComputedRef<any>;

    /**
     * @description 触发或重新触发数据加载过程的函数。
     * @returns {Promise<void>} 当加载过程完成时解析的 Promise。
     */
    execute: () => Promise<void>;
}

/**
 * @description 用于在整个应用中注入 IWorkflowConfigProvider 实现的 InjectionKey。
 */
export const WorkflowConfigProviderKey: InjectionKey<IWorkflowConfigProvider> = Symbol('WorkflowConfigProviderKey')


export function useProjectUniqueName()
{
    const projectUniqueName = inject(PluginUniqueNameKey);
    if (!projectUniqueName)
    {
        throw new Error("插件的独有名称未被提供。");
    }
    return projectUniqueName
}

export {WorkflowAnalysisProviderKey, type IWorkflowAnalysisProvider} from "./inject-key/WorkflowAnalysisProvider"