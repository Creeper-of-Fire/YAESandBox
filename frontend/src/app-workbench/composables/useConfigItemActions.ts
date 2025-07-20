// 文件路径: src/app-workbench/composables/useConfigItemActions.ts
import { computed, ref, type Ref, watch } from 'vue';
import { useMessage } from 'naive-ui';
import type { ConfigObject } from '@/app-workbench/services/EditSession';
import { useWorkbenchStore } from '@/app-workbench/stores/workbenchStore';
import { AddIcon, SaveIcon, TrashIcon, EditIcon } from '@/utils/icons';
import { createBlankConfig } from "@/app-workbench/utils/createBlankConfig.ts";
import type { EnhancedAction } from '@/app-workbench/components/share/ConfigItemActionsMenu.vue';

/**
 * @description useConfigItemActions 的输入参数类型
 */
interface UseConfigItemActionsParams {
    itemRef: Ref<ConfigObject | null>;
    parentContextRef: Ref<{ parent: ConfigObject; list: ConfigObject[] } | null>;
}

/**
 * @description 一个可组合函数，用于生成针对特定配置项的可用动作列表
 * @param params 包含当前项、会话和父级上下文的响应式引用
 */
export function useConfigItemActions({ itemRef, parentContextRef }: UseConfigItemActionsParams) {
    const message = useMessage();
    const workbenchStore = useWorkbenchStore();

    const moduleTypeOptions = computed(() => {
        const schemas = workbenchStore.moduleSchemasAsync.state.value;
        if (!schemas) return [];
        return Object.keys(schemas).map(key => {
            const metadata = workbenchStore.moduleMetadata[key];
            return {
                label: metadata?.classLabel || schemas[key].title || key,
                value: key,
            };
        });
    });

    const moduleDefaultNameGenerator = (newType: string, options: any[]) => {
        if (newType) {
            const schema = workbenchStore.moduleSchemasAsync.state.value?.[newType];
            const defaultName = schema?.properties?.name?.default;
            if (typeof defaultName === 'string') {
                return defaultName;
            }
            return options.find(opt => opt.value === newType)?.label || '新模块';
        }
        return '';
    };

    // 动作：重命名
    const renameAction = computed<EnhancedAction>(() => ({
        key: 'rename',
        label:'重命名',
        icon: EditIcon,
        renderType: 'popover',
        disabled: !itemRef.value,
        popoverTitle: '重命名',
        popoverInitialValue: itemRef.value?.name ?? '',
        handler: ({ name }) => {
            if (!itemRef.value || !name) return;
            itemRef.value.name = name;
            message.success(`已重命名为 "${name}"`);
        },
    }));

    const addChildAction = computed<EnhancedAction>(() => {
        const item = itemRef.value;
        // 默认隐藏动作
        let action: EnhancedAction = {
            key: 'add-child',
            icon: AddIcon,
            label: '添加子项',
            renderType: 'button',
            disabled: true };

        if (item && 'steps' in item) { // 工作流添加步骤
            action = {
                key: 'add-step',
                label: '添加步骤',
                icon: AddIcon,
                renderType: 'popover',
                type: 'primary',
                popoverTitle: '添加新步骤',
                popoverInitialValue: '新步骤',
                handler: ({ name }) => {
                    if (!name) return;
                    const newStep = createBlankConfig('step', name);
                    item.steps.push(newStep);
                    message.success('已添加新步骤');
                },
            };
        } else if (item && 'modules' in item) { // 步骤添加模块
            action = {
                key: 'add-module',
                label: '添加模块',
                icon: AddIcon,
                renderType: 'popover',
                type: 'primary',
                popoverTitle: '添加新模块',
                popoverContentType: 'select-and-input',
                popoverSelectOptions: moduleTypeOptions.value,
                popoverSelectPlaceholder: '请选择模块类型',
                popoverDefaultNameGenerator: moduleDefaultNameGenerator,
                handler: ({ name, type }) => {
                    if (!name || !type) return;
                    const newModule = createBlankConfig('module', name, { moduleType: type });
                    item.modules.push(newModule);
                    message.success('已添加新模块');
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
        renderType: 'confirm',
        type: 'error',
        disabled: !parentContextRef.value,
        confirmText: `你确定要删除“${itemRef.value?.name}”吗？`,
        handler: () => {
            const parentCtx = parentContextRef.value;
            const currentItem = itemRef.value;
            if (!parentCtx || !currentItem || !('configId' in currentItem)) return;

            const index = parentCtx.list.findIndex(i => 'configId' in i && i.configId === currentItem.configId);
            if (index > -1) {
                parentCtx.list.splice(index, 1);
                message.success(`已删除“${currentItem.name}”`);
            }
        },
    }));

    // 动作：另存为全局配置
    const saveAsGlobalAction = computed<EnhancedAction>(() => ({
        key: 'save-as-global',
        label:'另存为全局',
        icon: SaveIcon,
        renderType: 'popover',
        type: 'success',
        disabled: !itemRef.value,
        popoverTitle: '另存为全局配置',
        popoverInitialValue: `${itemRef.value?.name} (全局副本)`,
        handler: async ({ name }) => {
            const item = itemRef.value;
            if (!item || !name) return;

            try {
                await workbenchStore.createGlobalConfig({ ...item, name });
                message.success(`已成功将“${name}”保存为全局配置！`);
            } catch (e) {
                message.error(`保存失败: ${(e as Error).message}`);
            }
        },
    }));

    const allActions = computed<EnhancedAction[]>(() => {
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