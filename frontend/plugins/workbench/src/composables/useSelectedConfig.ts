import {inject, provide, ref, type InjectionKey, type Ref} from "vue";
import type {TuumEditorContext} from "#/components/tuum/editor/TuumEditorContext.ts";
import type {RuneEditorContext} from "#/components/rune/editor/RuneEditorContext.ts";
import type {WorkflowEditorContext} from "#/components/workflow/editor/WorkflowEditorContext.ts";

export interface SelectedConfigItem {
    readonly data: Ref<WorkflowEditorContext | TuumEditorContext | RuneEditorContext | null>;
    update: (item: WorkflowEditorContext | TuumEditorContext | RuneEditorContext | null) => void;
}

export const SelectedConfigItemKey: InjectionKey<SelectedConfigItem> = Symbol('selectedConfigItem');

/**
 * @description 在父组件中提供 selectedConfig 状态。
 * 这个函数应该在顶层组件（如 WorkbenchView）中被调用一次。
 */
export function createSelectedConfigProvider() {
    const selectedConfig = ref<WorkflowEditorContext | TuumEditorContext | RuneEditorContext | null>(null);

    const updateSelectedConfig = (config: WorkflowEditorContext | TuumEditorContext | RuneEditorContext | null) => {
        try {
            selectedConfig.value = config;
        } catch (e) {
            console.error("更新 selectedConfig 时出错:", e);
        }
    };

    provide<SelectedConfigItem>(SelectedConfigItemKey, {
        data: selectedConfig,
        update: updateSelectedConfig,
    });

    // 将状态和更新方法返回，以便提供者组件自身也能使用
    return {
        selectedConfig,
        updateSelectedConfig
    }
}

/**
 * @description 在子组件中使用 selectedConfig 状态。
 * @returns an object with the selected configuration ref and the update function.
 */
export function useSelectedConfig() {
    const selectedConfigItem = inject(SelectedConfigItemKey);

    if (!selectedConfigItem) {
        throw new Error('useSelectedConfig() 必须在 createSelectedConfigProvider() 的后代组件中使用。');
    }

    return {
        selectedConfig: selectedConfigItem.data,
        updateSelectedConfig: selectedConfigItem.update,
    };
}