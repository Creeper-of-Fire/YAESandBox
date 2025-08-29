/**
 * 定义了生成器面板中每个字段的 schema。
 *
 * @template T - 字段值的类型，例如 string 或 number。
 */
export interface SchemaField<T = string | number> {
    key: string;        // 对应数据对象中的键名
    label: string;      // 显示在 UI 上的标签
    type: 'text' | 'number' | 'textarea'; // 决定如何解析和显示
}