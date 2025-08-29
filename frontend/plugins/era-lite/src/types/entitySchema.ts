import type {Component} from "vue";
import type {FormItemRule} from "naive-ui";

/**
 * 定义了一个统一的、驱动展示和编辑的实体字段 Schema。
 * 这是应用中关于实体结构的“单一事实来源”。
 */
export interface EntityFieldSchema {
    key: string;                    // 对应数据对象中的键名
    label: string;                  // 表单项和展示时的标签

    // -- 编辑器配置 --
    component: Component;           // 要渲染的 Vue 组件 (例如 NInput, NInputNumber)
    componentProps?: Record<string, any>; // 传递给该组件的 props (例如 { type: 'textarea' })
    rules?: FormItemRule | FormItemRule[]; // Naive UI 的表单验证规则
}