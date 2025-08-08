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
    consumedEndpoints: Array<ConsumedSpec>;
    /**
     * Tuum 对外暴露的、可引出连接的【输出端点】的完整定义列表。
     * 前端可根据此列表生成 Tuum 的输出连接点。
     */
    producedEndpoints: Array<ProducedSpec>;
    /**
     * Tuum 内部所有被【消费】的变量的聚合列表。
     * 这对于前端在配置输入映射时，提供可用的内部目标变量推荐列表非常有用。
     */
    internalConsumedSpecs: Array<ConsumedSpec>;
    /**
     * Tuum 内部所有被【生产】的变量的聚合列表。
     * 这对于前端在配置输出映射时，提供可用的内部源变量推荐列表非常有用。
     */
    internalProducedSpecs: Array<ProducedSpec>;
    /**
     * Tuum 内部所有被发现的变量及其最终推断出的统一类型定义。
     * Key 是内部变量名，Value 是其类型定义。
     * 这主要用于内部校验和为外部端点确定类型。
     */
    internalVariableDefinitions: Record<string, VarSpecDef>;
    /**
     * 在分析过程中发现的所有错误和警告的列表。
     * 如果此列表为空，代表 Tuum 配置健康。
     */
    messages: Array<ValidationMessage>;
};

