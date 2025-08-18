/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { DynamicComponentAsset } from './DynamicComponentAsset';
/**
 * 获取符文 Schemas 的 API 的完整响应体。
 */
export type RuneSchemasResponse = {
    /**
     * 符文 Schema 的字典，键是符文类型名。
     */
    schemas: Record<string, any>;
    /**
     * 所有在 Schemas 中引用到的、需要动态加载的前端组件资源列表。
     */
    dynamicAssets: Array<DynamicComponentAsset>;
};

