/**
 * 后端返回的属性值可能是纯文本，
 * 也可能是一个包含文本和/或嵌套标签（如 <think>）的对象。
 */
export type ComplexPropertyValue = string | {
    _text?: string;
    think?: string;
    [key: string]: any; // 容纳其他标签
};

/**
 * 这是我们从 useWorkflowStream 中期望得到的原始、未处理的流式对象结构。
 * 注意：所有属性都是可选的，且值是 ComplexPropertyValue。
 * 价格会以字符串形式返回。
 */
export interface StreamedItem {
    name: ComplexPropertyValue;
    description: ComplexPropertyValue;
    price: ComplexPropertyValue; // 后端返回 string 或 { _text: "150", think: "..." }
}