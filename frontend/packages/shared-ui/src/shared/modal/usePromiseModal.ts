import {type ModalOptions, useModal} from 'naive-ui';
import {type VNode} from 'vue';
import {ModalResult} from './ModalResult.ts'; // 引入我们创建的 ModalResult 类

/**
 * 基于 Promise 的模态框选项。
 * `content` 和 `action` 的渲染函数现在接收 `resolve` 和 `cancel` 控制器。
 */
export type PromiseModalOptions<T> = Omit<ModalOptions, 'content' | 'action' | 'onUpdate:show'> & {
    /**
     * 自定义内容区域的渲染函数。
     * @param resolve 关闭模态框并使其返回一个成功的 `ModalResult.ok(data)`。
     * @param cancel 关闭模态框并使其返回一个取消的 `ModalResult.cancel()`。
     * @returns 返回一个 VNode。
     */
    content: (resolve: (data: T) => void, cancel: () => void) => VNode;

    /**
     * 自定义操作区域的渲染函数 (可选)。
     * @param resolve 关闭模态框并使其返回一个成功的 `ModalResult.ok(data)`。
     * @param cancel 关闭模态框并使其返回一个取消的 `ModalResult.cancel()`。
     * @returns 返回一个 VNode。
     */
    action?: (resolve: (data: T) => void, cancel: () => void) => VNode;
};

/**
 * 一个通用的、基于 Promise 的命令式模态框 Composable。
 * 调用 `open` 方法会返回一个 Promise，该 Promise 在模态框关闭时
 * 会 resolve 一个 ModalResult 实例。
 */
export function usePromiseModal() {
    const modal = useModal();

    function open<T>(options: PromiseModalOptions<T>): Promise<ModalResult<T>> {
        // 返回一个 Promise，这是改造的核心
        return new Promise((promiseResolve) => {
            let modalInstance: ReturnType<typeof modal.create> | null = null;

            // 当需要以 "ok" 状态关闭时调用
            const handleResolve = (data: T) => {
                promiseResolve(ModalResult.ok(data));
                modalInstance?.destroy();
            };

            // 当需要以 "cancel" 状态关闭时调用
            const handleCancel = () => {
                promiseResolve(ModalResult.cancel());
                modalInstance?.destroy();
            };

            const modalConfig: ModalOptions = {
                preset: 'card',
                style: {width: '90vw', maxWidth: '600px'},
                maskClosable: true,
                closeOnEsc: true,
                ...options,

                // 将 Naive UI 的关闭事件（如点击遮罩、按 ESC）映射到我们的 handleCancel
                onClose: handleCancel,

                content: () => options.content(handleResolve, handleCancel),
                action: options.action ? () => options.action!(handleResolve, handleCancel) : undefined,
            };

            modalInstance = modal.create(modalConfig);
        });
    }

    return {open};
}