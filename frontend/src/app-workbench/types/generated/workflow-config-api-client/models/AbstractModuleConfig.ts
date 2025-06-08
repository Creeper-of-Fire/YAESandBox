/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 模组的配置
 */
export type AbstractModuleConfig = {
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

