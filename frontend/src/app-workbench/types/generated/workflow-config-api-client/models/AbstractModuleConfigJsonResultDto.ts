/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AbstractModuleConfig } from './AbstractModuleConfig';
export type AbstractModuleConfigJsonResultDto = {
    isSuccess: boolean;
    data: AbstractModuleConfig;
    errorMessage: string | null;
    /**
     * 失败时，有可能返回序列化错误时的原始文本
     */
    originJsonString: string | null;
};

