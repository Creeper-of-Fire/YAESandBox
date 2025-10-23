import {type Component, computed, type Ref} from 'vue';
import {type ButtonProps, type TreeSelectOption, useMessage, useModal} from 'naive-ui';
import {type AnyConfigObject, type ConfigType, getConfigObjectType, isRuneWithInnerTuum} from "@yaesandbox-frontend/core-services/types";
import {deepCloneWithNewIds, useWorkbenchStore} from '#/stores/workbenchStore.ts';
import {AddIcon, CopyIcon, EditIcon, SaveIcon, TrashIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {createBlankConfig} from "#/utils/createBlankConfig.ts";
import {useSelectedConfig} from "#/services/editor-context/useSelectedConfig.ts";
import {useRuneTypeSelector} from "#/composables/useRuneTypeSelector.ts";
import type {ModalApiInjection} from "naive-ui/es/modal/src/ModalProvider";
import {
    ConfirmContent,
    createModalActionActivator,
    InputContent,
    SelectAndInputContent
} from "#/components/share/itemActions/ConfigItemComponents.tsx";

/**
 * @description useConfigItemActions 的输入参数类型
 */
interface UseConfigItemActionsParams
{
    itemRef: Ref<AnyConfigObject | null>;
    parentContextRef: Ref<{ parent: AnyConfigObject; list: AnyConfigObject[] } | null>;
}

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
    type?: ButtonProps['type'];
    /**
     * @description 激活此动作。
     * 该方法负责呈现所有必要的UI（如对话框、popover等）并执行最终的业务逻辑。
     * @param triggerElement 触发此动作的DOM元素，用于UI定位
     */
    activate: (triggerElement: HTMLElement) => void;
}


type ActionCreatorContext = {
    item: AnyConfigObject;
    parentCtx: { parent: AnyConfigObject; list: AnyConfigObject[] } | null;
    isReadOnly: boolean;
    modal: ModalApiInjection;
    message: ReturnType<typeof useMessage>;
    workbenchStore: ReturnType<typeof useWorkbenchStore>;
    runeTypeOptions: Ref<TreeSelectOption[]>;
};

const createRenameAction = ({item, isReadOnly, modal, message}: ActionCreatorContext): EnhancedAction | null =>
{
    if (isReadOnly) return null;
    return {
        key: 'rename',
        label: '重命名',
        icon: EditIcon,
        activate: createModalActionActivator(modal, (modalRef) =>
            <InputContent
                title="重命名" label="新名称" initialValue={item.name ?? ''}
                onCancel={() => modalRef.destroy()}
                onConfirm={(newName) =>
                {
                    item.name = newName;
                    message.success(`已重命名为 "${newName}"`);
                    modalRef.destroy();
                }}/>
        ),
    };
};

const createAddChildAction = (ctx: ActionCreatorContext): EnhancedAction | null =>
{
    const {item, isReadOnly, modal, message, runeTypeOptions} = ctx;
    const {type: itemType, config: typedItem} = getConfigObjectType(item);
    const {runeDefaultNameGenerator} = useRuneTypeSelector();

    const createAndPushTuum = async (name: string) =>
    {
        if (itemType !== 'workflow') return;
        const newTuum = await createBlankConfig('tuum', name);
        typedItem.tuums.push(newTuum);
        message.success('已添加新枢机');
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

    if (itemType === 'workflow')
    {
        return {
            key: 'add-tuum',
            label: '添加枢机',
            icon: AddIcon,
            disabled: isReadOnly,
            activate: createModalActionActivator(modal, (modalRef) =>
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

    if (isRuneWithInnerTuum(item) || itemType === 'tuum')
    {
        return {
            key: 'add-rune',
            label: '添加符文',
            icon: AddIcon,
            disabled: isReadOnly,
            activate: createModalActionActivator(modal, (modalRef) =>
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

    return null; // 不可添加子项
};

const createDuplicateAction = ({item, parentCtx, isReadOnly, message}: ActionCreatorContext): EnhancedAction | null =>
{
    if (isReadOnly || !parentCtx) return null;
    return {
        key: 'duplicate',
        label: '创建副本',
        icon: CopyIcon,
        activate: () =>
        {
            if (!('configId' in item)) return;
            const index = parentCtx.list.findIndex(i => 'configId' in i && i.configId === item.configId);
            if (index > -1)
            {
                const clonedItem = deepCloneWithNewIds(item);
                clonedItem.name = `${item.name} (副本)`;
                parentCtx.list.splice(index + 1, 0, clonedItem);
                message.success(`已为“${item.name}”创建副本`);
            }
        },
    };
};

const createDeleteAction = ({item, parentCtx, isReadOnly, modal, message}: ActionCreatorContext): EnhancedAction | null =>
{
    if (isReadOnly || !parentCtx) return null;
    return {
        key: 'delete',
        label: '删除',
        icon: TrashIcon,
        type: 'error',
        activate: createModalActionActivator(modal, (modalRef) =>
            <ConfirmContent
                title="确认删除" message={`你确定要删除“${item.name}”吗？此操作不可恢复。`}
                onCancel={() => modalRef.destroy()}
                onConfirm={() =>
                {
                    if (!('configId' in item)) return;
                    const index = parentCtx.list.findIndex(i => 'configId' in i && i.configId === item.configId);
                    if (index > -1)
                    {
                        parentCtx.list.splice(index, 1);
                        message.success(`已删除“${item.name}”`);
                    }
                    modalRef.destroy();
                }}/>
        ),
    };
};

const createSaveAsGlobalAction = ({item, modal, message, workbenchStore}: ActionCreatorContext): EnhancedAction | null =>
{
    return {
        key: 'save-as-global',
        label: '另存为全局',
        icon: SaveIcon,
        type: 'success',
        activate: createModalActionActivator(modal, (modalRef) =>
            <InputContent
                title="另存为全局配置" label="全局配置名称" initialValue={`${item.name} (全局副本)`}
                onCancel={() => modalRef.destroy()}
                onConfirm={async (name) =>
                {
                    try
                    {
                        await workbenchStore.createGlobalConfig({...item, name});
                        message.success(`已成功将“${name}”保存为全局配置！`);
                        modalRef.destroy(); // 成功后关闭
                    } catch (e)
                    {
                        message.error(`保存失败: ${(e as Error).message}`);
                        // 失败后不关闭，让用户可以重试或取消
                    }
                }}/>
        ),
    };
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
        const item = itemRef.value;
        const parentCtx = parentContextRef.value;
        if (!item) return [];

        const context: ActionCreatorContext = {
            item,
            parentCtx,
            isReadOnly: isReadOnlyRef.value,
            modal,
            message,
            workbenchStore,
            runeTypeOptions,
        };

        // 使用创建函数数组来组装，逻辑更清晰
        const actionCreators = [
            createRenameAction,
            createAddChildAction,
            createDeleteAction,
            createSaveAsGlobalAction,
            createDuplicateAction,
        ];

        // 调用每个创建函数，并过滤掉返回 null 的结果（即不适用的 Action）
        return actionCreators.map(creator => creator(context)).filter((action): action is EnhancedAction => !!action);
    });

    return {
        actions
    };
}

/**
 * @description 为全局配置列表生成 "新建" 动作
 * @param activeTab 当前激活的标签页类型 ('rune', 'workflow', 'tuum')
 * @param currentTabLabel 当前标签页的显示名称 (例如 "符文")
 *  @param onCreated 一个回调函数，当新配置成功创建并保存为草稿后被调用。它接收包含新会话信息的对象作为参数。
 */
export function useGlobalConfigCreationAction(
    activeTab: Ref<ConfigType>,
    currentTabLabel: Ref<string>,
    onCreated: (newSession: { type: ConfigType, storeId: string }) => void
): { createNewAction: Ref<EnhancedAction> }
{
    const message = useMessage();
    const modal = useModal();
    const workbenchStore = useWorkbenchStore();
    const {runeTypeOptions, runeDefaultNameGenerator} = useRuneTypeSelector();

    /**
     * @description 处理创建逻辑的核心函数
     */
    const handleCreateNew = async (payload: { name: string; type?: string }): Promise<boolean> =>
    {
        const {name, type: runeType} = payload;
        const resourceType = activeTab.value;

        if (resourceType === 'rune' && !runeType)
        {
            message.error('创建符文时必须选择符文类型');
            return false;
        }

        try
        {
            const blankConfig =
                resourceType === 'rune'
                    ? await createBlankConfig('rune', name, {runeType: runeType!})
                    : resourceType === 'workflow'
                        ? await createBlankConfig('workflow', name)
                        : await createBlankConfig('tuum', name);

            const newSession = workbenchStore.createNewDraftSession(resourceType, blankConfig);

            onCreated({type: newSession.type, storeId: newSession.storeId});

            message.success(`成功创建全局${currentTabLabel.value}“${name}”！`);
            return true; // 表示成功
        } catch (e)
        {
            message.error(`创建失败: ${(e as Error).message}`);
            return false; // 表示失败
        }
    };

    const createNewAction = computed<EnhancedAction>(() => ({
        key: 'create-new-global',
        icon: AddIcon,
        label: `新建全局${currentTabLabel.value}`,
        disabled: false,
        activate: createModalActionActivator(modal, (modalRef) =>
        {
            const title = `新建全局${currentTabLabel.value}`;

            // onConfirm 逻辑现在统一处理异步和关闭模态框
            const createAndClose = async (createFn: () => Promise<boolean>) =>
            {
                const success = await createFn();
                if (success)
                {
                    modalRef.destroy();
                }
            };

            if (activeTab.value === 'rune')
            {
                return (
                    <SelectAndInputContent
                        title={title}
                        selectOptions={runeTypeOptions.value}
                        selectPlaceholder="请选择符文类型"
                        nameGenerator={runeDefaultNameGenerator}
                        onCancel={() => modalRef.destroy()}
                        onConfirm={(payload) => createAndClose(() => handleCreateNew(payload))}
                    />
                );
            }
            else
            {
                return (
                    <InputContent
                        title={title}
                        label="名称"
                        initialValue={`新建${currentTabLabel.value}`}
                        onCancel={() => modalRef.destroy()}
                        onConfirm={(name) => createAndClose(() => handleCreateNew({name}))}
                    />
                );
            }
        }),
    }));

    return {
        createNewAction
    };
}