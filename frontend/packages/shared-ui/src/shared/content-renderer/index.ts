// src/shared/content-renderer/index.ts
import { registerComponents, builtinRegistrations } from './core/componentRegistry';

/**
 * 安装函数：注册所有模块内置的组件及其解析契约。
 * 默认情况下，渲染器是空的。首次使用时应调用此函数来启用内置组件。
 * 这是推荐的、最简单的集成方式。
 */
export function installBuiltinComponents() {
    registerComponents(builtinRegistrations);
}

// --- 核心导出 ---

/**
 * 主渲染组件，是使用此模块的核心。
 */
export { default as ContentRenderer } from './ContentRenderer.vue';

/**
 * 核心 API 方法，供高级用法或动态注册。
 */
export {
    registerComponents,     // 注册新的组件及其契约
    resolveComponent,     // 根据标签名解析 Vue 组件
    unregisterComponents, // 卸载已注册的组件
    contractsMap          // 获取响应式的、只读的组件契约 Map
} from './core/componentRegistry';

export { parseContent } from './core/contentParser';


// --- 类型定义导出 ---

/**
 * 导出所有核心的数据结构类型。
 */
export * from './types';
export type {
    ComponentContract,
    ComponentRegistration
} from './core/componentRegistry';


// --- 组件及定义导出 (方便直接使用或调试) ---

/**
 * 导出内置组件的 Vue 组件本身，以便在应用的其他地方直接使用。
 */
export { default as CollapseComponent } from './components/Collapse.vue';
export { default as InfoPopupComponent } from './components/InfoPopup.vue';
export { default as RawHtmlComponent } from './components/RawHtml.vue';

/**
 * 导出内置组件的完整注册定义，以备不时之需或用于构建自定义的安装逻辑。
 */
export { builtinRegistrations };