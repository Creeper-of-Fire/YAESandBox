import { computed } from 'vue';
import { useWorkbenchStore } from '#/stores/workbenchStore';
import type { TreeSelectOption } from 'naive-ui';

/**
 * 一个可组合函数，提供与符文类型选择器相关的数据和辅助函数。
 * 它会生成用于 n-tree-select 的树状选项，并提供一个默认名称生成器。
 */
export function useRuneTypeSelector() {
    const workbenchStore = useWorkbenchStore();

    /**
     * 计算属性，生成用于 n-tree-select 的树状符文类型选项。
     * 包含分类、排序等逻辑。
     */
    const runeTypeOptions = computed<TreeSelectOption[]>(() => {
        const schemas = workbenchStore.runeSchemasAsync.state;
        if (!schemas) return [];

        interface TreeNode {
            label: string;
            key: string;
            children: TreeNode[];
            runes: TreeSelectOption[];
        }

        const root: TreeNode = { label: 'root', key: 'root', children: [], runes: [] };

        Object.keys(schemas).forEach(key => {
            const metadata = workbenchStore.runeMetadata[key];
            const option: TreeSelectOption = {
                label: metadata?.classLabel || schemas[key].title || key,
                key: key,
            };

            const categoryPath = metadata?.category;

            if (categoryPath) {
                const pathSegments = categoryPath.split('/');
                let currentNode = root;

                pathSegments.forEach(segment => {
                    let childNode = currentNode.children.find(c => c.label === segment);
                    if (!childNode) {
                        childNode = {
                            label: segment,
                            key: `${currentNode.key}/${segment}`,
                            children: [],
                            runes: [],
                        };
                        currentNode.children.push(childNode);
                    }
                    currentNode = childNode;
                });
                currentNode.runes.push(option);
            } else {
                root.runes.push(option);
            }
        });

        const convertToTreeSelectOptions = (node: TreeNode): TreeSelectOption[] => {
            node.children.sort((a, b) => a.label.localeCompare(b.label));
            node.runes.sort((a, b) => (a.label as string).localeCompare(b.label as string));

            const subFolders = node.children.map(childNode => ({
                label: childNode.label,
                key: childNode.key,
                children: convertToTreeSelectOptions(childNode),
                isLeaf: false,
            }));

            const leafNodes = node.runes.map(rune => ({ ...rune, isLeaf: true }));

            return [...subFolders, ...leafNodes];
        };

        return convertToTreeSelectOptions(root);
    });

    /**
     * 为新符文生成默认名称的辅助函数。
     * @param newType - 选中的符文类型 (key)。
     * @returns {string} - 生成的默认名称。
     */
    const runeDefaultNameGenerator = (newType: string): string => {
        if (newType) {
            const schema = workbenchStore.runeSchemasAsync.state?.[newType];
            const defaultName = schema?.properties?.name?.default;
            if (typeof defaultName === 'string') {
                return defaultName;
            }

            // 从树状结构中查找 label 作为备用名称
            const findLabel = (nodes: TreeSelectOption[], key: string): string | undefined => {
                for (const node of nodes) {
                    if (node.key === key) return node.label as string;
                    if (node.children) {
                        const found = findLabel(node.children, key);
                        if (found) return found;
                    }
                }
            };

            return findLabel(runeTypeOptions.value, newType) || '新符文';
        }
        return '';
    };

    return {
        runeTypeOptions,
        runeDefaultNameGenerator,
    };
}