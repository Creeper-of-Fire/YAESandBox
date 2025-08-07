/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { ConsumedSpec } from './ConsumedSpec';
import type { ProducedSpec } from './ProducedSpec';
import type { ValidationMessage } from './ValidationMessage';
import type { VarSpecDef } from './VarSpecDef';
/**
 * 对枢机进行静态分析后的结果报告。
 */
export type TuumAnalysisResult = {
    /**
     * Tuum 对外暴露的、可被连接的【输入端点】的完整定义列表。
     * 前端可根据此列表生成 Tuum 的输入连接点。
     */
    consumedEndpoints?: Array<ConsumedSpec> | null;
    /**
     * Tuum 对外暴露的、可引出连接的【输出端点】的完整定义列表。
     * 前端可根据此列表生成 Tuum 的输出连接点。
     */
    producedEndpoints?: Array<ProducedSpec> | null;
    /**
     * Tuum 内部所有被发现的变量及其最终推断出的类型定义。
     * Key 是内部变量名，Value 是其类型定义。
     * 这对于前端调试和提供智能提示非常有用。
     */
    internalVariableDefinitions?: Record<string, VarSpecDef> | null;
    /**
     * 在分析过程中发现的所有错误和警告的列表。
     * 如果此列表为空，代表 Tuum 配置健康。
     */
    messages?: Array<ValidationMessage> | null;
};

