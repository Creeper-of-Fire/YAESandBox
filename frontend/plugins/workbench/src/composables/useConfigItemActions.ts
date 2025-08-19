// 文件路径: src/app-workbench/composables/useConfigItemActions.ts
import {type Component, computed, type Ref} from 'vue';
import {type SelectOption, useMessage} from 'naive-ui';
import type {ConfigObject} from '#/services/EditSession';
import {useWorkbenchStore} from '#/stores/workbenchStore';
import {AddIcon, EditIcon, SaveIcon, TrashIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {createBlankConfig} from "#/utils/createBlankConfig.ts";

/**
 * @description useConfigItemActions 的输入参数类型
 */
interface UseConfigItemActionsParams
{
    itemRef: Ref<ConfigObject | null>;
    parentContextRef: Ref<{ parent: ConfigObject; list: ConfigObject[] } | null>;
}

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
    popoverSelectOptions?: SelectOption[];
    popoverSelectPlaceholder?: string;
    popoverInitialValue?: string;
    popoverDefaultNameGenerator?: (selectedValue: any, selectOptions: SelectOption[]) => string;
    popoverConfirmMessage?: string;
    confirmText?: string;
    handler?: (payload: { name?: string; type?: string }) => void;
}

/**
 * @description 一个可组合函数，用于生成针对特定配置项的可用动作列表
 * @param params 包含当前项、会话和父级上下文的响应式引用
 */
export function useConfigItemActions({itemRef, parentContextRef}: UseConfigItemActionsParams)
{
    const message = useMessage();
    const workbenchStore = useWorkbenchStore();

    const runeTypeOptions = computed(() =>
    {
        const schemas = workbenchStore.runeSchemasAsync.state;
        if (!schemas) return [];
        return Object.keys(schemas).map(key =>
        {
            const metadata = workbenchStore.runeMetadata[key];
            return {
                label: metadata?.classLabel || schemas[key].title || key,
                value: key,
            };
        });
    });

    const runeDefaultNameGenerator = (newType: string, options: any[]) =>
    {
        if (newType)
        {
            const schema = workbenchStore.runeSchemasAsync.state[newType];
            const defaultName = schema?.properties?.name?.default;
            if (typeof defaultName === 'string')
            {
                return defaultName;
            }
            return options.find(opt => opt.value === newType)?.label || '新符文';
        }
        return '';
    };

    // 动作：重命名
    const renameAction = computed<EnhancedAction>(() => ({
        key: 'rename',
        label: '重命名',
        icon: EditIcon,
        renderType: 'popover',
        disabled: !itemRef.value,
        popoverTitle: '重命名',
        popoverContentType: 'input',
        popoverInitialValue: itemRef.value?.name ?? '',
        handler: ({name}) =>
        {
            if (!itemRef.value || !name) return;
            itemRef.value.name = name;
            message.success(`已重命名为 "${name}"`);
        },
    }));

    const addChildAction = computed<EnhancedAction>(() =>
    {
        const item = itemRef.value;
        // 默认隐藏动作
        let action: EnhancedAction = {
            key: 'add-child',
            icon: AddIcon,
            label: '添加子项',
            renderType: 'button',
            disabled: true
        };

        if (item && 'tuums' in item)
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
                handler: async ({name}) =>
                {
                    if (!name) return;
                    const newTuum = await createBlankConfig('tuum', name);
                    item.tuums.push(newTuum);
                    message.success('已添加新枢机');
                },
            };
        }
        else if (item && 'runes' in item)
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
    });

    // 动作：删除
    const deleteAction = computed<EnhancedAction>(() => ({
        key: 'delete',
        label: '删除',
        icon: TrashIcon,
        renderType: 'popover',
        type: 'error',
        disabled: !parentContextRef.value,
        popoverTitle: '确认删除',
        popoverContentType: 'confirm-delete',
        popoverConfirmMessage: `你确定要删除“${itemRef.value?.name}”吗？此操作不可恢复。`,
        handler: () =>
        {
            const parentCtx = parentContextRef.value;
            const currentItem = itemRef.value;
            if (!parentCtx || !currentItem || !('configId' in currentItem)) return;

            const index = parentCtx.list.findIndex(i => 'configId' in i && i.configId === currentItem.configId);
            if (index > -1)
            {
                parentCtx.list.splice(index, 1);
                message.success(`已删除“${currentItem.name}”`);
            }
        },
    }));

    // 动作：另存为全局配置
    const saveAsGlobalAction = computed<EnhancedAction>(() => ({
        key: 'save-as-global',
        label: '另存为全局',
        icon: SaveIcon,
        renderType: 'popover',
        type: 'success',
        disabled: !itemRef.value,
        popoverTitle: '另存为全局配置',
        popoverContentType: 'input',
        popoverInitialValue: `${itemRef.value?.name} (全局副本)`,
        handler: async ({name}) =>
        {
            const item = itemRef.value;
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
    }));

    const allActions = computed<EnhancedAction[]>(() =>
    {
        return [
            renameAction.value,
            addChildAction.value,
            deleteAction.value,
            saveAsGlobalAction.value,
        ].filter(a => a.key && !a.disabled); // 过滤掉无效和禁用的动作
    });

    return {
        actions: allActions,
    };
}