import { defineStore } from 'pinia';
import {shallowRef, markRaw, defineAsyncComponent} from 'vue';
import type { Component } from 'vue';

// 预定义面板类型
const EntityListPanel = markRaw(defineAsyncComponent(() => import('@/components/panels/EntityListPanel.vue')));
const GameStatePanel = markRaw(defineAsyncComponent(() => import('@/components/panels/GameStatePanel.vue')));
const SettingsPanel = markRaw(defineAsyncComponent(() => import('@/components/panels/SettingsPanel.vue')));
// ... 其他面板

interface PanelInfo {
    component: Component | null;
    title: string;
    props?: Record<string, any>; // 可以给面板传递 props
}

interface UiState {
    isLeftPanelOpen: boolean;
    isRightPanelOpen: boolean;
    isLeftPanelPinned: boolean;
    isRightPanelPinned: boolean;
    activeLeftPanel: PanelInfo;
    activeRightPanel: PanelInfo;
    leftPanelWidth: number;
    rightPanelWidth: number;
}

export const useUiStore = defineStore('ui', {
    state: (): UiState => ({
        isLeftPanelOpen: false,
        isRightPanelOpen: false,
        isLeftPanelPinned: false,
        isRightPanelPinned: false,
        activeLeftPanel: { component: null, title: '' },
        activeRightPanel: { component: null, title: '' },
        leftPanelWidth: 350, // 默认宽度
        rightPanelWidth: 400, // 默认宽度
    }),
    getters: {
        activeLeftPanelComponent: (state) => state.activeLeftPanel.component,
        activeRightPanelComponent: (state) => state.activeRightPanel.component,
        leftPanelTitle: (state) => state.activeLeftPanel.title,
        rightPanelTitle: (state) => state.activeRightPanel.title,
    },
    actions: {
        openLeftPanel(component: Component, title: string, props?: Record<string, any>) {
            this.activeLeftPanel = { component: shallowRef(component), title, props };
            this.isLeftPanelOpen = true;
            console.log(`UIStore: 打开左侧面板 - ${title}`);
        },
        openRightPanel(component: Component, title: string, props?: Record<string, any>) {
            this.activeRightPanel = { component: shallowRef(component), title, props };
            this.isRightPanelOpen = true;
            console.log(`UIStore: 打开右侧面板 - ${title}`);
        },
        closeLeftPanel() {
            if (!this.isLeftPanelPinned) {
                this.isLeftPanelOpen = false;
                // 可以考虑延迟清空组件，给动画留时间
                // setTimeout(() => { this.activeLeftPanel = { component: null, title: '' }; }, 300);
                this.activeLeftPanel = { component: null, title: '' }; // 暂时立即清空
                console.log('UIStore: 关闭左侧面板');
            }
        },
        closeRightPanel() {
            if (!this.isRightPanelPinned) {
                this.isRightPanelOpen = false;
                this.activeRightPanel = { component: null, title: '' };
                console.log('UIStore: 关闭右侧面板');
            }
        },
        toggleLeftPanelPin() {
            this.isLeftPanelPinned = !this.isLeftPanelPinned;
            console.log(`UIStore: 左侧面板锁定状态: ${this.isLeftPanelPinned}`);
        },
        toggleRightPanelPin() {
            this.isRightPanelPinned = !this.isRightPanelPinned;
            console.log(`UIStore: 右侧面板锁定状态: ${this.isRightPanelPinned}`);
        },
        
        // --- 新增：通用的切换方法 ---
        /**
         * 切换左侧面板的显示状态，如果未打开则加载指定组件。
         * @param component 要加载的组件
         * @param title 面板标题
         * @param props 传递给组件的 props
         */
        toggleLeftPanel(component: Component, title: string, props?: Record<string, any>) {
            // 情况 1: 左侧已打开且显示的是当前要切换的组件 -> 关闭 (如果未锁定)
            if (this.isLeftPanelOpen && this.activeLeftPanel.component === component) {
                this.closeLeftPanel();
            }
            // 情况 2: 左侧未打开，或打开的不是当前组件 -> 打开/切换到新组件
            else {
                // 如果打开的是其他组件，先关闭（如果未锁定）
                if (this.isLeftPanelOpen && !this.isLeftPanelPinned) {
                    this.closeLeftPanel();
                    // 需要一点延迟确保关闭动画后再打开新的，或者 openLeftPanel 内部处理
                    setTimeout(() => {
                        this.openLeftPanel(component, title, props);
                        // 考虑是否自动关闭右侧未锁定面板
                        if (!this.isRightPanelPinned) this.closeRightPanel();
                    }, this.isLeftPanelOpen ? 310 : 0); // 如果之前是打开的，稍微延迟
                } else if (!this.isLeftPanelOpen) {
                    this.openLeftPanel(component, title, props);
                    // 考虑是否自动关闭右侧未锁定面板
                    if (!this.isRightPanelPinned) this.closeRightPanel();
                } else { // 已打开且已锁定，但请求打开不同组件 -> 替换内容
                    this.openLeftPanel(component, title, props);
                    if (!this.isRightPanelPinned) this.closeRightPanel();
                }
            }
        },
        /**
         * 切换右侧面板的显示状态，如果未打开则加载指定组件。
         * @param component 要加载的组件
         * @param title 面板标题
         * @param props 传递给组件的 props
         */
        toggleRightPanel(component: Component, title: string, props?: Record<string, any>) {
            if (this.isRightPanelOpen && this.activeRightPanel.component === component) {
                this.closeRightPanel();
            } else {
                if (this.isRightPanelOpen && !this.isRightPanelPinned) {
                    this.closeRightPanel();
                    setTimeout(() => {
                        this.openRightPanel(component, title, props);
                        if (!this.isLeftPanelPinned) this.closeLeftPanel();
                    }, this.isRightPanelOpen ? 310 : 0);
                } else if (!this.isRightPanelOpen) {
                    this.openRightPanel(component, title, props);
                    if (!this.isLeftPanelPinned) this.closeLeftPanel();
                } else {
                    this.openRightPanel(component, title, props);
                    if (!this.isLeftPanelPinned) this.closeLeftPanel();
                }
            }
        },


        // --- Toolbar 调用的具体面板方法 (现在调用 toggle) ---
        showEntityList() {
            this.toggleLeftPanel(EntityListPanel, '实体列表');
        },
        showSettings() {
            this.toggleRightPanel(SettingsPanel, '设置');
        },
        showGameStateEditor() {
            this.toggleLeftPanel(GameStatePanel, '游戏状态');
        },
        // ... 其他面板的打开方法
    }
});