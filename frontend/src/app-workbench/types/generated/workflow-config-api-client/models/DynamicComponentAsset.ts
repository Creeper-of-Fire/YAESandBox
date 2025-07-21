/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 描述一个需要动态加载的前端组件资源。
 */
export type DynamicComponentAsset = {
    /**
     * 插件的唯一名称，用于构建URL和识别。
     */
    pluginName: string;
    /**
     * 组件类型："Vue" 或 "WebComponent"。
     */
    componentType: string;
    /**
     * 组件脚本的完整可访问URL。
     */
    scriptUrl: string;
    /**
     * 组件样式表的完整可访问URL（如果存在）。
     */
    styleUrl: string;
};

