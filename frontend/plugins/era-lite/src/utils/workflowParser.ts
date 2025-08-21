import type { ComplexPropertyValue } from '#/types/streaming';

/**
 * 从 ComplexPropertyValue 中安全地提取可显示的文本。
 * 处理三种情况:
 * 1. "纯字符串" -> "纯字符串"
 * 2. { _text: "文本" } -> "文本"
 * 3. { other: "..." } -> "" (没有可直接显示的文本)
 * @param value 要解析的值
 * @returns 提取出的字符串，如果没有则返回空字符串
 */
export function getText(value: ComplexPropertyValue | undefined): string {
    if (!value) return '';
    if (typeof value === 'string') {
        return value;
    }
    return value._text || '';
}

/**
 * 从 ComplexPropertyValue 中安全地提取AI的“思考”内容。
 * @param value 要解析的值
 * @returns 思考内容字符串，如果没有则返回 null
 */
export function getThink(value: ComplexPropertyValue | undefined): string | null {
    if (!value || typeof value === 'string') {
        return null;
    }
    // `think` 的值根据后端契约，应该就是纯字符串
    if (typeof value.think === 'string') {
        return value.think;
    }
    return null;
}

/**
 * 从 ComplexPropertyValue 中解析数字。
 * 后端返回的数字是字符串，我们需要将其转换为数字。
 * @param value 要解析的值
 * @returns 解析后的数字价格
 */
export function parseNumber(value: ComplexPropertyValue | undefined): number | null{
    const textValue = getText(value);
    const parsed = parseInt(textValue, 10);
    return isNaN(parsed) ? null : parsed;
}