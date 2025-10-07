// src/stores/fileSystemStore.ts
import {defineStore} from 'pinia';
import {computed} from 'vue';
import {useScopedStorage} from '@yaesandbox-frontend/core-services/composables';
import {v4 as uuidv4} from 'uuid';
import type {TreeOption} from 'naive-ui';
import type {ConfigType} from '#/services/GlobalEditSession';
import type {GlobalResourceItem} from '#/stores/workbenchStore';

// 扩展 Naive UI 的 TreeOption 来包含我们的自定义元数据
export interface FileSystemNode extends TreeOption
{
    key: string; // 文件夹用 UUID, 文件用 storeId
    label: string;
    type: 'folder' | 'file';
    configType: ConfigType; // 文件节点需要知道它是什么类型的配置
    children?: FileSystemNode[];
    isLeaf: boolean;
}

// 定义 Store 的状态结构
interface FileSystemState
{
    workflow: FileSystemNode[];
    tuum: FileSystemNode[];
    rune: FileSystemNode[];
}

/**
 * 辅助函数，在树中递归查找具有指定 key 的节点。
 * @param nodes - 要搜索的节点数组
 * @param key - 要查找的 key
 * @returns 找到的节点或 null
 */
function findNode(nodes: FileSystemNode[], key: string): FileSystemNode | null
{
    for (const node of nodes)
    {
        if (node.key === key)
        {
            return node;
        }
        if (node.children)
        {
            const found = findNode(node.children, key);
            if (found)
            {
                return found;
            }
        }
    }
    return null;
}


// 辅助函数：深度遍历树并执行操作
function walkTree(nodes: FileSystemNode[], callback: (node: FileSystemNode, parent: FileSystemNode | null) => void)
{
    const queue: { node: FileSystemNode; parent: FileSystemNode | null }[] = nodes.map(node => ({node, parent: null}));
    while (queue.length > 0)
    {
        const {node, parent} = queue.shift()!;
        callback(node, parent);
        if (node.children)
        {
            node.children.forEach(child => queue.push({node: child, parent: node}));
        }
    }
}

// 辅助函数：在树中查找并删除节点
function findAndRemoveNode(nodes: FileSystemNode[], key: string): boolean
{
    for (let i = 0; i < nodes.length; i++)
    {
        if (nodes[i].key === key)
        {
            nodes.splice(i, 1);
            return true;
        }
        if (nodes[i].children && findAndRemoveNode(nodes[i].children!, key))
        {
            return true;
        }
    }
    return false;
}

const STORAGE_KEY = 'workbench-file-system-state';

export const useFileSystemStore = defineStore(STORAGE_KEY, () =>
{
    // 1. 使用 useScopedStorage 持久化状态
    const state = useScopedStorage<FileSystemState>(STORAGE_KEY, {
        workflow: [],
        tuum: [],
        rune: [],
    });

    const getTreeForType = (type: ConfigType) => state.value[type];

    /**
     * 核心：初始化/同步文件系统
     * @param allResources - 从 useGlobalResources 获取的原始资源 map
     */
    function syncFileSystem(allResources: { [key in ConfigType]: Record<string, GlobalResourceItem<any>> })
    {
        const allTypes: ConfigType[] = ['workflow', 'tuum', 'rune'];

        for (const type of allTypes)
        {
            // 直接获取并修改 state 中的树，因为 Pinia 允许直接修改 state
            const currentTree = state.value[type];
            const fetchedResources = allResources[type];
            if (!fetchedResources) continue;

            const fetchedKeys = new Set(Object.keys(fetchedResources));
            const existingFileKeys = new Set<string>();

            // 1. **更新阶段**: 遍历当前树，更新现有文件的标签（名称），并收集所有文件键
            walkTree(currentTree, (node) => {
                if (node.isLeaf) { // 只处理文件节点
                    existingFileKeys.add(node.key);
                    const fetchedItem = fetchedResources[node.key];
                    // 如果文件仍然存在于获取的数据中，并且名称不一致，则更新它
                    if (fetchedItem && fetchedItem.isSuccess && node.label !== fetchedItem.data.name) {
                        node.label = fetchedItem.data.name;
                    }
                }
            });

            // 2. **追加阶段**: 遍历获取到的资源，将任何本地不存在的新文件添加到根目录
            for (const fetchedKey of fetchedKeys)
            {
                if (!existingFileKeys.has(fetchedKey) && fetchedResources[fetchedKey].isSuccess)
                {
                    // 这是一个全新的文件，将它添加到根列表
                    currentTree.push({
                        key: fetchedKey,
                        type: 'file',
                        label: fetchedResources[fetchedKey].data.name,
                        isLeaf: true,
                        configType: type,
                    });
                }
            }
        }
    }

    function createFolder(type: ConfigType, name: string, parentKey: string | null = null)
    {
        const tree = getTreeForType(type);
        const newFolder: FileSystemNode = {
            key: uuidv4(),
            label: name,
            type: 'folder',
            configType: type,
            isLeaf: false,
            children: [],
        };

        if (parentKey)
        {
            const parentNode = findNode(tree, parentKey);
            if (parentNode && !parentNode.isLeaf)
            {
                // 确保 children 数组存在
                if (!parentNode.children)
                {
                    parentNode.children = [];
                }
                parentNode.children!.push(newFolder);
            }
            else
            {
                tree.push(newFolder); // 找不到父节点或父节点是文件，则添加到根
            }
        }
        else
        {
            tree.push(newFolder);
        }
    }

    function renameNode(type: ConfigType, key: string, newName: string)
    {
        const tree = getTreeForType(type);
        walkTree(tree, (node) =>
        {
            if (node.key === key)
            {
                node.label = newName;
            }
        });
    }

    function deleteNode(type: ConfigType, key: string)
    {
        const tree = getTreeForType(type);
        findAndRemoveNode(tree, key);
    }

    function moveNode(type: ConfigType, nodeKey: string, targetKey: string, position: 'before' | 'after' | 'inside')
    {
        const tree = getTreeForType(type);
        let nodeToMove: FileSystemNode | null = null;

        // 1. 找到并移除要移动的节点
        walkTree(tree, (node, parent) =>
        {
            if (node.key === nodeKey)
            {
                nodeToMove = node;
                const sourceList = parent ? parent.children! : tree;
                const index = sourceList.findIndex(n => n.key === nodeKey);
                if (index > -1) sourceList.splice(index, 1);
            }
        });

        if (!nodeToMove) return;

        // 2. 将节点插入到新位置
        let targetFound = false;
        walkTree(tree, (node, parent) =>
        {
            if (node.key === targetKey && !targetFound)
            {
                targetFound = true;
                const targetList = parent ? parent.children! : tree;
                const targetIndex = targetList.findIndex(n => n.key === targetKey);

                if (position === 'inside')
                {
                    if (!node.isLeaf)
                    {
                        if (!node.children) node.children = [];
                        node.children.unshift(nodeToMove!);
                    }
                }
                else if (position === 'before')
                {
                    targetList.splice(targetIndex, 0, nodeToMove!);
                }
                else if (position === 'after')
                {
                    targetList.splice(targetIndex + 1, 0, nodeToMove!);
                }
            }
        });

        // 如果目标是根节点（例如拖到一个文件的旁边）
        if (!targetFound)
        {
            const targetIndex = tree.findIndex(n => n.key === targetKey);
            if (targetIndex > -1)
            {
                if (position === 'before')
                {
                    tree.splice(targetIndex, 0, nodeToMove!);
                }
                else if (position === 'after')
                {
                    tree.splice(targetIndex + 1, 0, nodeToMove!);
                }
            }
        }
    }

    function updateTree(type: ConfigType, newTree: FileSystemNode[]) {
        state.value[type] = newTree;
    }


    return {
        // State/Getters
        workflowTree: computed(() => state.value.workflow),
        tuumTree: computed(() => state.value.tuum),
        runeTree: computed(() => state.value.rune),
        // Actions
        syncFileSystem,
        createFolder,
        renameNode,
        deleteNode,
        moveNode,
        updateTree,

        // 辅助函数
        findNode: (type: ConfigType, key: string) => findNode(getTreeForType(type), key),
        getTreeForType: (type: ConfigType) => getTreeForType(type),
        walkTree: (
            nodes: FileSystemNode[],
            callback: (node: FileSystemNode, parent: (FileSystemNode | null)) => void,
        ) => walkTree(nodes, callback),
    };
});