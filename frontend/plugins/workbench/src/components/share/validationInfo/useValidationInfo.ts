import {computed, type Ref} from 'vue';
import {RuleSeverity, type ValidationMessage} from "#/types/generated/workflow-config-api-client";


/**
 * @internal
 * 定义校验结果的结构。
 */
interface ValidationResult
{
    count: number;
    messages: ValidationMessage[];
}

/**
 * 一个可组合函数，用于处理和计算校验信息的状态。
 * @param messagesRef 一个包含 ValidationMessage 数组的响应式引用。
 * @returns 返回一个计算属性 `validationInfo`，它包含独立的错误和警告信息（数量和列表），如果不存在则为 null。
 */
export function useValidationInfo(messagesRef: Ref<ValidationMessage[] | undefined | null>)
{
    const validationInfo = computed(() =>
    {
        const messages = messagesRef.value;
        if (!messages || messages.length === 0)
        {
            return {errors: null, warnings: null};
        }

        const errorMessages = messages.filter(m => m.severity === RuleSeverity.ERROR || m.severity === RuleSeverity.FATAL);
        const warningMessages = messages.filter(m => m.severity === RuleSeverity.WARNING);

        const errors: ValidationResult | null = errorMessages.length > 0
            ? {count: errorMessages.length, messages: errorMessages}
            : null;

        const warnings: ValidationResult | null = warningMessages.length > 0
            ? {count: warningMessages.length, messages: warningMessages}
            : null;

        return {errors, warnings};
    });

    return {
        validationInfo,
    };
}