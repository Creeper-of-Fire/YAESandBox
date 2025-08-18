// src/shared/content-renderer/index.ts
import {builtinComponents, registerCustomComponents} from './core/componentRegistry';

/**
 * 安装函数：注册所有模块内置的自定义组件。
 * 这是推荐的使用方式，可以简化集成。
 */
export function installBuiltinComponents()
{
    registerCustomComponents(builtinComponents);
}

// 导出主渲染组件
export {default as ContentRenderer} from './ContentRenderer.vue';

// 导出核心方法和类型，以便在应用的其他地方使用
export {registerCustomComponents, resolveComponent} from './core/componentRegistry';
export {parseContent} from './core/contentParser';
export * from './types';

// 方便起见，也导出自定义组件本身
export {default as CollapseComponent} from './components/Collapse.vue';
export {default as InfoPopupComponent} from './components/InfoPopup.vue';

// 也导出内置组件的定义，以备不时之需
export {builtinComponents};