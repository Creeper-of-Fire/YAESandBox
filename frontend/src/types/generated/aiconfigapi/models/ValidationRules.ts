/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 包含字段的校验规则。
 */
export type ValidationRules = {
    /**
     * 对于数字类型，允许的最小值。
     */
    min?: number | null;
    /**
     * 对于数字类型，允许的最大值。
     */
    max?: number | null;
    /**
     * 对于字符串类型，允许的最小长度。
     */
    minLength?: number | null;
    /**
     * 对于字符串类型，允许的最大长度。
     */
    maxLength?: number | null;
    /**
     * 正则表达式模式，用于校验输入。
     * 也可用于特殊标记，如 "url"，由前端特定处理。
     */
    pattern?: string | null;
    /**
     * 当校验失败时显示的通用错误信息。
     * 如果多个校验特性都提供了错误信息，它们可能会被合并。
     */
    errorMessage?: string | null;
};

