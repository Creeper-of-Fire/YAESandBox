// 文件路径: src/app-workbench/composables/useConfigItemActions.ts
import {type Component, computed, nextTick, ref, type Ref, type VNode, watch} from 'vue';
import {
    type InputInst,
    NAlert,
    NButton,
    NCard,
    NFlex,
    NFormItem,
    NInput,
    NTreeSelect,
    type TreeSelectOption,
    useMessage,
    useModal
} from 'naive-ui';
import {type AnyConfigObject, type ConfigType, getConfigObjectType, isRuneWithInnerTuum} from "@yaesandbox-frontend/core-services/types";
import {deepCloneWithNewIds, useWorkbenchStore} from '#/stores/workbenchStore.ts';
import {AddIcon, CopyIcon, EditIcon, SaveIcon, TrashIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {createBlankConfig} from "#/utils/createBlankConfig.ts";
import {useSelectedConfig} from "#/services/editor-context/useSelectedConfig.ts";
import {useRuneTypeSelector} from "#/composables/useRuneTypeSelector.ts";
import {useScopedStorage} from "@yaesandbox-frontend/core-services/composables";
import type {ModalApiInjection} from "naive-ui/es/modal/src/ModalProvider";

/**
 * @description useConfigItemActions 的输入参数类型
 */
interface UseConfigItemActionsParams
{
    itemRef: Ref<AnyConfigObject | null>;
    parentContextRef: Ref<{ parent: AnyConfigObject; list: AnyConfigObject[] } | null>;
}

export type ActionsProvider = () => EnhancedAction[];

export interface EnhancedAction
{
    /**
     * @description 唯一标识符
     */
    key: string;
    /**
     * @description 显示在菜单项中的标签
     */
    label: string;
    /**
     * @description 显示在菜单项中的图标 (组件)
     */
    icon?: Component;
    /**
     * @description 是否禁用此动作
     */
    disabled?: boolean;
    /**
     * @description 作为按钮/菜单选项的样式类型
     */
    type?: 'default' | 'primary' | 'info' | 'success' | 'warning' | 'error';
    /**
     * @description 激活此动作。
     * 该方法负责呈现所有必要的UI（如对话框、popover等）并执行最终的业务逻辑。
     * @param triggerElement 触发此动作的DOM元素，用于UI定位
     */
    activate: (triggerElement: HTMLElement) => void;
}

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

// 确认删除内容
// TODO 改成更通用的
export const ConfirmContent = (props: {
    title: string;
    message: string;
    onConfirm: () => void;
    onCancel: () => void;
}) => (
    <NCard title={props.title} bordered={false} size="small" style={{width: '300px'}}>
        <NAlert showIcon={false} type="warning" style={{marginBottom: '12px'}}>{props.message}</NAlert>
        <NFlex justify="end">
            <NButton size="small" onClick={props.onCancel}>取消</NButton>
            <NButton size="small" type="error" onClick={props.onConfirm}>确认删除</NButton>
        </NFlex>
    </NCard>
);

/**
 * @description 创建一个类似 Popover 的模态框的辅助函数
 * @param modal - useModal() 的返回值
 * @param triggerElement - 用于定位的 HTML 元素
 * @param contentRenderer - 渲染卡片内容的 TSX 函数
 */
const createPopoverModal = (
    modal: ModalApiInjection,
    triggerElement: HTMLElement,
    contentRenderer: (modalRef: { destroy: () => void }) => VNode
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
 * @description 一个可组合函数，用于生成针对特定配置项的可用动作列表
 * @param params 包含当前项、会话和父级上下文的响应式引用
 */
export function useConfigItemActions({itemRef, parentContextRef}: UseConfigItemActionsParams): { actions: Ref<EnhancedAction[]>; }
{
    const {isReadOnly: isReadOnlyRef} = useSelectedConfig();
    const message = useMessage();
    const modal = useModal();
    const workbenchStore = useWorkbenchStore();

    const {runeTypeOptions, runeDefaultNameGenerator} = useRuneTypeSelector();


    /**
     * @description 生成并返回当前状态下的可用动作列表。
     */
    const actions = computed<EnhancedAction[]>(() =>
    {
        const isReadOnly = isReadOnlyRef.value;
        // 从闭包中获取最新的 ref 值
        const item = itemRef.value;
        const parentCtx = parentContextRef.value;

        // 如果没有 item，直接返回空数组
        if (!item) return [];

        const {type: itemType, config: typedItem} = getConfigObjectType(item);

        // 动作：重命名
        const renameAction: EnhancedAction = {
            key: 'rename',
            label: '重命名',
            icon: EditIcon,
            disabled: isReadOnly || !item,
            activate: (trigger) => createPopoverModal(modal, trigger, (modalRef) =>
                <InputContent
                    title="重命名" label="新名称" initialValue={item.name ?? ''}
                    onCancel={() => modalRef.destroy()}
                    onConfirm={(newName) =>
                    {
                        item.name = newName;
                        message.success(`已重命名为 "${newName}"`);
                        modalRef.destroy();
                    }}/>),
        }

        const getAddChildAction = (): EnhancedAction =>
        {
            // 默认隐藏动作
            let action: EnhancedAction = {key: 'add-child', label: '添加子项', disabled: true, activate: () => {}};

            const createAndPushTuum = async (name: string) =>
            {
                const newTuum = await createBlankConfig('tuum', name);
                if (itemType === 'workflow')
                {
                    typedItem.tuums.push(newTuum);
                    message.success('已添加新枢机');
                }
            };

            const createAndPushRune = async (payload: { name: string, type: string }) =>
            {
                const newRune = await createBlankConfig('rune', payload.name, {runeType: payload.type});
                if (isRuneWithInnerTuum(item) && item.innerTuum)
                {
                    item.innerTuum.runes.push(newRune);
                    message.success('已添加新符文到子枢机');
                }
                else if (itemType === 'tuum')
                {
                    typedItem.runes.push(newRune);
                    message.success('已添加新符文');
                }
            };

            if (itemType == 'workflow')
            { // 工作流添加枢机
                action = {
                    key: 'add-tuum',
                    label: '添加枢机',
                    icon: AddIcon,
                    disabled: isReadOnly,
                    activate: (trigger) => createPopoverModal(modal, trigger, (modalRef) =>
                        <InputContent
                            title="添加新枢机" label="名称" initialValue="新枢机"
                            onCancel={() => modalRef.destroy()}
                            onConfirm={async (name) =>
                            {
                                await createAndPushTuum(name);
                                modalRef.destroy();
                            }}/>
                    ),
                };
            }
            else if (isRuneWithInnerTuum(item) || itemType === 'tuum')
            { // 枢机添加符文
                action = {
                    key: 'add-rune',
                    label: '添加符文',
                    icon: AddIcon,
                    disabled: isReadOnly,
                    activate: (trigger) => createPopoverModal(modal, trigger, (modalRef) =>
                        <SelectAndInputContent
                            title="添加新符文" selectOptions={runeTypeOptions.value} selectPlaceholder="请选择符文类型"
                            nameGenerator={runeDefaultNameGenerator}
                            onCancel={() => modalRef.destroy()}
                            onConfirm={async (payload) =>
                            {
                                await createAndPushRune(payload);
                                modalRef.destroy();
                            }}/>
                    ),
                };
            }
            return action;
        }

        // 动作：创建副本
        const duplicateAction: EnhancedAction = {
            key: 'duplicate',
            label: '创建副本',
            icon: CopyIcon,
            disabled: isReadOnly || !parentCtx, // 如果是只读或没有父级上下文，则禁用
            activate: () =>
            {
                if (!parentCtx || !item || !('configId' in item)) return;

                const index = parentCtx.list.findIndex(i => 'configId' in i && i.configId === item.configId);
                if (index > -1)
                {
                    // 使用 deepCloneWithNewIds 创建一个全新的副本
                    const clonedItem = deepCloneWithNewIds(item);
                    // 修改副本名称以作区分
                    clonedItem.name = `${item.name} (副本)`;
                    // 将副本插入到原项的后面
                    parentCtx.list.splice(index + 1, 0, clonedItem);

                    message.success(`已为“${item.name}”创建副本`);
                }
            }
        }

        // 动作：删除
        const deleteAction: EnhancedAction = {
            key: 'delete',
            label: '删除',
            icon: TrashIcon,
            type: 'error',
            disabled: isReadOnly || !parentCtx,
            activate: (trigger) => createPopoverModal(modal, trigger, (modalRef) =>
                <ConfirmContent
                    title="确认删除" message={`你确定要删除“${item.name}”吗？此操作不可恢复。`}
                    onCancel={() => modalRef.destroy()}
                    onConfirm={() =>
                    {
                        if (!parentCtx || !('configId' in item)) return;
                        const index = parentCtx.list.findIndex(i => 'configId' in i && i.configId === item.configId);
                        if (index > -1)
                        {
                            parentCtx.list.splice(index, 1);
                            message.success(`已删除“${item.name}”`);
                        }
                        modalRef.destroy();
                    }}/>
            ),
        }

        // 动作：另存为全局配置
        const saveAsGlobalAction: EnhancedAction = {
            key: 'save-as-global',
            label: '另存为全局',
            icon: SaveIcon,
            type: 'success',
            disabled: !item,
            activate: (trigger) => createPopoverModal(modal, trigger, (modalRef) =>
                <InputContent
                    title="另存为全局配置" label="全局配置名称" initialValue={`${item.name} (全局副本)`}
                    onCancel={() => modalRef.destroy()}
                    onConfirm={async (name) =>
                    {
                        try
                        {
                            await workbenchStore.createGlobalConfig({...item, name});
                            message.success(`已成功将“${name}”保存为全局配置！`);
                        } catch (e)
                        {
                            message.error(`保存失败: ${(e as Error).message}`);
                        }
                        modalRef.destroy();
                    }}/>
            ),
        }

        return [
            renameAction,
            getAddChildAction(),
            deleteAction,
            saveAsGlobalAction,
            duplicateAction,
        ].filter(a => a.key && !a.disabled); // 过滤掉无效和禁用的动作
    });

    return {
        actions
    };
}

/**
 * @description 为全局配置列表生成 "新建" 动作
 * @param activeTab 当前激活的标签页类型 ('rune', 'workflow', 'tuum')
 * @param currentTabLabel 当前标签页的显示名称 (例如 "符文")
 * @param emit 组件的 emit 函数，用于触发事件
 */
export function useGlobalConfigCreationAction(
    activeTab: Ref<ConfigType>,
    currentTabLabel: Ref<string>,
    emit: (event: 'start-editing', payload: { type: ConfigType, storeId: string }) => void
): { createNewAction: Ref<EnhancedAction> }
{
    const message = useMessage();
    const modal = useModal();
    const workbenchStore = useWorkbenchStore();
    const {runeTypeOptions, runeDefaultNameGenerator} = useRuneTypeSelector();

    /**
     * @description 处理创建逻辑的核心函数
     */
    const handleCreateNew = async (payload: { name: string; type?: string }) =>
    {
        const { name, type: runeType } = payload;
        const resourceType = activeTab.value;

        // 验证逻辑
        if (!name) {
            message.error('请输入有效的名称');
            return;
        }
        if (resourceType === 'rune' && !runeType) {
            message.error('创建符文时必须选择符文类型');
            return;
        }

        try {
            const blankConfig =
                resourceType === 'rune'
                    ? await createBlankConfig('rune', name, { runeType: runeType! })
                    : resourceType === 'workflow'
                        ? await createBlankConfig('workflow', name)
                        : await createBlankConfig('tuum', name);

            const newSession = workbenchStore.createNewDraftSession(resourceType, blankConfig);
            emit('start-editing', { type: newSession.type, storeId: newSession.storeId });

            message.success(`成功创建全局${currentTabLabel.value}“${name}”！`);
            return true; // 表示成功
        } catch (e) {
            message.error(`创建失败: ${(e as Error).message}`);
            return false; // 表示失败
        }
    };

    const createNewAction = computed<EnhancedAction>(() => ({
        key: 'create-new-global',
        icon: AddIcon,
        label: `新建全局${currentTabLabel.value}`,
        disabled: false,
        activate: (triggerElement) =>
        {
            const title = `新建全局${currentTabLabel.value}`;

            if (activeTab.value === 'rune')
            {
                // 符文需要选择类型
                createPopoverModal(modal, triggerElement, (modalRef) =>
                    <SelectAndInputContent
                        title={title}
                        selectOptions={runeTypeOptions.value}
                        selectPlaceholder="请选择符文类型"
                        nameGenerator={runeDefaultNameGenerator}
                        onCancel={() => modalRef.destroy()}
                        onConfirm={async (payload) => {
                            const success = await handleCreateNew(payload);
                            if (success) modalRef.destroy();
                        }}
                    />
                );
            }
            else
            {
                // 工作流和枢机只需要输入名称
                createPopoverModal(modal, triggerElement, (modalRef) =>
                    <InputContent
                        title={title}
                        label="名称"
                        initialValue={`新建${currentTabLabel.value}`}
                        onCancel={() => modalRef.destroy()}
                        onConfirm={async (name) => {
                            const success = await handleCreateNew({ name });
                            if (success) modalRef.destroy();
                        }}
                    />
                );
            }
        }
    }));

    return {
        createNewAction
    };
}