import {type Component, readonly, shallowRef} from 'vue';

// 导入我们所有的内置组件
import CollapseComponent from '../components/Collapse.vue';
import InfoPopupComponent from '../components/InfoPopup.vue';

// 定义内置组件的映射表
export const builtinComponents: Record<string, Component> = {
    'collapse': CollapseComponent,
    'info-popup': InfoPopupComponent,
};


// 使用 shallowRef 来存储映射，以获得轻量的响应性
// 键是标签名 (小写)，值是Vue组件对象
const componentMap = shallowRef<Record<string, Component>>({});

/**
 * 注册一个或多个自定义标签组件
 * @param newComponents 一个对象，键是标签名，值是组件
 */
export function registerCustomComponents(newComponents: Record<string, Component>): void
{
    componentMap.value = {
        ...componentMap.value,
        ...newComponents
    };
}

/**
 * 根据标签名解析对应的Vue组件
 * @param tagName 标签名
 * @returns 找到的组件或undefined
 */
export function resolveComponent(tagName: string): Component | undefined
{
    return componentMap.value[tagName.toLowerCase()];
}

/**
 * 提供一个只读的组件映射表，供调试或高级用途
 */
export const readonlyComponentMap: Readonly<Record<string, Component>> = readonly(componentMap);