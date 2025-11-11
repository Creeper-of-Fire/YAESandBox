import {ref, type VNode} from 'vue';
import {type ModalOptions, NButton, NFlex, useMessage} from 'naive-ui';
import {usePromiseModal} from './usePromiseModal.ts';
import {ModalPromise} from "./ModalPromise.ts";

/**
 * 一个用于模态框操作的接口，定义了模态框的提交逻辑。
 */
export type SubmitExpose<T> = {
    submit: () => Promise<T | null>;
}

/**
 * useSaveableModal `open` 方法的选项。
 */
export interface SaveableModalOptions<T> extends Omit<ModalOptions, 'content' | 'action' | 'onUpdate:show' | 'onClose' | 'onNegativeClick'>
{
    /**
     * 模态框的标题。
     */
    title: string;

    /**
     * 模态框的主体内容，通常是一个表单组件。
     * @returns 返回一个 VNode。
     */
    content: () => VNode;

    /**
     * 当用户点击“保存”按钮时执行的异步函数。
     * - 如果函数成功 resolve 一个 T 类型的值，模态框将关闭。
     * - 如果函数 resolve `null`，表示操作失败（如验证未通过），模态框将不会关闭。
     * - 如果函数 reject 或抛出异常，将被视为未知错误，同样不会关闭并显示通用错误消息。
     * @returns 一个 Promise，解析为要保存的数据或 null。
     */
    onSave: () => Promise<T | null>;

    /**
     * “保存”按钮的文本 (可选, 默认为 "保存")。
     */
    saveText?: string;

    /**
     * “取消”按钮的文本 (可选, 默认为 "取消")。
     */
    cancelText?: string;

    /**
     * 当模态框被取消时执行的回调 (可选)。
     */
    onCancel?: () => void;
}

/**
 * 一个高度封装的、用于“保存/取消”场景的命令式模态框 Composable。
 * 它内置了按钮、加载状态和错误处理逻辑。
 */
export function useSaveableModal()
{
    const promiseModal = usePromiseModal();
    const message = useMessage(); // 用于在保存失败时给出提示

    /**
     * 打开一个可保存的模态框。
     * @param options 模态框的配置。
     * @returns 一个 Promise，在保存成功时 resolve 为数据 T，在取消时 resolve 为 undefined。
     */
    function open<T>(options: SaveableModalOptions<T>): ModalPromise<T>
    {
        return new ModalPromise<T>(async (resolve) =>
        {
            const isLoading = ref(false);

            const {title, content, onSave, onCancel, saveText, cancelText, ...restOptions} = options;

            const result = await promiseModal.open<T>({
                title,
                // 默认设置为点击遮罩或按 ESC 不关闭，强制用户通过按钮进行选择
                maskClosable: false,
                closeOnEsc: false,
                // 允许用户覆盖默认配置
                ...restOptions,

                // content 函数直接透传
                content: () => content(),

                // action 是这个 Composable 的核心，它渲染按钮并管理状态
                action: (modalResolve, modalCancel) => (
                    <NFlex justify="end">
                        <NButton
                            onClick={modalCancel}
                            disabled={isLoading.value}
                        >
                            {cancelText ?? '取消'}
                        </NButton>
                        <NButton
                            type="primary"
                            loading={isLoading.value}
                            disabled={isLoading.value}
                            onClick={async () =>
                            {
                                isLoading.value = true;
                                try
                                {
                                    // 调用 onSave 并等待其结果
                                    const savedData = await onSave();
                                    // 如果返回了有效数据 (不是 null)，则成功
                                    if (savedData !== null)
                                    {
                                        modalResolve(savedData);
                                    }
                                    else
                                    {
                                        // 如果返回 null，我们什么都不做，模态框保持打开
                                        // 可以在这里加一个 debug log
                                        console.debug('onSave 返回 null，操作被中止，模态框保持打开。');
                                    }

                                } catch (error)
                                {
                                    // onSave 失败，模态框不关闭
                                    console.error('错误日志：onSave 操作中发生意外异常', error);
                                    message.error('保存失败，发生了未知错误。');
                                } finally
                                {
                                    isLoading.value = false;
                                }
                            }}
                        >
                            {saveText ?? '保存'}
                        </NButton>
                    </NFlex>
                ),
            });

            // 根据底层 promiseModal 的结果，处理回调并 resolve 最终的 ModalPromise
            if (result.isOk())
            {
                resolve(result.data);
            }
            else
            {
                onCancel?.();
                console.debug('用户取消了保存操作。');
                resolve(undefined);
            }
        });
    }

    return {open};
}