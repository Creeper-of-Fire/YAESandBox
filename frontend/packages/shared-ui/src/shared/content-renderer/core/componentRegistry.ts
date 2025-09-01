import { type Component, shallowRef, computed, readonly } from 'vue';

// 导入我们所有的内置组件
import CollapseComponent from '../components/Collapse.vue';
import InfoPopupComponent from '../components/InfoPopup.vue';
// import RawHtmlComponent from '../components/RawHtml.vue';

// --- 1. 定义核心数据结构 ---

/**
 * 定义解析器应如何处理一个标签的子内容。
 * 'strict': 递归地、严格地解析子内容。这是默认行为。
 * 'raw':    不解析子内容，将其作为单个原始字符串处理，交给组件渲染。
 */
export interface ComponentContract {
    parseMode: 'strict' | 'raw';
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
        contract: { parseMode: 'strict' },
        component: CollapseComponent
    },
    'info-popup': {
        contract: { parseMode: 'strict' },
        component: InfoPopupComponent
    },
    // 为 raw-html 定义一个特殊的 'raw' 契约
    // 'raw-html': {
    //     contract: { parseMode: 'raw' },
    //     component: RawHtmlComponent
    // }
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