import type {EntityFieldSchema} from "./entitySchema.ts";

/**
 * 从实体字段 Schema 中提取所有扁平化的输出路径。
 *
 * 这个函数是连接“组件期望的数据结构 (schema)”和“工作流选择器UI提示 (expectedOutputs)”的关键桥梁。
 *
 * @param schema - 实体字段的 Schema 数组。
 * @returns 一个由扁平化路径组成的字符串数组，例如 ['name', 'description', 'avatar']。
 */
export function extractOutputsFromSchema(schema: EntityFieldSchema[] | undefined): string[] {
    if (!schema) {
        return [];
    }

    return schema.map(field => {
        // useStructuredWorkflowStream 使用 '__' 连接路径
        // 所以我们也用同样的方式来表示期望的输出
        return field.path.join('__');
    });
}