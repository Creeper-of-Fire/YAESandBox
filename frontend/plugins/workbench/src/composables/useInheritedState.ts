// src/composables/useInheritedState.ts

import {computed, type ComputedRef, inject, type InjectionKey, isRef, provide, ref, type Ref} from 'vue';

// 在 Composable 内部定义 InjectionKey。
// 它不会被导出，从而实现了细节的封装。
const IsParentDisabledKey: InjectionKey<Ref<boolean>> = Symbol('IsParentDisabledKey');

/**
 * 【消费者/提供者】
 * 一个可组合函数，用于处理继承性的禁用状态。
 * 它会从父级注入禁用状态，结合自身的启用状态，计算出最终的有效禁用状态，
 * 并将这个新状态提供给所有子组件。
 *
 * @param isSelfEnabled - 组件自身的启用状态。可以是一个 Ref<boolean>，或一个返回 boolean 的 getter 函数。
 * @returns 返回一个计算属性 `isEffectivelyDisabled`，表示最终的禁用状态。
 */
export function useInheritedDisableState(isSelfEnabled: Ref<boolean> | (() => boolean)): {
    isEffectivelyDisabled: ComputedRef<boolean>
}
{
    // 从父级注入状态，如果没有提供者，则默认为 false (不禁用)。
    const isParentDisabled = inject(IsParentDisabledKey, ref(false));

    // 如果传入的是 getter 函数，就用 computed 包裹它，得到一个 Ref。
    // 如果传入的本身就是 Ref，则直接使用。
    const selfEnabledRef = isRef(isSelfEnabled)
        ? isSelfEnabled
        : computed(isSelfEnabled);

    // 计算出最终的有效禁用状态。逻辑是：自身被禁用 或 父级被禁用。
    const isEffectivelyDisabled = computed(() => !selfEnabledRef.value || isParentDisabled.value);

    // 将计算出的新状态提供给子组件。
    provide(IsParentDisabledKey, isEffectivelyDisabled);

    // 返回计算结果，供当前组件使用。
    return {
        isEffectivelyDisabled,
    };
}

/**
 * 【纯消费者】
 * 一个简化的可组合函数，仅用于注入和返回父级的禁用状态。
 * 适用于那些自身没有启用/禁用状态，但其外观受父级影响的组件。
 *
 * @returns 返回一个包含 isParentDisabled Ref 的对象。
 */
export function useParentDisabledState(): { isParentDisabled: Ref<boolean> }
{
    const isParentDisabled = inject(IsParentDisabledKey, ref(false));
    return {
        isParentDisabled
    }
}