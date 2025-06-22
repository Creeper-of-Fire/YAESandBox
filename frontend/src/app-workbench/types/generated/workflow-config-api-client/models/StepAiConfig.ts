/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 步骤本身的 AI 配置。
 */
export type StepAiConfig = {
    /**
     * AI服务的配置的UUID
     */
    aiProcessorConfigUuid?: string | null;
    /**
     * 当前选中的AI模型的类型名
     */
    selectedAiModuleType?: string | null;
    /**
     * 是否为流式传输
     */
    isStream: boolean;
};

