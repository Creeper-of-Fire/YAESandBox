import {cloneDeep} from "lodash-es";
import {type Component, defineAsyncComponent, markRaw} from "vue";
import {getVuePluginComponent} from "#/features/schema-viewer/plugin-loader.ts";
import WebComponentWrapper from "#/features/schema-viewer/WebComponentWrapper.vue";
import MonacoEditorWidget from "#/features/schema-viewer/field-widget/MonacoEditorWidget.vue";
import MyCustomStringAutoComplete from "#/features/schema-viewer/field-widget/MyCustomStringAutoComplete.vue";
import SliderWithInputWidget from "#/features/schema-viewer/field-widget/SliderWithInputWidget.vue";
import {NAutoComplete, NCheckbox, NInput, NInputNumber, NSelect, NSwitch} from "naive-ui";
import RadioGroupWidget from "#/features/schema-viewer/field-widget/RadioGroupWidget.vue";

// =================================================================
// 1. 组件注册表
// =================================================================

// 主应用内建的、通过键名引用的自定义组件
const MAIN_APP_WIDGETS: Record<string, Component> = {
    'AiConfigEditorWidget': markRaw(defineAsyncComponent(() => import('#/features/schema-viewer/field-widget/AiConfigEditorWidget.vue')))
    // ... 其他内建组件
};

// 将字符串标识符映射到实际的组件
const COMPONENT_MAP: Record<string, Component> = {
    // Naive UI 标准组件的别名
    // 使用 .then(m => m.NComponent) 是因为 naive-ui 的具名导出需要这样处理
    'Input': markRaw(NInput),
    'InputNumber': markRaw(NInputNumber),
    'Select': markRaw(NSelect),
    'Switch': markRaw(NSwitch),
    'Checkbox': markRaw(NCheckbox),
    'AutoComplete': markRaw(NAutoComplete),

    // 自定义 Widget 组件
    'SliderWithInputWidget': markRaw(SliderWithInputWidget),
    'MyCustomStringAutoComplete': markRaw(MyCustomStringAutoComplete),
    'MonacoEditorWidget': markRaw(MonacoEditorWidget),
    'WebComponentWrapper': markRaw(WebComponentWrapper),
    'RadioGroupWidget': markRaw(RadioGroupWidget),

    // 插件和内建组件
    ...MAIN_APP_WIDGETS,
};

// =================================================================
// 2. 类型定义
// =================================================================

// JSON Schema 字段的类型接口 (保持与原始文件一致)
interface FieldProps
{
    type?: string | string[];
    title?: string;
    description?: string;
    default?: any;
    properties?: Record<string, FieldProps>;
    items?: FieldProps | FieldProps[];
    required?: string[];
    enum?: any[];
    enumNames?: string[];
    'x-enumNames'?: string[];
    minimum?: number;
    maximum?: number;

    // ... 其他 JSON Schema 属性
    [key: string]: any;
}

// 转换后输出的视图模型接口
export interface FormFieldViewModel
{
    name: string; // 字段的唯一标识，用于 vee-validate
    label: string;
    description?: string;
    component: string; // 组件在 COMPONENT_MAP 中的键名
    props: Record<string, any>; // 传递给组件的 props
    rules: string[]; // vee-validate 的校验规则数组
    initialValue?: any;
    inlineGroup?: string;
    type: string;
    // 未来可扩展布局和依赖关系
    // layout?: { inline?: boolean; group?: string };
    // dependsOn?: { field: string; value: any };
}

// =================================================================
// 3. 主转换函数
// =================================================================

export function preprocessSchemaForVeeValidate(
    originalSchema: Record<string, any>
): { fields: FormFieldViewModel[], componentMap: Record<string, Component> }
{
    if (typeof originalSchema !== 'object' || originalSchema === null)
    {
        return {fields: [], componentMap: COMPONENT_MAP};
    }
    const schema = cloneDeep(originalSchema as FieldProps);
    const fields = processNode(schema, '', []);

    return {fields, componentMap: COMPONENT_MAP};
}

// =================================================================
// 4. 递归处理函数
// =================================================================

function processNode(
    node: FieldProps,
    path: string,
    requiredFields: string[]
): FormFieldViewModel[]
{
    // --- 处理 hidden 字段 ---
    if (node['ui:hidden'] === true)
    {
        return []; // 如果字段被标记为隐藏，则不生成任何视图模型，直接返回空数组
    }

    // 类级别组件渲染器: 如果存在，则将整个对象视为单个字段
    const classVueComponent = node['x-vue-component-class'] as string;
    if (classVueComponent)
    {
        const componentName = `plugin:${classVueComponent}`;
        COMPONENT_MAP[componentName] = getVuePluginComponent(classVueComponent)!;
        return [createFieldViewModel(node, path, requiredFields, componentName)];
    }

    const classWebComponent = node['x-web-component-class'] as string;
    if (classWebComponent)
    {
        // 对于类级别的 Web Component，我们创建一个使用 WebComponentWrapper 的视图模型
        const vm = createFieldViewModel(node, path, requiredFields, 'WebComponentWrapper');
        // 将 Web Component 的标签名作为 prop 传递
        vm.props.tagName = classWebComponent;
        return [vm];
    }

    const customRendererKey = node['x-custom-renderer-property'] as string;
    if (customRendererKey && MAIN_APP_WIDGETS[customRendererKey])
    {
        return [createFieldViewModel(node, path, requiredFields, customRendererKey)];
    }

    // 根据类型处理
    const type = getPrimaryType(node.type);
    switch (type)
    {
        case 'object':
            if (node.properties)
            {
                const required = node.required || [];
                // 1. 决定属性的遍历顺序
                //    - 如果存在 'ui:order' 数组，就使用它
                //    - 否则，回退到使用 Object.keys 的默认顺序
                const propertyKeys: string[] = node['ui:order'] && Array.isArray(node['ui:order'])
                    ? node['ui:order']
                    : Object.keys(node.properties);

                // 2. 使用我们确定的顺序来遍历属性
                return propertyKeys.flatMap(key =>
                {
                    const propNode = node.properties![key];
                    // 安全检查：如果 ui:order 中的 key 在 properties 中不存在，则跳过
                    if (!propNode) {
                        return [];
                    }

                    const newPath = path ? `${path}.${key}` : key;
                    return processNode(propNode, newPath, required);
                });
            }
            return [];
        case 'array':
            // vee-validate 使用 useFieldArray 处理数组，这里暂时简化
            // 可以创建一个自定义组件来管理数组项的增删
            console.warn(`Array type at path "${path}" is not fully supported in this simplified conversion.`);
            return [];
        default:
            // 基本类型 (string, number, boolean)
            return [createFieldViewModel(node, path, requiredFields)];
    }
}

// =================================================================
// 5. ViewModel 创建辅助函数
// =================================================================

function createFieldViewModel(
    node: FieldProps,
    name: string,
    parentRequired: string[],
    overrideComponent?: string
): FormFieldViewModel
{
    const {component, props} = determineComponentAndProps(node);
    const rules = determineValidationRules(node, name, parentRequired);
    const fieldName = name.split('.').pop()!;
    const type = getPrimaryType(node.type); // 获取字段类型

    let initialValue = node.default;

    return {
        name,
        label: node.title || fieldName,
        description: node.description,
        component: overrideComponent || component,
        props,
        rules,
        initialValue,
        inlineGroup: node['ui:inlineGroup'] as string | undefined,
        type: type,
    };
}

function getPrimaryType(type: string | string[] | undefined): string
{
    if (Array.isArray(type))
    {
        return type.find(t => t !== 'null') || 'string';
    }
    return type || 'string';
}

function determineComponentAndProps(node: FieldProps): { component: string; props: Record<string, any> }
{
    const props: Record<string, any> = {placeholder: node.description || ''};
    let component = 'Input'; // 默认组件

    // 插件或WebComponent
    const vuePlugin = node['x-vue-component-property'] as string;
    if (vuePlugin && getVuePluginComponent(vuePlugin))
    {
        const componentName = `plugin:${vuePlugin}`;
        COMPONENT_MAP[componentName] = getVuePluginComponent(vuePlugin)!;
        return {component: componentName, props};
    }
    const webComponent = node['x-web-component-property'] as string;
    if (webComponent)
    {
        props.tagName = webComponent;
        return {component: 'WebComponentWrapper', props};
    }
    const monacoConfig = node['x-monaco-editor'] as any;
    if (monacoConfig)
    {
        Object.assign(props, monacoConfig);
        return {component: 'MonacoEditorWidget', props};
    }

    // 根据类型和属性决定组件
    const type = getPrimaryType(node.type);

    if (node.enum)
    {
        // 定义一个阈值，少于等于这个数量的选项将使用 RadioGroup
        const RADIO_GROUP_THRESHOLD = 4;

        const enumNames = node.enumNames || node['x-enumNames'] || node.enum;
        props.options = node.enum.map((value, index) => ({
            label: String(enumNames[index]),
            value: value
        }));
        props.clearable = true;

        if (
            Array.isArray(node.enum) &&
            node.enum.length <= RADIO_GROUP_THRESHOLD &&
            // 确保 isEditableSelectOptions 优先级更高
            !node['ui:options']?.isEditableSelectOptions
        ) {
            component = 'RadioGroupWidget';
        } else {
            component = node['ui:options']?.isEditableSelectOptions ? 'MyCustomStringAutoComplete' : 'Select';
        }
        return {component, props};
    }

    switch (type)
    {
        case 'number':
        case 'integer':
            if (typeof node.minimum === 'number' && typeof node.maximum === 'number')
            {
                props.min = node.minimum;
                props.max = node.maximum;
                props.step = node.multipleOf || 1;
                component = 'SliderWithInputWidget';
            }
            else
            {
                component = 'InputNumber';
            }
            break;
        case 'boolean':
            component = 'Switch';
            break;
        case 'string':
            if (node.dataType === 'multilineText')
            {
                // Naive UI Input 的多行模式
                props.type = 'textarea';
                props.autosize = {minRows: 3}; // 提供一个合理的默认值
                component = 'Input';
            }
            else
            {
                component = 'Input';
            }
            break;
    }

    return {component, props};
}

function determineValidationRules(node: FieldProps, name: string, parentRequired: string[]): string[]
{
    const rules: string[] = [];
    const fieldName = name.split('.').pop()!;
    if (parentRequired.includes(fieldName))
    {
        rules.push('required');
    }

    const type = getPrimaryType(node.type);
    if (type === 'number' || type === 'integer')
    {
        // FIX: 'numeric' 规则过于严格（只允许无符号整数）。
        // 对于 'integer' 类型，使用 'integer' 规则（允许负数）；
        // 对于 'number' 类型，使用 'double' 规则（允许负数和小数）。
        rules.push(type === 'integer' ? 'integer' : 'double');

        if (node.minimum !== undefined)
            rules.push(`min_value:${node.minimum}`);
        if (node.maximum !== undefined)
            rules.push(`max_value:${node.maximum}`);
    }
    if (node.format === 'email')
    {
        rules.push('email');
    }
    // ... 可以添加更多规则映射
    return rules;
}