// src/utils/injectionKeys.ts

// 定义选中项的类型
import type {InjectionKey, Ref} from "vue";

/**
 * 用于在应用中注入是否为暗黑主题的状态
 */
export const IsDarkThemeKey: InjectionKey<Ref<boolean>> = Symbol('IsDarkThemeKey');
