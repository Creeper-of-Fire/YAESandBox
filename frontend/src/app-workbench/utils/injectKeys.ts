// src/app-workbench/utils/injectionKeys.ts

// 定义选中项的类型
import type {InjectionKey, Ref} from "vue";
import type {TuumEditorContext} from "@/app-workbench/components/tuum/editor/TuumEditorContext.ts";
import type {RuneEditorContext} from "@/app-workbench/components/rune/editor/RuneEditorContext.ts";

export interface SelectedConfigItem
{
    readonly data: Ref<TuumEditorContext | RuneEditorContext | null>;
    update: (item: TuumEditorContext | RuneEditorContext) => void;
}

export const SelectedConfigItemKey: InjectionKey<SelectedConfigItem> = Symbol('selectedConfigItem'); // 提供一个函数来更新