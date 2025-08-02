// src/app-workbench/utils/injectionKeys.ts

// 定义选中项的类型
import type {InjectionKey, Ref} from "vue";
import type {StepEditorContext} from "@/app-workbench/components/step/editor/StepEditorContext.ts";
import type {RuneEditorContext} from "@/app-workbench/components/rune/editor/RuneEditorContext.ts";

export interface SelectedConfigItem
{
    readonly data: Ref<StepEditorContext | RuneEditorContext | null>;
    update: (item: StepEditorContext | RuneEditorContext) => void;
}

export const SelectedConfigItemKey: InjectionKey<SelectedConfigItem> = Symbol('selectedConfigItem'); // 提供一个函数来更新