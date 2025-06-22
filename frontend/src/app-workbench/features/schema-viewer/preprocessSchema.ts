import {cloneDeep} from "lodash-es";
import {defineAsyncComponent, markRaw} from "vue";
const MyCustomStringAutoComplete = markRaw(defineAsyncComponent(() => import('@/app-workbench/features/schema-viewer/field-widget/MyCustomStringAutoComplete.vue')));
const SliderWithInputWidget = markRaw(defineAsyncComponent(() => import('@/app-workbench/features/schema-viewer/field-widget/SliderWithInputWidget.vue')));

// 为 JSON Schema 属性定义一个更具体的类型
interface FieldProps
{
    // 标准 JSON Schema 字段
    type?: 'string' | 'number' | 'integer' | 'boolean' | 'object' | 'array' | string[];
    description?: string;
    default?: unknown;

    // 数字相关
    maximum?: number;
    minimum?: number;
    multipleOf?: number;

    // 枚举相关
    enum?: unknown[];
    enumNames?: string[];
    'x-enumNames'?: string[]; // 兼容 x- 前缀

    // 结构相关
    oneOf?: (FieldProps | { $ref: string })[];

    // 非标准的 UI 提示字段 (vue-form 或类似库常用)
    'ui:widget'?: string | object; // 允许字符串名称或组件对象
    'ui:options'?: {
        isEditableSelectOptions?: boolean;
        [key: string]: unknown; // 允许其他任何选项
    };
    'ui:enumOptions'?: { label: string; value: unknown }[];

    // 允许其他任何未定义的属性
    [key: string]: unknown;
}


/**
 * 预处理从后端获取的 JSON Schema，根据约定动态注入 ui:widget。
 * @param originalSchema 从后端获取的原始 JSON Schema 对象。
 * @returns 处理后、可供 vue-form 使用的 Schema 对象。
 */
export function preprocessSchemaForWidgets(originalSchema: Record<string, any>): Record<string, any>
{
    const schema = cloneDeep(originalSchema);

    // 1. 预处理 definitions
    // 必须先处理 definitions，因为 properties 可能会引用它们
    if (schema.definitions)
    {
        for (const defName in schema.definitions)
        {
            // 递归调用自身，处理 definitions 内部可能存在的嵌套 oneOf 等
            schema.definitions[defName] = preprocessSchemaForWidgets(schema.definitions[defName]);
        }
    }

    // 2. 预处理 properties
    if (schema.properties)
    {
        for (const fieldName in schema.properties)
        {
            // 使用辅助函数处理每个字段
            schema.properties[fieldName] = preprocessSchemaOfFieldProps(schema.properties[fieldName], schema.definitions);
        }
    }

    return schema;
}

/**
 * 预处理单个字段的 Schema 属性
 * @param oldFieldProps - 原始字段属性对象
 * @param definitions - 顶层的 definitions 对象，用于解析 $ref
 * @returns - 处理后的字段属性对象
 */
function preprocessSchemaOfFieldProps(oldFieldProps: unknown, definitions?: Record<string, FieldProps>): FieldProps
{
    // 1. 在执行任何操作前，先验证输入是否为有效的对象
    if (typeof oldFieldProps !== 'object' || oldFieldProps === null || Array.isArray(oldFieldProps))
    {
        // 如果输入不是一个有效的普通对象，则直接返回空对象
        // 返回 {} 是有效的，因为它符合 FieldProps 接口（所有属性都是可选的）
        return {};
    }

    // 2. 对验证过的有效对象进行深拷贝，并进行类型断言
    // 因为我们已经检查过它是一个对象，所以这里的断言是安全的。
    let fieldProps: FieldProps = cloneDeep(oldFieldProps as FieldProps);

    // ==================== oneOf 扁平化逻辑 开始 ====================
    // 如果 oneOf 中只有一个选项，则将其属性合并到顶层
    if (Array.isArray(fieldProps.oneOf) && fieldProps.oneOf.length === 1)
    {
        let singleOption = fieldProps.oneOf[0];

        // 如果这个选项是 $ref，需要去 definitions 里解析它
        // 使用类型守卫 'in' 来安全地检查属性
        if (singleOption && typeof singleOption === 'object' && '$ref' in singleOption && typeof singleOption.$ref === 'string' && definitions)
        {
            const refPath = singleOption.$ref.split('/'); // 例如 "#/definitions/PromptRoleType"
            const defName = refPath[refPath.length - 1];
            if (defName && definitions[defName])
            {
                singleOption = definitions[defName];
            }
        }

        // 确保 singleOption 是一个可以合并的对象
        if (singleOption && typeof singleOption === 'object' && !Array.isArray(singleOption))
        {
            const originalDescription = fieldProps.description; // 保存原始的 description

            // 将 oneOf 中的属性合并到主对象，主对象的同名属性优先级更高（覆盖 oneOf 的）
            fieldProps = {...singleOption, ...fieldProps};

            // 如果主对象没有 description，但原始的有，则恢复它
            if (originalDescription && !fieldProps.description)
            {
                fieldProps.description = originalDescription;
            }
        }

        // 清理掉 oneOf
        delete fieldProps.oneOf;
    }
    // ==================== oneOf 扁平化逻辑 结束 ====================

    // 确保每个 property 都有一个 ui:options 对象，方便后续写入
    if (!fieldProps['ui:options'])
    {
        fieldProps['ui:options'] = {};
    }
    // 也可以直接在 uiSchema 层面操作（如果 vue-form 优先 uiSchema）
    // 但既然你提议对 ui:widget 赋值，直接修改 fieldProps 里的 ui:widget 更直接

    // 确保 ui:options 存在，以便安全地向其添加属性
    if (!fieldProps['ui:options'])
    {
        fieldProps['ui:options'] = {};
    }

    // 规则 1: 处理数字输入类型 (滑块或普通数字输入)
    const fieldType = fieldProps.type;
    const isNumeric = (typeof fieldType === 'string' && ['number', 'integer'].includes(fieldType)) ||
        (Array.isArray(fieldType) && fieldType.some(t => ['number', 'integer'].includes(t)));

    if (isNumeric && !fieldProps['ui:widget'])
    {
        // 检查是否应该使用滑块
        if (typeof fieldProps.maximum === 'number' && typeof fieldProps.minimum === 'number')
        {
            fieldProps['ui:widget'] = SliderWithInputWidget;
            // 安全地向 ui:options 添加属性
            const options = fieldProps['ui:options'];
            options.step = fieldProps.multipleOf;
            options.default = fieldProps.default;
            options.max = fieldProps.maximum;
            options.min = fieldProps.minimum;

            // 如果类型不是'integer'，multipleOf 会被 step 替代，可以删除
            if (fieldProps.type !== 'integer')
            {
                delete fieldProps.multipleOf;
            }
        }
        else
        {
            // 否则使用普通的数字输入框
            fieldProps['ui:widget'] = 'InputNumberWidget';
            fieldProps['ui:options'].showButton = false;
        }
    }

    // 规则 2: 处理枚举类型 (单选、下拉或自动完成)
    const enumValues = fieldProps.enum;
    const enumNames = fieldProps.enumNames || fieldProps['x-enumNames'];

    if (Array.isArray(enumValues) && Array.isArray(enumNames))
    {
        // 根据 isEditableSelectOptions 决定 widget 类型
        if (fieldProps['ui:options']?.isEditableSelectOptions === true)
        {
            if (!fieldProps['ui:widget'])
            {
                fieldProps['ui:widget'] = MyCustomStringAutoComplete;
            }
            delete fieldProps['ui:options'].isEditableSelectOptions;
        }
        else
        {
            if (!fieldProps['ui:widget'])
            {
                // 这里可以根据选项数量决定用 Radio 还是 Select，但按约定先用 Radio
                fieldProps['ui:widget'] = 'RadioWidget';
            }
        }

        // 统一将 enum 和 enumNames 转换为 UI 库需要的格式
        fieldProps['ui:enumOptions'] = enumValues.map((value, index) => ({
            // 确保 enumNames[index] 存在，如果不存在则使用 value 作为备用标签
            label: (enumNames[index] as string) ?? String(value),
            value
        }));

        // 清理掉原始的 enum 字段，避免混淆
        delete fieldProps.enum;
        delete fieldProps.enumNames;
        delete fieldProps['x-enumNames'];

    }

    // 你可以根据需要添加更多规则...

    // 最后清理：如果 ui:options 最终为空，则删除它
    if (fieldProps['ui:options'] && Object.keys(fieldProps['ui:options']).length === 0)
    {
        delete fieldProps['ui:options'];
    }

    return fieldProps;
}

