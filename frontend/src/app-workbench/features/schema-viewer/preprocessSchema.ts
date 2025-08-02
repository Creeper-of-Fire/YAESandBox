import {cloneDeep} from "lodash-es";
import {type Component, defineAsyncComponent, markRaw} from "vue";
import {getVuePluginComponent} from "@/app-workbench/features/schema-viewer/plugin-loader.ts";
import WebComponentWrapper from "@/app-workbench/features/schema-viewer/WebComponentWrapper.vue";
import MonacoEditorWidget from "@/app-workbench/features/schema-viewer/field-widget/MonacoEditorWidget.vue";

// =================================================================
// 1. 动态组件定义
// 使用 markRaw 和 defineAsyncComponent 是处理动态组件的最佳实践，
// 它可以防止 Vue 对组件对象进行不必要的响应式代理，从而提高性能。
// =================================================================
const MyCustomStringAutoComplete = markRaw(defineAsyncComponent(() => import('@/app-workbench/features/schema-viewer/field-widget/MyCustomStringAutoComplete.vue')));
const SliderWithInputWidget = markRaw(defineAsyncComponent(() => import('@/app-workbench/features/schema-viewer/field-widget/SliderWithInputWidget.vue')));
// --- 主应用内建的、通过键名引用的自定义组件 ---
// 这是我们为 RenderWithMainAppComponent 设计的注册表
const MAIN_APP_WIDGETS: Record<string, Component> = {
    'AiConfigEditorWidget': markRaw(defineAsyncComponent(() => import('@/app-workbench/features/schema-viewer/field-widget/AiConfigEditorWidget.vue')))
    // 未来可以添加更多内建组件...
};

// =================================================================
// 2. 类型定义
// 为 JSON Schema 及其 UI 扩展属性定义严谨的类型接口
// =================================================================
interface FieldProps
{
    // 标准 JSON Schema 字段
    type?: 'string' | 'number' | 'integer' | 'boolean' | 'object' | 'array' | string[];
    title?: string;
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
    properties?: Record<string, FieldProps>;
    definitions?: Record<string, FieldProps>;
    items?: FieldProps | FieldProps[];

    // UI 扩展字段 (非标准)
    'ui:widget'?: string | Component;
    'ui:custom-renderer'?: Component;
    'ui:hidden'?: boolean;
    'ui:options'?: {
        isEditableSelectOptions?: boolean;
        [key: string]: unknown;
    };
    'ui:enumOptions'?: { label: string; value: unknown }[];

    // 允许其他任何未定义的属性
    [key: string]: unknown;
}

/**
 * 检查一个字段是否为指定的目标类型，正确处理 type 为字符串或数组的情况。
 * @param field - 要检查的字段 Schema。
 * @param targetType - 目标类型，如 'array', 'object', 'number' 等。
 * @returns - 如果字段是目标类型，则返回 true，否则返回 false。
 */
function isFieldType(field: FieldProps, targetType: 'string' | 'number' | 'integer' | 'boolean' | 'object' | 'array'): boolean
{
    const fieldType = field.type;
    if (typeof fieldType === 'string')
    {
        return fieldType === targetType;
    }
    if (Array.isArray(fieldType))
    {
        // 只要数组中包含目标类型即可，忽略 'null' 等其他类型
        return fieldType.includes(targetType);
    }
    return false;
}

// =================================================================
// 3. 导出主函数 (入口)
// =================================================================
/**
 * 预处理整个 JSON Schema，递归地为其属性注入 UI Widget 并修复结构问题。
 * @param originalSchema - 从后端获取的原始 JSON Schema 对象。
 * @returns - 处理后、可供 UI 框架使用的 Schema 对象。
 */
export function preprocessSchemaForWidgets(originalSchema: Record<string, any>): FieldProps
{
    if (typeof originalSchema !== 'object' || originalSchema === null || Array.isArray(originalSchema))
    {
        return {};
    }
    const schema = cloneDeep(originalSchema as FieldProps);
    recursivePreprocess(schema, schema.definitions || {});
    return schema;
}

// =================================================================
// 4. 真正一视同仁的递归处理函数
// =================================================================
/**
 * 内部递归函数，对任何 Schema 节点执行相同的处理流程。
 * @param schemaNode - 当前正在处理的任何 Schema 节点。
 * @param definitions - 顶层的 definitions 集合，用于在整个递归过程中解析 $ref。
 */
function recursivePreprocess(schemaNode: FieldProps, definitions: Record<string, FieldProps>): void
{
    // 步骤 1: 首先处理当前节点自身的元数据转换（oneOf, widget 等）。
    // 这确保了在检查子节点之前，父节点的信息是最终的。
    // preprocessSingleField 的逻辑是正确的，它返回了一个我们想要的扁平化对象。
    const processedNode = preprocessSingleField(cloneDeep(schemaNode), definitions);

    // =================================================================
    // 步骤 2: 替换 schemaNode 的内容，而不是合并。
    // 这可以确保像 oneOf 这样的属性被彻底从原始 schemaNode 中移除。
    // =================================================================
    // 2.1 先清空当前节点的所有键
    Object.keys(schemaNode).forEach(key => delete (schemaNode as Record<string, any>)[key]);
    // 2.2 再将新节点的所有属性复制过来
    Object.assign(schemaNode, processedNode);

    // 步骤 3: 处理 definitions (如果当前节点有的话)
    // 确保在处理 properties/items 之前，所有 definitions 已被处理。
    if (schemaNode.definitions)
    {
        for (const defName in schemaNode.definitions)
        {
            recursivePreprocess(schemaNode.definitions[defName], definitions);
        }
    }

    // 步骤 4: 处理 properties (如果当前节点是对象)
    if (schemaNode.properties)
    {
        for (const fieldName in schemaNode.properties)
        {
            recursivePreprocess(schemaNode.properties[fieldName], definitions);
        }
    }

    // 步骤 5: 处理 items (如果当前节点是数组)
    // 这是修复问题的核心所在，它对任何数组类型的节点都生效。
    if (isFieldType(schemaNode, 'array') && schemaNode.items && !Array.isArray(schemaNode.items) && typeof schemaNode.items === 'object')
    {
        const itemSchema = schemaNode.items;

        // --- 关键修复逻辑：一视同仁地处理所有数组和其 items ---
        // 比较当前节点（父数组）和其 items 的描述，如果重复则删除 items 的。
        if (itemSchema.title && itemSchema.title === schemaNode.title)
        {
            delete itemSchema.title;
        }
        if (itemSchema.description && itemSchema.description === schemaNode.description)
        {
            delete itemSchema.description;
        }

        // 清理完毕后，对 items 节点本身进行递归处理。
        recursivePreprocess(itemSchema, definitions);
    }

    // =================================================================
    // 步骤 6: 最终custom-renderer分类和转换逻辑
    // 在所有子节点都处理完毕后，对当前节点进行最终决策。
    // =================================================================
    if (schemaNode['ui:custom-renderer']) {
        if (isFieldType(schemaNode, 'object')) {
            // 这是对象级自定义组件
            // 1. 保留 ui:custom-renderer 给我们的 CustomFieldRenderer 使用
            // 2. 添加 ui:hidden: true，告诉 vue3-form-naive 不要渲染这个对象
            if (!schemaNode['ui:options'])
                schemaNode['ui:options'] = {};
            schemaNode['ui:hidden'] = true;
        } else {
            // 这是字段级自定义组件
            // 1. 将自定义组件赋给官方的 ui:widget
            schemaNode['ui:widget'] = schemaNode['ui:custom-renderer'];
            // 2. 清理掉我们的中间属性
            delete schemaNode['ui:custom-renderer'];
        }
    }
}


// =================================================================
// 5. 单个字段处理函数 (无递归)
// =================================================================
/**
 * 预处理【单个字段】的 Schema 属性，主要负责 oneOf 扁平化和 widget 注入。
 * 这个函数不进行递归，只处理当前层级的字段，实现关注点分离。
 * @param fieldProps - 原始字段属性对象。
 * @param definitions - 顶层的 definitions 对象，用于解析 $ref。
 * @returns - 处理后的字段属性对象。
 */
function preprocessSingleField(fieldProps: FieldProps, definitions?: Record<string, FieldProps>): FieldProps
{
    // 对传入的对象进行操作，因为上层函数会用 Object.assign 合并
    let processedProps = fieldProps;

    // === oneOf 扁平化逻辑 ===
    if (Array.isArray(processedProps.oneOf) && processedProps.oneOf.length === 1)
    {
        let singleOption = processedProps.oneOf[0];

        if (singleOption && typeof singleOption === 'object' && '$ref' in singleOption && typeof singleOption.$ref === 'string' && definitions)
        {
            const refPath = singleOption.$ref.split('/');
            const defName = refPath[refPath.length - 1];
            if (defName && definitions[defName])
            {
                // 注意：definitions 里的项可能已经被预处理过了
                singleOption = definitions[defName];
            }
        }

        if (singleOption && typeof singleOption === 'object' && !Array.isArray(singleOption))
        {
            const originalDescription = processedProps.description;
            // 合并时，processedProps 的属性优先级更高，会覆盖 singleOption 的同名属性
            processedProps = {...singleOption, ...processedProps};
            // 如果合并后 description 丢失了，则恢复原始的
            if (originalDescription && !processedProps.description)
            {
                processedProps.description = originalDescription;
            }
        }
        delete processedProps.oneOf;
    }

    // === Widget 注入逻辑 ===
    // 确保 ui:options 存在
    if (!processedProps['ui:options'])
    {
        processedProps['ui:options'] = {};
    }

    // 规则 1: 数字类型
    // 使用 isFieldType 辅助函数进行健壮的数字类型检查。
    const isNumeric = isFieldType(processedProps, 'number') || isFieldType(processedProps, 'integer');
    if (isNumeric && !processedProps['ui:widget'])
    {
        if (typeof processedProps.maximum === 'number' && typeof processedProps.minimum === 'number')
        {
            processedProps['ui:widget'] = SliderWithInputWidget;
            const options = processedProps['ui:options'];
            options.tuum = processedProps.multipleOf;
            options.default = processedProps.default;
            options.max = processedProps.maximum;
            options.min = processedProps.minimum;
            if (processedProps.type !== 'integer')
            {
                delete processedProps.multipleOf;
            }
        }
        else
        {
            processedProps['ui:widget'] = 'InputNumberWidget';
            processedProps['ui:options'].showButton = false;
        }
    }

    // 规则 2: 枚举类型
    const enumValues = processedProps.enum;
    const enumNames = processedProps.enumNames || processedProps['x-enumNames'];

    if (Array.isArray(enumValues) && Array.isArray(enumNames))
    {
        if (processedProps['ui:options']?.isEditableSelectOptions === true)
        {
            if (!processedProps['ui:widget'])
            {
                processedProps['ui:widget'] = MyCustomStringAutoComplete;
            }
            // 使用后删除该标记
            delete processedProps['ui:options'].isEditableSelectOptions;
        }
        else if (!processedProps['ui:widget'])
        {
            processedProps['ui:widget'] = 'RadioWidget';
        }

        processedProps['ui:enumOptions'] = enumValues.map((value, index) => ({
            label: (enumNames[index] as string) ?? String(value),
            value
        }));

        delete processedProps.enum;
        delete processedProps.enumNames;
        delete processedProps['x-enumNames'];
    }

    // 规则 3: DataType 类型
    if (processedProps.dataType === 'multilineText')
    {
        // 只有在没有被手动指定 widget 时，我们才注入配置
        if (!processedProps['ui:widget'])
        {
            // 不再是: processedProps['ui:widget'] = 'textarea';
            // 而是，为默认的 InputWidget 注入配置参数
            processedProps['ui:options'].type = 'textarea';

            // 我们可以提供一些更友好的默认值，比如自动调整高度
            if (processedProps['ui:options'].autosize === undefined)
            {
                processedProps['ui:options'].autosize = {
                    minRows: 3
                };
            }
            // TODO 之后可能采用 NInput的count-graphemes来实现token自动计算/估算？
        }

        // 处理完毕后，删除这个自定义的、非标准的属性，保持最终 schema 的干净
        delete processedProps.dataType;
    }

    // === 动态组件注入逻辑 ===
    const vueComponentName = processedProps['x-vue-component'] as string;
    const wcTagName = processedProps['x-web-component'] as string;

    // 优先使用 Vue 组件
    if (vueComponentName)
    {
        const component = getVuePluginComponent(vueComponentName);
        if (component)
        {
            processedProps['ui:custom-renderer'] = component;
        }
        else
        {
            console.warn(`未找到名为 "${vueComponentName}" 的Vue插件组件。`);
        }
    }
    // 其次使用 Web Component
    else if (wcTagName)
    {
        processedProps['ui:custom-renderer'] = WebComponentWrapper;
        processedProps['ui:options'] ??= {};
        processedProps['ui:options'].tagName = wcTagName;
    }

    // monaco-editor 处理
    const monacoConfig = processedProps['x-monaco-editor'] as {
        language: string;
        simpleConfigUrl: string;
        languageServerWorkerUrl: string
    } | undefined;
    if (monacoConfig && monacoConfig.language)
    {
        // 1. 设置 ui:custom-renderer 为我们的 MonacoEditorWidget 组件
        processedProps['ui:custom-renderer'] = MonacoEditorWidget;

        // 2. 确保 ui:options 存在
        if (!processedProps['ui:options'])
        {
            processedProps['ui:options'] = {};
        }

        // 3. 将 monacoConfig 的内容作为 props 传递给 ui:options
        // vue3-form-naive 会将 ui:options 的所有键值对作为 props 传递给自定义 widget
        processedProps['ui:options'].language = monacoConfig.language;
        processedProps['ui:options'].simpleConfigUrl = monacoConfig.simpleConfigUrl;
        processedProps['ui:options'].languageServerWorkerUrl = monacoConfig.languageServerWorkerUrl;

        // 4. (可选) 删除自定义指令，保持最终 schema 干净
        delete processedProps['x-monaco-editor'];
    }

    // === 主应用内建组件注入逻辑 ===
    const customRendererKey = processedProps['x-custom-renderer'] as string;
    if (customRendererKey) {
        const component = MAIN_APP_WIDGETS[customRendererKey];
        if (component) {
            // ui:widget 不支持 type:object
            // 错误的方式：processedProps['ui:widget'] = component;
            // 正确的方式：使用我们自己的、不会被表单库解释的属性
            processedProps['ui:custom-renderer'] = component;
            processedProps['ui:hidden'] = true;
            delete processedProps['oneOf']
        } else {
            console.warn(`未在主应用中找到键名为 "${customRendererKey}" 的自定义渲染组件。`);
        }

        // 确保 x-custom-renderer 指令被清理掉，但我们新的 ui:custom-renderer 属性被保留
        delete processedProps['x-custom-renderer'];
    }

    // 清理空的 ui:options
    if (processedProps['ui:options'] && Object.keys(processedProps['ui:options']).length === 0)
    {
        delete processedProps['ui:options'];
    }

    return processedProps;
}