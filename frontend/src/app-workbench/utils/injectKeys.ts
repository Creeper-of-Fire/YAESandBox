// src/app-workbench/utils/injectionKeys.ts

// 定义选中项的类型
import type {InjectionKey, Ref} from "vue";
import type {StepEditorContext} from "@/app-workbench/components/editor/StepEditorContext.ts";
import type {ModuleEditorContext} from "@/app-workbench/components/editor/ModuleEditorContext.ts";

export interface SelectedConfigItem
{
    readonly data: Ref<StepEditorContext | ModuleEditorContext | null>;
    update: (item: StepEditorContext | ModuleEditorContext) => void;
}

export const SelectedConfigItemKey: InjectionKey<SelectedConfigItem> = Symbol('selectedConfigItem'); // 提供一个函数来更新