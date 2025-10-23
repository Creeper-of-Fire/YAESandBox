import {nextTick, ref, type VNode, watch} from "vue";
import {
    type AlertProps,
    type ButtonProps,
    type InputInst, type ModalReactive, NAlert,
    NButton,
    NCard,
    NFlex,
    NFormItem,
    NInput,
    NTreeSelect,
    type TreeSelectOption
} from "naive-ui";
import {useScopedStorage} from "@yaesandbox-frontend/core-services/composables";
import type {ModalApiInjection} from "naive-ui/es/modal/src/ModalProvider";
import type {EnhancedAction} from "#/components/share/itemActions/useConfigItemActions.tsx";

/**
 * 渲染一个带输入框的对话框内容
 * @param props 配置项
 */
export const InputContent = (props: {
    title: string;
    label: string;
    initialValue: string;
    onConfirm: (value: string) => void;
    onCancel: () => void;
}) =>
{
    const inputValue = ref(props.initialValue);
    const inputRef = ref<InputInst | null>(null);
    nextTick(() => inputRef.value?.focus());

    const handleConfirm = () =>
    {
        const finalValue = inputValue.value.trim();
        if (finalValue)
        {
            props.onConfirm(finalValue);
        }
    };

    return (
        <NCard title={props.title} bordered={false} size="small" style={{width: '300px'}}>
            <NFormItem label={props.label} required>
                <NInput ref={inputRef} v-model:value={inputValue.value}
                        onKeydown={(e: KeyboardEvent) => e.key === 'Enter' && (e.preventDefault(), handleConfirm())}/>
            </NFormItem>
            <NFlex justify="end">
                <NButton size="small" onClick={props.onCancel}>取消</NButton>
                <NButton size="small" type="primary" disabled={!inputValue.value.trim()} onClick={handleConfirm}>确认</NButton>
            </NFlex>
        </NCard>
    );
};

/**
 * 渲染一个带树选择和输入框的对话框内容
 */
export const SelectAndInputContent = (props: {
    title: string;
    selectOptions: TreeSelectOption[];
    selectPlaceholder: string;
    nameGenerator: (type: any, opts: TreeSelectOption[]) => string;
    onConfirm: (payload: { type: string, name: string }) => void;
    onCancel: () => void;
}) =>
{
    const selectedType = ref<string | null>(null);
    const nameValue = ref('');
    const expandedKeys = useScopedStorage<string[]>('tree-expanded-keys', []);

    watch(selectedType, (newType) =>
    {
        if (newType)
        {
            nameValue.value = props.nameGenerator(newType, props.selectOptions);
        }
        else
        {
            nameValue.value = '';
        }
    });

    if (expandedKeys.value.length === 0 && props.selectOptions)
    {
        expandedKeys.value = props.selectOptions.filter(o => o.children?.length).map(o => o.key as string);
    }

    const handleConfirm = () =>
    {
        if (selectedType.value && nameValue.value.trim())
        {
            props.onConfirm({type: selectedType.value, name: nameValue.value.trim()});
        }
    };

    return (
        <NCard title={props.title} bordered={false} size="small" style={{width: '300px'}}>
            <NFormItem label="类型" required>
                <NTreeSelect
                    v-model:value={selectedType.value} v-model:expanded-keys={expandedKeys.value}
                    options={props.selectOptions} placeholder={props.selectPlaceholder}
                    block-line clearable filterable
                    overrideDefaultNodeClickBehavior={({option}) => (option.children ? 'toggleExpand' : 'default')}
                />
            </NFormItem>
            {selectedType.value && (
                <NFormItem label="名称" required>
                    <NInput v-model:value={nameValue.value}
                            onKeydown={(e: KeyboardEvent) => e.key === 'Enter' && (e.preventDefault(), handleConfirm())}/>
                </NFormItem>
            )}
            <NFlex justify="end">
                <NButton size="small" onClick={props.onCancel}>取消</NButton>
                <NButton size="small" type="primary" disabled={!selectedType.value || !nameValue.value.trim()}
                         onClick={handleConfirm}>确认</NButton>
            </NFlex>
        </NCard>
    );
};

/**
 * @description 一个通用的确认对话框内容组件
 */
export const ConfirmContent = (props: {
    title: string;
    message: string;
    confirmText?: string;
    cancelText?: string;
    confirmButtonType?: ButtonProps['type'];
    alertType?: AlertProps['type'];
    showAlertIcon?: boolean;
    onConfirm: () => void;
    onCancel: () => void;
}) =>
{
    const {
        title,
        message,
        onConfirm,
        onCancel,
        confirmText = '确认',
        cancelText = '取消',
        confirmButtonType = 'primary',
        alertType = 'warning',
        showAlertIcon = true,
    } = props;

    // 内部状态，用于处理异步 onConfirm 的加载效果
    const isLoading = ref(false);

    const handleConfirm = async () =>
    {
        if (isLoading.value) return; // 防止重复点击

        isLoading.value = true;
        try
        {
            await onConfirm();
        } finally
        {
            // 无论成功或失败，都结束加载状态
            isLoading.value = false;
        }
    };

    return (
        <NCard title={title} bordered={false} size="small" style={{width: '300px'}}>
            <NAlert showIcon={showAlertIcon} type={alertType} style={{marginBottom: '12px'}}>
                {message}
            </NAlert>
            <NFlex justify="end">
                <NButton size="small" onClick={onCancel} disabled={isLoading.value}>
                    {cancelText}
                </NButton>
                <NButton
                    size="small"
                    type={confirmButtonType}
                    onClick={handleConfirm}
                    loading={isLoading.value}
                    disabled={isLoading.value}
                >
                    {confirmText}
                </NButton>
            </NFlex>
        </NCard>
    );
};

/**
 * @description 创建一个类似 Popover 的模态框的辅助函数
 * @param modal - useModal() 的返回值
 * @param triggerElement - 用于定位的 HTML 元素
 * @param contentRenderer - 渲染卡片内容的 TSX 函数
 */
export const createPopoverModal = (
    modal: ModalApiInjection,
    triggerElement: HTMLElement,
    contentRenderer: (modalRef: ModalReactive) => VNode
) =>
{
    const rect = triggerElement.getBoundingClientRect();
    // 确保弹窗不会超出屏幕右侧或底部
    const left = Math.min(rect.left, window.innerWidth - 320 - 16);
    const top = Math.min(rect.bottom + 4, window.innerHeight - 200 - 16); // 假设弹窗最大高度

    const modalInstance = modal.create({
        title: undefined,
        preset: 'card',
        content: () => contentRenderer(modalInstance),
        maskClosable: true,
        style: {
            position: 'fixed',
            top: `${top}px`,
            left: `${left}px`,
            width: '320px',
            margin: '0',
        },
        bordered: false,
        closable: false,
        headerStyle: {display: 'none'},
        contentStyle: {padding: '0'},
        displayDirective: 'if'
    });

    return modalInstance;
};

/**
 * @description 一个高阶函数，用于创建打开模态框的 `activate` 方法。
 * 这样可以避免在每个 Action 定义中重复编写 `createPopoverModal` 的调用逻辑。
 * @param modal - Naive UI 的 modal 服务
 * @param contentRenderer - 一个函数，它接收 modal 实例并返回要渲染的 VNode 内容。
 *                          `onConfirm` 等回调现在应该负责在成功后调用 `modalRef.destroy()`。
 */
export const createModalActionActivator = (
    modal: ModalApiInjection,
    contentRenderer: (modalRef: ModalReactive) => VNode
): EnhancedAction['activate'] =>
{
    return (triggerElement) =>
    {
        createPopoverModal(modal, triggerElement, contentRenderer);
    };
};