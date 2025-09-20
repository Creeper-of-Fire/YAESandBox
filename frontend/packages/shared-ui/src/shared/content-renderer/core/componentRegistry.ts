import { type Component, shallowRef, computed, readonly } from 'vue';

// 导入我们所有的内置组件
import CollapseComponent from '../components/Collapse.vue';
import InfoPopupComponent from '../components/InfoPopup.vue';
import RawHtmlComponent from '../components/RawHtml.vue';

// --- 1. 定义核心数据结构 ---


export interface ComponentContract {
    /**
     * 定义解析器应如何处理一个标签的子内容。
     * 'strict': 递归地、严格地解析子内容。这是默认行为。
     * 'raw':    不解析子内容，将其作为单个原始字符串处理，交给组件渲染。
     */
    parseMode: 'strict' | 'raw';
    /**
     * 定义如何处理该标签周围的空白文本节点。
     * 'trim':   移除该标签前后的纯空白文本节点。（默认行为，适用于块级组件）
     * 'preserve': 保留该标签前后的纯空白文本节点。（适用于内联组件）
     */
    whitespace: 'trim' | 'preserve';
}

/**
 * 描述一个完整组件的注册信息，包括其解析契约和渲染组件。
 */
export interface ComponentRegistration {
    contract: ComponentContract;
    component: Component;
}

// --- 2. 定义内置组件及其契约 ---

export const builtinRegistrations: Record<string, ComponentRegistration> = {
    'collapse': {
        contract: { parseMode: 'strict', whitespace: 'trim' },
        component: CollapseComponent
    },
    'info-popup': {
        contract: { parseMode: 'strict', whitespace: 'preserve' },
        component: InfoPopupComponent
    },
    // 为 raw-html 定义一个特殊的 'raw' 契约
    'raw-html': {
        contract: { parseMode: 'raw', whitespace: 'trim' },
        component: RawHtmlComponent
    }
};


// --- 3. 创建响应式的中央注册表 ---

// 使用 shallowRef 存储注册表 Map，键是小写标签名
const registrationMap = shallowRef(new Map<string, ComponentRegistration>(
    Object.entries(builtinRegistrations)
));

/**
 * 注册一个或多个自定义组件及其契约。
 * @param newRegistrations 一个对象，键是标签名，值是 ComponentRegistration 对象。
 */
export function registerComponents(newRegistrations: Record<string, ComponentRegistration>): void {
    // 必须创建一个新的 Map 来触发 shallowRef 的响应性更新
    const newMap = new Map(registrationMap.value);
    for (const [tagName, registration] of Object.entries(newRegistrations)) {
        newMap.set(tagName.toLowerCase(), registration);
    }
    registrationMap.value = newMap;
}

/**
 * 从全局注册表中注销一个或多个组件。
 * @param tagNames 要注销的组件标签名数组。
 */
export function unregisterComponents(tagNames: string[]): void {
    const newMap = new Map(registrationMap.value);
    let changed = false;
    for (const tagName of tagNames) {
        if (newMap.delete(tagName.toLowerCase())) {
            changed = true;
        }
    }
    // 仅在确实有组件被删除时才更新 ref，以避免不必要的重渲染
    if (changed) {
        registrationMap.value = newMap;
    }
}


// --- 4. 提供给系统其他部分使用的公共 API ---

/**
 * 【供解析器使用】
 * 提供一个只包含解析契约的 Map。
 * 使用 computed 来确保它总是与主注册表同步。
 */
export const contractsMap = computed<Map<string, ComponentContract>>(() => {
    const contracts = new Map<string, ComponentContract>();
    for (const [tagName, registration] of registrationMap.value.entries()) {
        contracts.set(tagName, registration.contract);
    }
    return contracts;
});

/**
 * 【供渲染器使用】
 * 根据标签名解析对应的Vue组件。
 * @param tagName 标签名
 * @returns 找到的组件或undefined
 */
export function resolveComponent(tagName: string): Component | undefined {
    return registrationMap.value.get(tagName.toLowerCase())?.component;
}

/**
 * 【供调试或高级用途】
 * 提供一个对完整注册表的只读访问。
 */
export const readonlyRegistrationMap: Readonly<Record<string, Component>> = readonly(registrationMap);