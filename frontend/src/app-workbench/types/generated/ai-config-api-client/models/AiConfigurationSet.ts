/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AbstractAiProcessorConfig } from './AbstractAiProcessorConfig';
/**
 * 代表一个 AI 配置集，它包含了一组特定类型的 AI 配置。
 */
export type AiConfigurationSet = {
    /**
     * 用户为配置集指定的名称，用于在 UI 上显示和识别。
     */
    configSetName: string;
    /**
     * 包含在此配置集中的具体 AI 配置。
     * Key 是 AI 配置的模块类型 (ModuleType, 例如 "DoubaoAiProcessorConfig")。
     * Value 是该模块类型的具体配置数据对象 (不包含 ConfigName 和 ModuleType 字段本身)。
     */
    configurations: Record<string, AbstractAiProcessorConfig>;
};

