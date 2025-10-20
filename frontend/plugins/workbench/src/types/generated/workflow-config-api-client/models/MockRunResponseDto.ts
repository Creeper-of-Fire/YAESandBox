/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { IRuneProcessorDebugDto } from './IRuneProcessorDebugDto';
/**
 * 测试响应的数据传输对象。
 */
export type MockRunResponseDto = {
    /**
     * 是否运行成功。
     */
    isSuccess: boolean;
    /**
     * 失败时，这里是详细的错误信息。
     */
    errorMessage?: string | null;
    /**
     * 符文执行后产生的所有输出变量。
     * Key是变量名，Value是执行后的结果。
     */
    producedOutputs?: Record<string, any> | null;
    debugInfo?: IRuneProcessorDebugDto;
};

