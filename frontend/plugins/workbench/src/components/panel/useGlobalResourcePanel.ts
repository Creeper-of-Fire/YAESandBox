// src/composables/useGlobalResourcePanel.ts

import {computed, h, nextTick, reactive, type Ref, ref, watch} from 'vue';
import {useDialog, useMessage, type DropdownOption, type TreeDropInfo, NInput, NIcon, type TreeOverrideNodeClickBehavior} from 'naive-ui';
import {useFileSystemStore, type FileSystemNode} from '#/stores/useFileSystemStore';
import { useGlobalResources } from '#/composables/useGlobalResources';
import { useWorkbenchStore } from '#/stores/workbenchStore';
import { useEditorControlPayload } from '#/services/editor-context/useSelectedConfig';
import type { ConfigType } from '#/services/GlobalEditSession';
import { AddIcon as FolderAddIcon, EditIcon, TrashIcon } from '@yaesandbox-frontend/shared-ui/icons';

// Composable 的参数类型，定义它需要哪些外部状态
interface UseGlobalResourcePanelParams {
    activeTab: Ref<'workflow' | 'tuum' | 'rune'>;
}

export function useGlobalResourcePanel({ activeTab }: UseGlobalResourcePanelParams) {
    // --- 内部依赖 ---
    const dialog = useDialog();
    const message = useMessage();
    const fileSystemStore = useFileSystemStore();
    const workbenchStore = useWorkbenchStore();
    const { switchContext } = useEditorControlPayload();

    // --- 数据获取与同步 ---
    const { resources: workflows, isLoading: isWorkflowsLoading, error: workflowsError,execute: executeWorkflows } = useGlobalResources('workflow');
    const { resources: tuums, isLoading: isTuumsLoading, error: tuumsError,execute: executeTuums } = useGlobalResources('tuum');
    const { resources: runes, isLoading: isRunesLoading, error: runesError,execute: executeRunes } = useGlobalResources('rune');

    const executeAll = async () => {
        await executeWorkflows();
        await executeTuums();
        await executeRunes();
    };

    const allResourcesReady = computed(() => !isWorkflowsLoading.value && !isTuumsLoading.value && !isRunesLoading.value);

    watch(allResourcesReady, (isReady) => {
        if (isReady) {
            fileSystemStore.syncFileSystem({
                workflow: workflows.value,
                tuum: tuums.value,
                rune: runes.value,
            });
        }
    }, { immediate: true });

    // --- 覆盖节点点击行为的逻辑 ---
    const overrideNodeClickBehavior: TreeOverrideNodeClickBehavior = ({ option }) => {
        const fsNode = option as FileSystemNode;

        // 如果点击的是文件夹，行为是“切换展开/折叠”
        if (fsNode.type === 'folder') {
            return 'toggleExpand';
        }

        // 如果点击的是文件，我们希望执行自定义的“开始编辑”逻辑
        // 但 Naive UI 没有 'custom' 选项，所以我们返回 'default'
        // 然后在 GlobalResourceListItem 组件的 @click 事件中处理实际逻辑。
        return 'default';
    };

    // --- 响应式状态 ---
    const aggregatedIsLoading = computed(() => isWorkflowsLoading.value || isTuumsLoading.value || isRunesLoading.value);
    const aggregatedError = computed(() => workflowsError.value || tuumsError.value || runesError.value);

    const isCurrentTreeEmpty = computed(() => {
        if (!activeTab.value) return true;
        return (fileSystemStore.getTreeForType(activeTab.value)?.length ?? 0) === 0;
    });

    const resourceMaps = computed(() => ({
        workflow: workflows.value,
        tuum: tuums.value,
        rune: runes.value,
    }));


    // --- 拖拽处理 ---
    const handleDrop = ({ node, dragNode, dropPosition }: TreeDropInfo) => {
        const targetNode = node as FileSystemNode;
        const draggedNode = dragNode as FileSystemNode;

        if (draggedNode.configType !== targetNode.configType) {
            message.error("不允许跨资源类型拖拽。");
            return;
        }
        if (targetNode.isLeaf && dropPosition === 'inside') {
            message.warning("不能将项目拖拽进一个文件内。");
            return;
        }
        fileSystemStore.moveNode(draggedNode.configType, draggedNode.key as string, targetNode.key as string, dropPosition);
    };


    // --- 右键菜单 (Context Menu) ---
    const showDropdown = ref(false);
    const dropdownPosition = reactive({ x: 0, y: 0 });
    const activeContextItem = ref<{
        type: ConfigType;
        storeId: string;
        name: string;
        isDamaged?: boolean;
        isFolder?: boolean;
    } | null>(null);

    function handleContextMenu(payload: {
        type: ConfigType;
        storeId: string;
        name: string;
        isDamaged?: boolean;
        isFolder?: boolean;
        event: MouseEvent;
    }) {
        showDropdown.value = false;
        activeContextItem.value = { ...payload };
        dropdownPosition.x = payload.event.clientX;
        dropdownPosition.y = payload.event.clientY;
        nextTick(() => { showDropdown.value = true; });
    }

    const dropdownOptions = computed<DropdownOption[]>(() => {
        if (!activeContextItem.value) return [];
        const item = activeContextItem.value;

        if (item.isFolder) {
            return [
                { label: '新建子文件夹', key: 'create-subfolder', icon: () => h(NIcon, { component: FolderAddIcon })},
                { label: '重命名', key: 'rename', icon: () => h(NIcon, { component: EditIcon })},
                { type: 'divider', key: 'd1' },
                { label: '删除文件夹', key: 'delete', icon: () => h(NIcon, { component: TrashIcon })},
            ];
        }

        if (item.isDamaged) {
            return [{ label: '强制删除', key: 'delete', icon: () => h(NIcon, { component: TrashIcon })}];
        }

        return [
            { label: '编辑', key: 'edit', icon: () => h(NIcon, { component: EditIcon })},
            { label: '重命名', key: 'rename', icon: () => h(NIcon, { component: EditIcon })},
            { type: 'divider', key: 'd2' },
            { label: '删除', key: 'delete', icon: () => h(NIcon, { component: TrashIcon })},
        ];
    });

    async function handleDropdownSelect(key: string) {
        showDropdown.value = false;
        const item = activeContextItem.value;
        if (!item) return;

        const isFolder = !!item.isFolder;

        switch(key) {
            case 'create-subfolder':
                promptCreateFolder(item.type, item.name, item.storeId);
                break;
            case 'rename':
                promptRename(item.type, item.storeId, item.name, isFolder);
                break;
            case 'delete':
                if (isFolder) {
                    promptDeleteFolder(item.type, item.storeId, item.name);
                } else {
                    promptDeleteFile(item.type, item.storeId, item.name);
                }
                break;
            case 'edit':
                switchContext(item.type, item.storeId);
                break;
        }
    }


    // --- 对话框交互逻辑 (从 handleDropdownSelect 中提取) ---
    function promptCreateFolder(type: ConfigType, parentName: string, parentId: string) {
        let name = '新文件夹';
        const d = dialog.info({
            title: `在 "${parentName}" 中新建文件夹`,
            content: () => h(NInput, {
                defaultValue: name,
                onUpdateValue: (v) => { name = v; },
                onVnodeMounted: ({ el }) => el?.querySelector('input')?.focus()
            }),
            positiveText: '创建',
            onPositiveClick: () => {
                if (name.trim()) {
                    fileSystemStore.createFolder(type, name.trim(), parentId);
                    message.success(`文件夹 "${name.trim()}" 已创建`);
                } else {
                    message.error('文件夹名称不能为空');
                    return false;
                }
            }
        });
    }

    function promptRename(type: ConfigType, id: string, oldName: string, isFolder: boolean) {
        let newName = oldName;
        const d = dialog.info({
            title: `重命名 "${oldName}"`,
            content: () => h(NInput, {
                defaultValue: oldName,
                onUpdateValue: (v) => { newName = v; },
                onVnodeMounted: ({ el }) => {
                    const input = el?.querySelector('input');
                    input?.focus();
                    input?.select();
                }
            }),
            positiveText: '确认',
            onPositiveClick: async () => {
                const finalName = newName.trim();
                if (!finalName) {
                    message.error('名称不能为空');
                    return false;
                }
                if (finalName === oldName) return;

                try {
                    if (isFolder) {
                        fileSystemStore.renameNode(type, id, finalName);
                    } else {
                        await workbenchStore.renameGlobalConfig(type, id, finalName);
                        fileSystemStore.renameNode(type, id, finalName);
                    }
                    message.success(`已重命名为 "${finalName}"`);
                } catch (err: any) {
                    message.error(`重命名失败: ${err.message}`);
                }
            }
        });
    }

    async function promptDeleteFolder(type: ConfigType, id: string, name: string) {
        dialog.warning({
            title: '确认删除文件夹',
            content: `你确定要永久删除文件夹 “${name}” 及其所有内容吗？此操作不可恢复。`,
            positiveText: '全部删除',
            onPositiveClick: async () => {
                try {
                    const tree = fileSystemStore.getTreeForType(type);
                    const nodeToDelete = fileSystemStore.findNode(type, id);
                    if (!nodeToDelete) throw new Error("找不到要删除的文件夹节点");

                    const filesToDelete: { type: ConfigType; id: string }[] = [];
                    fileSystemStore.walkTree([nodeToDelete], (node) => {
                        if (node.type === 'file') {
                            filesToDelete.push({ type: node.configType, id: node.key });
                        }
                    });

                    await Promise.all(filesToDelete.map(f => workbenchStore.deleteGlobalConfig(f.type, f.id)));

                    fileSystemStore.deleteNode(type, id);
                    message.success(`文件夹 "${name}" 及其内容已删除`);
                } catch(error: any) {
                    message.error(`删除文件夹时出错: ${error.message}`);
                }
            },
        });
    }

    function promptDeleteFile(type: ConfigType, id: string, name: string) {
        dialog.warning({
            title: '确认删除',
            content: `你确定要永久删除全局资源 “${name}” 吗？此操作不可恢复。`,
            positiveText: '确定删除',
            onPositiveClick: async () => {
                try {
                    await workbenchStore.deleteGlobalConfig(type, id);
                    fileSystemStore.deleteNode(type, id);
                    message.success(`已删除 “${name}”`);
                } catch (error: any) {
                    message.error(`删除 “${name}” 失败: ${error.message}`);
                }
            },
        });
    }

    // --- 返回所有需要暴露给组件的 state 和方法 ---
    return {
        // State
        aggregatedIsLoading,
        aggregatedError,
        isCurrentTreeEmpty,
        resourceMaps,
        showDropdown,
        dropdownPosition,
        dropdownOptions,
        fileSystemStore, // 直接暴露 store 实例，方便模板访问树数据

        overrideNodeClickBehavior,

        // Methods
        executeAll,
        handleDrop,
        handleContextMenu,
        handleDropdownSelect,
        promptCreateFolder: (type: ConfigType) => promptCreateFolder(type, '根目录', ''), // 提供一个创建根文件夹的方法
    };
}