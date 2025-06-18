import { type Ref } from 'vue';

/**
 * 一个通用的异步数据状态机接口。
 * 它封装了数据本身、加载状态和错误信息，为UI提供了一个统一的、可预测的数据消费模式。
 * @template T - 数据的类型。
 */
export interface AsyncData<T> {
    /**
     * 响应式的数据引用。在加载完成前可能为 null 或 undefined。
     */
    data: Ref<T | null>;
    /**
     * 响应式的加载状态。
     */
    isLoading: Ref<boolean>;
    /**
     * 响应式的错误信息。如果没有错误，则为 null。
     */
    error: Ref<any | null>;
    /**
     * 一个幂等的函数，用于触发或重新触发数据加载。
     * @returns {Promise<void>}
     */
    execute: () => Promise<void>;
}