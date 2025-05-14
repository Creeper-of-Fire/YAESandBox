// --- START OF FILE uiStore.ts ---

import {defineStore} from 'pinia';
import {shallowRef, type Component} from 'vue'; // shallowRef 用于存储组件引用

// 不再需要 PanelNames 常量
// export const PanelNames = { ... } as const;
// export type PanelName = ...;

type MobileFocusTarget = 'left' | 'right' | 'main';

interface UiState {
    isMobileLayout: boolean;
    /**
     * 当前在左侧区域激活的组件引用。null 表示无激活组件。
     */
    activeLeftComponent: Component | null;
    /**
     * 当前在右侧区域激活的组件引用。null 表示无激活组件。
     */
    activeRightComponent: Component | null;
    /**
     * 移动端当前焦点所在的虚拟区域。
     */
    mobileFocusTarget: MobileFocusTarget;
}

export const useUiStore = defineStore('ui', {
    state: (): UiState => ({
        isMobileLayout: false,
        // 使用 shallowRef 存储组件引用，避免深度响应式处理整个组件对象
        activeLeftComponent: shallowRef(null),
        activeRightComponent: shallowRef(null),
        mobileFocusTarget: 'main',
    }),

    getters: {
        /**
         * 计算移动端当前应显示的组件引用。
         * @returns Component 引用或 null
         */
        getMobileViewComponent: (state): Component | null => {
            if (!state.isMobileLayout) return null;

            switch (state.mobileFocusTarget) {
                case 'left':
                    // 如果焦点在左，返回左侧组件 (如果存在)
                    return state.activeLeftComponent;
                case 'right':
                    // 如果焦点在右，返回右侧组件 (如果存在)
                    return state.activeRightComponent;
                case 'main':
                default:
                    // 焦点在中间，返回 null (App.vue 会渲染 BubbleStream)
                    return null;
            }
        },
    },

    actions: {
        /**
         * 设置当前是否为移动端布局。
         */
        setIsMobileLayout(isMobile: boolean) {
            if (this.isMobileLayout !== isMobile) {
                console.log(`UIStore: 切换布局模式 -> ${isMobile ? '移动端' : '桌面端'}`);
                this.isMobileLayout = isMobile;
                // 切换到移动端时，根据当前激活的组件重置焦点
                if (isMobile) {
                    // 优先级：如果右侧有，焦点在右；否则如果左侧有，焦点在左；都无则在 main
                    if (this.activeRightComponent) this.mobileFocusTarget = 'right';
                    else if (this.activeLeftComponent) this.mobileFocusTarget = 'left';
                    else this.mobileFocusTarget = 'main';
                    console.log(`UIStore: (移动端) 初始/切换后焦点 -> ${this.mobileFocusTarget}`);
                } else {
                    this.mobileFocusTarget = 'main'; // 切回桌面，焦点回 main
                }
            }
        },

        /**
         * 设置或清除指定区域的激活组件。包含切换逻辑。
         * @param target - 目标区域 'left' 或 'right'
         * @param component - 要设置的组件引用，或 null 来清除/关闭
         */
        setActiveComponent(target: 'left' | 'right', component: Component | null) {
            let panelClosed = false;
            let panelOpenedOrSwitched = false;
            let currentComponentRef: Component | null = null;

            // 获取当前目标区域的组件引用
            if (target === 'left') {
                currentComponentRef = this.activeLeftComponent;
            } else {
                currentComponentRef = this.activeRightComponent;
            }

            // 判断操作类型：打开/切换 vs 关闭
            if (component === null) { // 请求关闭
                if (currentComponentRef !== null) {
                    panelClosed = true;
                    console.log(`UIStore: 关闭 ${target} 面板`);
                }
            } else { // 请求打开或切换
                if (currentComponentRef !== component) {
                    panelOpenedOrSwitched = true;
                    console.log(`UIStore: 打开/切换 ${target} 面板`);
                } else {
                    // 请求打开的组件已在目标位置打开 -> 变为关闭操作
                    component = null; // 将操作转为关闭
                    panelClosed = true;
                    console.log(`UIStore: 关闭 ${target} 面板 (点击已激活按钮)`);
                }
            }

            // 更新目标区域的组件引用
            if (target === 'left') {
                // 使用 markRaw 防止组件对象本身被代理，shallowRef 只跟踪引用变化
                this.activeLeftComponent = component ? shallowRef(component) : null;
            } else {
                this.activeRightComponent = component ? shallowRef(component) : null;
            }

            // 更新移动端焦点
            if (this.isMobileLayout) {
                if (panelClosed) {
                    // 关闭了面板，检查另一侧是否打开，决定焦点
                    const otherPanelComponent = (target === 'left') ? this.activeRightComponent : this.activeLeftComponent;
                    this.mobileFocusTarget = otherPanelComponent ? (target === 'left' ? 'right' : 'left') : 'main';
                    console.log(`UIStore: (移动端) 关闭 ${target} 面板，焦点移至 -> ${this.mobileFocusTarget}`);
                } else if (panelOpenedOrSwitched) {
                    // 打开或切换了面板，焦点移到该面板
                    this.mobileFocusTarget = target;
                    console.log(`UIStore: (移动端) 打开/切换 ${target} 面板，焦点移至 -> ${target}`);
                }
                // 如果是点击已打开面板关闭，panelClosed 为 true，逻辑已处理
            }
        },

        /**
         * 显式将移动端焦点设置回中间 (Bubble Stream)。
         */
        setMobileFocusToMain() {
            if (this.isMobileLayout) {
                this.mobileFocusTarget = 'main';
                console.log("UIStore: (移动端) 显式设置焦点到 -> main");
            } else {
                // 桌面端也可选择关闭两侧面板
                this.activeLeftComponent = null;
                this.activeRightComponent = null;
                console.log("UIStore: (桌面端) 返回主内容视图，关闭侧边面板");
            }
        },
    }
});

// --- END OF FILE uiStore.ts ---