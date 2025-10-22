// 文件路径: src/app-workbench/composables/useConfigItemActions.ts
import {type Component, computed, type Ref} from 'vue';
import {type SelectOption, type TreeSelectOption, useMessage} from 'naive-ui';
import type {AnyConfigObject} from '#/services/GlobalEditSession.ts';
import {useWorkbenchStore} from '#/stores/workbenchStore';
import {AddIcon, EditIcon, SaveIcon, TrashIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {createBlankConfig} from "#/utils/createBlankConfig.ts";
import {useSelectedConfig} from "#/services/editor-context/useSelectedConfig.ts";
import {useRuneTypeSelector} from "#/composables/useRuneTypeSelector.ts";
import type {TuumConfig} from "#/types/generated/workflow-config-api-client";

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
    key: string;
    label: string;
    icon?: Component;
    disabled?: boolean;
    type?: 'default' | 'primary' | 'info' | 'success' | 'warning' | 'error';
    renderType: 'popover' | 'confirm' | 'button';
    popoverTitle?: string;
    popoverContentType?: 'input' | 'select-and-input' | 'confirm-delete';
    popoverSelectOptions?: SelectOption[] | TreeSelectOption[];
    popoverSelectPlaceholder?: string;
    popoverInitialValue?: string;
    popoverDefaultNameGenerator?: (selectedValue: any, selectOptions: SelectOption[] | TreeSelectOption[]) => string;
    popoverConfirmMessage?: string;
    confirmText?: string;
    handler?: (payload: { name?: string; type?: string }) => void;
}

/**
 * @description 一个可组合函数，用于生成针对特定配置项的可用动作列表
 * @param params 包含当前项、会话和父级上下文的响应式引用
 */
export function useConfigItemActions({itemRef, parentContextRef}: UseConfigItemActionsParams): { actions: Ref<EnhancedAction[]>; }
{
    const {isReadOnly: isReadOnlyRef} = useSelectedConfig();
    const message = useMessage();
    const workbenchStore = useWorkbenchStore();

    const { runeTypeOptions, runeDefaultNameGenerator } = useRuneTypeSelector();

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

        // 动作：重命名
        const renameAction: EnhancedAction = {
            key: 'rename',
            label: '重命名',
            icon: EditIcon,
            renderType: 'popover',
            disabled: isReadOnly || !item,
            popoverTitle: '重命名',
            popoverContentType: 'input',
            popoverInitialValue: item?.name ?? '',
            handler: ({name}) =>
            {
                if (!item || !name) return;
                item.name = name;
                message.success(`已重命名为 "${name}"`);
            },
        }

        const getAddChildAction = (): EnhancedAction =>
        {
            // 默认隐藏动作
            let action: EnhancedAction = {
                key: 'add-child',
                icon: AddIcon,
                label: '添加子项',
                renderType: 'button',
                disabled: true
            };

            if ('tuums' in item)
            { // 工作流添加枢机
                action = {
                    key: 'add-tuum',
                    label: '添加枢机',
                    icon: AddIcon,
                    renderType: 'popover',
                    type: 'primary',
                    popoverTitle: '添加新枢机',
                    popoverContentType: 'input',
                    popoverInitialValue: '新枢机',
                    disabled: isReadOnly,
                    handler: async ({name}) =>
                    {
                        if (!name) return;
                        const newTuum = await createBlankConfig('tuum', name);
                        item.tuums.push(newTuum);
                        message.success('已添加新枢机');
                    },
                };
            }
            else if ('innerTuum' in item && item.innerTuum)
            { // 包含 innerTuum 的符文（如 TuumRune），为其添加符文
                action = {
                    key: 'add-rune-to-inner-tuum',
                    label: '添加符文',
                    icon: AddIcon,
                    renderType: 'popover',
                    type: 'primary',
                    popoverTitle: '添加新符文到子枢机',
                    popoverContentType: 'select-and-input',
                    popoverSelectOptions: runeTypeOptions.value,
                    popoverSelectPlaceholder: '请选择符文类型',
                    disabled: isReadOnly,
                    popoverDefaultNameGenerator: runeDefaultNameGenerator,
                    handler: async ({name, type}) =>
                    {
                        if (!name || !type || !item.innerTuum) return;
                        const newRune = await createBlankConfig('rune', name, {runeType: type});
                        (item.innerTuum as TuumConfig).runes.push(newRune);
                        message.success('已添加新符文到子枢机');
                    },
                };
            }
            else if ('runes' in item)
            { // 枢机添加符文
                action = {
                    key: 'add-rune',
                    label: '添加符文',
                    icon: AddIcon,
                    renderType: 'popover',
                    type: 'primary',
                    popoverTitle: '添加新符文',
                    popoverContentType: 'select-and-input',
                    popoverSelectOptions: runeTypeOptions.value,
                    popoverSelectPlaceholder: '请选择符文类型',
                    disabled: isReadOnly,
                    popoverDefaultNameGenerator: runeDefaultNameGenerator,
                    handler: async ({name, type}) =>
                    {
                        if (!name || !type) return;
                        const newRune = await createBlankConfig('rune', name, {runeType: type});
                        item.runes.push(newRune);
                        message.success('已添加新符文');
                    },
                };
            }
            return action;
        }

        // 动作：删除
        const deleteAction: EnhancedAction = {
            key: 'delete',
            label: '删除',
            icon: TrashIcon,
            renderType: 'popover',
            type: 'error',
            disabled: isReadOnly || !parentCtx,
            popoverTitle: '确认删除',
            popoverContentType: 'confirm-delete',
            popoverConfirmMessage: `你确定要删除“${item.name}”吗？此操作不可恢复。`,
            handler: () =>
            {
                if (!parentCtx || !item || !('configId' in item)) return;

                const index = parentCtx.list.findIndex(i => 'configId' in i && i.configId === item.configId);
                if (index > -1)
                {
                    parentCtx.list.splice(index, 1);
                    message.success(`已删除“${item.name}”`);
                }
            },
        }

        // 动作：另存为全局配置
        const saveAsGlobalAction: EnhancedAction = {
            key: 'save-as-global',
            label: '另存为全局',
            icon: SaveIcon,
            renderType: 'popover',
            type: 'success',
            disabled: !item,
            popoverTitle: '另存为全局配置',
            popoverContentType: 'input',
            popoverInitialValue: `${item?.name} (全局副本)`,
            handler: async ({name}) =>
            {
                if (!item || !name) return;

                try
                {
                    await workbenchStore.createGlobalConfig({...item, name});
                    message.success(`已成功将“${name}”保存为全局配置！`);
                } catch (e)
                {
                    message.error(`保存失败: ${(e as Error).message}`);
                }
            },
        }

        return [
            renameAction,
            getAddChildAction(),
            deleteAction,
            saveAsGlobalAction,
        ].filter(a => a.key && !a.disabled); // 过滤掉无效和禁用的动作
    });

    return {
        actions
    };
}