// 从 Naive UI (或其他你用的UI库) 导入你想注入的组件
import {NButton, NCard, NCollapse, NCollapseItem, NInput, NProgress, NSlider, NTag, useMessage} from 'naive-ui';

import {Fragment as FragmentWrapper, h as hWrapper} from './utils/h-wrapper';

// 从 core-services 导入核心服务
import * as workflowThings from '@yaesandbox-frontend/core-services/workflow';
import * as composableThings from '@yaesandbox-frontend/core-services/composables';
import * as injectKeys from '@yaesandbox-frontend/core-services/injectKeys';
import * as coreServices from '@yaesandbox-frontend/core-services';

// 从 shared-ui 导入共享 UI 组件和功能
import * as icons from '@yaesandbox-frontend/shared-ui/icons'
import * as contentRenderer from '@yaesandbox-frontend/shared-ui/content-renderer'
import * as sharedUI from '@yaesandbox-frontend/shared-ui'

// 创建一个增强版的 Vue 对象
const enhancedVue = {
    ...(window as any).Vue, // 复制所有 Vue 的原始导出 (ref, computed, watch, etc.)
    h: hWrapper,        // 用我们的 h-wrapper 覆盖 h
    Fragment: FragmentWrapper // 确保 Fragment 也被正确提供
};

/**
 * 定义我们要注入到 JSX 执行上下文中的所有内容。
 * - 键 (Key): 将在 JSX 代码中使用的变量名 (e.g., 'NButton')。
 * - 值 (Value): 实际导入的对象、函数或组件。
 */
export const injectionMap: Record<string, any> = {
    Vue: enhancedVue,

    // UI 组件
    NButton,
    NInput,
    NCard,
    NProgress,
    NTag,
    NSlider,
    NCollapse,
    NCollapseItem,

    // Hooks (需要注意，hook 必须在 setup 函数内部调用)
    useMessage,

    // 共享 UI 组件和功能
    ...icons,
    ...contentRenderer,
    ...sharedUI,

    // 核心服务
    ...workflowThings,
    ...composableThings,
    ...injectKeys,
    ...coreServices,
};

// 导出注入项的名称列表和值列表，方便 store 使用
export const injectionKeys = Object.keys(injectionMap);
export const injectionValues = Object.values(injectionMap);