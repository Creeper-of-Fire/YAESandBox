/**
 * 后端返回的属性值是一个对象，
 * 它可能包含文本、嵌套标签（如 <think>）或两者都有。
 */
export type ComplexPropertyValue = {
    _text?: string;
    think?: string;
    [key: string]: any; // 容纳其他标签
};