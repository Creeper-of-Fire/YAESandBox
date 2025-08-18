/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 代表一个包含完整定义的 AI 配置类型，用于前端一次性加载。
 */
export type AiConfigTypeWithSchemaDto = {
    /**
     * 选项的实际值，即配置类型的编程名称 (例如 "DoubaoAiProcessorConfig")。
     */
    value: string;
    /**
     * 选项在UI上显示的文本 (例如 "豆包AI模型")。
     */
    label: string;
    /**
     * 用于动态生成表单的 JSON Schema (包含UI指令)。
     */
    schema: any;
};

