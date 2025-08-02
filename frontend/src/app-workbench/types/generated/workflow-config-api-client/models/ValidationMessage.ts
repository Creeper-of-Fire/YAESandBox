/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { RuleSeverity } from './RuleSeverity';
/**
 * 一条具体的校验信息。
 */
export type ValidationMessage = {
    severity: RuleSeverity;
    /**
     * 具体的错误或警告文本。
     */
    message: string;
    /**
     * 触发此消息的规则来源，便于前端分类处理。
     * 例如："DataFlow", "SingleInTuum", "FormValidation"。
     */
    ruleSource: string;
};

