/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 这个是Ai服务配置的基类，仅含绝对必须的字段。
 */
export type AbstractAiProcessorConfig = {
    /**
     * 配置的名称，不唯一（防止不小心搞错了），建议保证其是唯一的
     */
    configName: string;
    /**
     * 模型的类型，持久化时工厂模式会使用它
     */
    readonly moduleType: string;
    /**
     * 最大输出Token数
     */
    maxOutputTokens?: number | null;
    /**
     * 最大输入Token数。不出现在请求体中，但是在其他地方（如历史记录生成）会有用。
     */
    maxInputTokens?: number | null;
};

