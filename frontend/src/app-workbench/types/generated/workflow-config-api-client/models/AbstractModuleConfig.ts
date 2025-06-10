/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 模块的配置
 */
export type AbstractModuleConfig = {
    /**
     * 名字
     */
    name: string;
    /**
     * 是否被启用，默认为True
     */
    enabled: boolean;
    /**
     * 唯一的 ID，在拷贝时也需要更新
     */
    configId: string;
    /**
     * 模块的类型
     */
    moduleType: string;
    /**
     * 输入变量名
     */
    consumes: Array<string>;
    /**
     * 输出变量名
     */
    produces: Array<string>;
};

