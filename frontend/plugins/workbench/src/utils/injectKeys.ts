// src/app-workbench/utils/injectionKeys.ts

// 定义选中项的类型
import type {InjectionKey, Ref} from "vue";
import type {TuumEditorContext} from "#/components/tuum/editor/TuumEditorContext.ts";
import type {RuneEditorContext} from "#/components/rune/editor/RuneEditorContext.ts";

export interface SelectedConfigItem
{
    readonly data: Ref<TuumEditorContext | RuneEditorContext | null>;
    update: (item: TuumEditorContext | RuneEditorContext) => void;
}

export const SelectedConfigItemKey: InjectionKey<SelectedConfigItem> = Symbol('selectedConfigItem'); // 提供一个函数来更新

/**
 * 用于在配置项组件树中传播父容器禁用状态的 InjectionKey。
 * 它提供的是一个 Ref<boolean>，确保响应性。
 */
export const IsParentDisabledKey: InjectionKey<Ref<boolean>> = Symbol('IsParentDisabledKey');