import type {Component} from "vue";
import type {FormItemRule} from "naive-ui";

/**
 * 定义了一个统一的、驱动展示和编辑的实体字段 Schema。
 * 这是应用中关于实体结构的“单一事实来源”。
 * 使用 `path` 来支持深层嵌套的对象结构。
 */
export interface EntityFieldSchema {
    path: string[];                 // 对应数据对象中的访问路径，例如 ['content', 'description']
    label: string;                  // 表单项和展示时的标签
    dataType?: 'string' | 'number'; // 定义字段的数据类型

    // -- 编辑器配置 --
    component: Component;           // 要渲染的 Vue 组件 (例如 NInput, NInputNumber)
    componentProps?: Record<string, any>; // 传递给该组件的 props (例如 { type: 'textarea' })
    rules?: FormItemRule | FormItemRule[]; // Naive UI 的表单验证规则
}

export const getKey = (field: EntityFieldSchema) => {
    // 接收 { path: ['content', 'description'], ... }
    // 返回 'content__description'
    return field.path.join('__');
};