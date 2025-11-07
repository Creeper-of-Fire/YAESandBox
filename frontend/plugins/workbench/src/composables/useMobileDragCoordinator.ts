import {inject, type InjectionKey, provide, ref, type Ref} from 'vue';

// 定义注入的上下文类型
interface MobileDragContext
{
    notifyDragStart: () => void;
    notifyDragEnd: () => void;
}

// 创建一个唯一的 InjectionKey，确保类型安全
const MobileDragKey: InjectionKey<MobileDragContext> = Symbol('MobileDragCoordinator');

// --- "Provider" 部分 ---

/**
 * 在父组件 (EditorLayout) 中创建并提供拖拽协调上下文。
 * @param isMobile - 一个 ref，指示当前是否为移动端视图。
 * @returns {Ref<boolean>} - 一个 ref，用于控制抽屉的显示/隐藏状态。
 */
export function createMobileDragCoordinator(isMobile: Ref<boolean>): { isDrawerOpen: Ref<boolean> }
{

    // 这个状态由 Provider (EditorLayout) 持有
    const isDrawerOpen = ref(false);

    const notifyDragStart = () =>
    {
        // 只有在移动端视图下，拖拽开始时才隐藏抽屉
        if (isMobile.value)
        {
            isDrawerOpen.value = false;
        }
    };

    const notifyDragEnd = () =>
    {
        // 拖动结束后，重新显示抽屉，同样只在移动端生效
        // 注意：原代码的逻辑是在拖拽结束后重新打开，我们在此保留该行为。
        // 如果未来需求是“保持关闭”，只需注释掉下面这行即可。
        if (isMobile.value)
        {
            isDrawerOpen.value = true;
        }
    };

    // 提供上下文给所有后代组件
    provide(MobileDragKey, {
        notifyDragStart,
        notifyDragEnd,
    });

    // 将状态返回给 EditorLayout，以便它可以直接绑定到 <n-drawer>
    return {
        isDrawerOpen,
    };
}

// --- "Consumer" 部分 ---

/**
 * 在子组件 (GlobalResourcePanel) 中注入并使用拖拽协调器。
 * @returns {MobileDragContext} - 包含 notifyDragStart 和 notifyDragEnd 方法的对象。
 */
export function useMobileDragCoordinator(): MobileDragContext
{
    // 注入上下文。如果找不到，则提供一个安全的空函数作为回退。
    return inject(MobileDragKey, {
        notifyDragStart: () => {}, // 空函数，什么也不做
        notifyDragEnd: () => {},   // 空函数，什么也不做
    });
}