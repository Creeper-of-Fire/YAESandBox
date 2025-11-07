// useContextMenu.tsx
import {nextTick, ref, type Ref, type VNode} from 'vue';
import {type DropdownOption, NDropdown} from 'naive-ui';
import {onLongPress, type OnLongPressOptions, watchDebounced} from '@vueuse/core';

// 我们可以扩展 Naive UI 的类型，让每个选项可以直接携带一个处理函数
export type ContextMenuOption = DropdownOption & {
    onClick?: () => void;
}

export interface UseContextMenuOptions
{
    /**
     * 自动触发菜单的事件类型。
     * - 'contextmenu': 监听鼠标右键点击。
     * - 'longpress': 监听长按事件。
     * 默认为 ['contextmenu']。如果传入空数组，则只支持手动调用 showMenu。
     */
    triggerOn?: ('contextmenu' | 'longpress')[];

    /**
     * onLongPress 的配置项，例如 { delay: 500 }。
     * 仅在 triggerOn 包含 'longpress' 时生效。
     */
    longpressOptions?: OnLongPressOptions;

    /**
     * 当菜单即将显示时触发的回调函数。
     * 这提供了一个在菜单显示前执行自定义逻辑的入口。
     * @param event 触发菜单的原始事件对象
     */
    onShow?: (event: MouseEvent | PointerEvent) => void;
}

/**
 * 一个功能完备的右键菜单 Composable。
 * @param options - 菜单项的响应式引用。
 * @param config - 配置对象，用于定义自动触发的行为。
 * @returns { setTriggerRef, showMenu, hideMenu, ContextMenu }
 */
export function useContextMenu(
    options: Ref<ContextMenuOption[]>,
    config: UseContextMenuOptions = {}
)
{
    // 内部状态管理
    const show = ref(false);
    const x = ref(0);
    const y = ref(0);

    // 内部私有的 ref，用于存储目标元素
    const triggerElRef = ref<HTMLElement | null>(null);

    // 触发函数 (暴露给外部使用)
    const showMenu = (event: MouseEvent) =>
    {
        event.preventDefault();
        if (config.onShow) {
            config.onShow(event);
        }
        show.value = false; // 先隐藏，确保 nextTick 能重新渲染
        nextTick().then(() =>
        {
            show.value = true;
            x.value = event.clientX;
            y.value = event.clientY;
        });
    };

    const hideMenu = () =>
    {
        show.value = false;
    };

    // --- 自动事件绑定 ---
    // onLongPress 是响应式的，它会观察 triggerElRef 的变化自动绑定/解绑
    const triggers = config.triggerOn ?? [];
    if (triggers.includes('longpress'))
    {
        onLongPress(triggerElRef, showMenu, config.longpressOptions);
    }

    // 对于 contextmenu，我们需要手动处理
    // 使用 watchDebounced 是为了确保在 DOM 更新后只执行一次绑定
    watchDebounced(triggerElRef, (el, oldEl) =>
    {
        if (oldEl && triggers.includes('contextmenu'))
        {
            oldEl.removeEventListener('contextmenu', showMenu);
        }
        if (el && triggers.includes('contextmenu'))
        {
            el.addEventListener('contextmenu', showMenu);
        }
    }, {debounce: 10, immediate: true});

    // 返回一个 TSX 功能组件
    const ContextMenu = (): VNode => (
        <NDropdown
            show={show.value}
            options={options.value}
            x={x.value}
            y={y.value}
            trigger="manual"
            placement="bottom-start"
            onClickoutside={hideMenu}
            onSelect={(key: string, option: ContextMenuOption) =>
            {
                // 当选项被选中时，隐藏菜单
                hideMenu();
                // 如果选项上直接定义了 onClick 方法，则执行它
                // 这是比通过 key 查找更直接、类型安全的方式
                if (option.onClick)
                {
                    option.onClick();
                }
            }}
        />
    );

    /**
     * 这是提供给外部的函数 ref。
     * 它接收组件实例或 DOM 元素，并将其存入内部的 triggerElRef。
     * @param el - Vue 传递的元素或组件实例
     */
    const setTriggerRef = (el: object | null) => {
        // 处理 Vue 组件实例 (el.$el) 和普通 HTML 元素
        triggerElRef.value = (el as any)?.$el ?? el as (HTMLElement | null);
    };

    return {
        // 函数 ref setter
        setTriggerRef,
        // 手动控制方法
        showMenu,
        hideMenu,
        // 返回 TSX 组件用于渲染
        ContextMenu,
    };
}