/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AbstractRuneConfig } from './AbstractRuneConfig';
/**
 * 测试请求的数据传输对象。
 */
export type MockRunRequestDto = {
    runeConfig: AbstractRuneConfig;
    /**
     * 模拟的输入变量。
     * Key是变量名，Value是用户提供的模拟值。
     */
    mockInputs: Record<string, any>;
};

