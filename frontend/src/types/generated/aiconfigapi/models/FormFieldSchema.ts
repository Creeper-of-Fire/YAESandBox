/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { DictionaryKeyInfo } from './DictionaryKeyInfo';
import type { SchemaDataType } from './SchemaDataType';
import type { SelectOption } from './SelectOption';
import type { ValidationRules } from './ValidationRules';
/**
 * 用于描述表单字段的元数据，传递给前端以动态生成表单。
 */
export type FormFieldSchema = {
    /**
     * 字段的编程名称（通常是C#属性名）。
     */
    name: string;
    /**
     * 字段在UI上显示的标签文本。
     */
    label: string;
    /**
     * 对字段的额外描述或提示信息，显示在标签下方或作为tooltip。
     */
    description?: string | null;
    /**
     * 输入框的占位提示文本 (placeholder)。
     */
    placeholder?: string | null;
    schemaDataType: SchemaDataType;
    /**
     * 字段是否为只读。
     */
    isReadOnly: boolean;
    /**
     * 字段是否为必填。
     */
    isRequired: boolean;
    /**
     * 字段的默认值。
     */
    defaultValue?: any;
    /**
     * 用于选择类型（如枚举、下拉列表）的选项列表。
     * 如果 YAESandBox.Workflow.AIService.AiConfigSchema.FormFieldSchema.IsEditableSelectOptions 为 true，这些是建议选项，用户仍可输入自定义值。
     * 默认先选择第一个，如果YAESandBox.Workflow.AIService.AiConfigSchema.FormFieldSchema.DefaultValue不为空，尝试从YAESandBox.Workflow.AIService.AiConfigSchema.SelectOption.Value和YAESandBox.Workflow.AIService.AiConfigSchema.SelectOption.Label属性中进行匹配
     */
    options?: Array<SelectOption> | null;
    /**
     * 如果为 true，并且 Options 不为空，表示这是一个可编辑的下拉框 (combobox)。
     * 用户可以选择建议的 Options，也可以输入不在列表中的自定义值。
     * 例如，如果 SchemaDataType 是 String，且 Options 为 null 或空，但此值为 true，
     * 暗示前端可能需要一个普通的文本输入，但可能带有某种自动完成或建议机制（如果 OptionsProviderEndpoint 指定）。
     */
    isEditableSelectOptions: boolean;
    /**
     * 如果提供，表示该字段的选项可以从这个API端点动态获取。
     * 前端可以调用此端点（通常是GET请求）来刷新或获取选项列表。
     * 端点应返回 SelectOption[] 或类似结构。
     * 例如："/api/ai-models/doubao/available-models"
     */
    optionsProviderEndpoint?: string | null;
    validation?: ValidationRules;
    /**
     * 如果 SchemaDataType 是 Object，此属性包含该嵌套对象的字段定义。
     */
    nestedSchema?: Array<FormFieldSchema> | null;
    arrayItemSchema?: FormFieldSchema;
    keyInfo?: DictionaryKeyInfo;
    dictionaryValueSchema?: FormFieldSchema;
    /**
     * 字段在表单中的显示顺序，值越小越靠前。
     */
    order: number;
};

