import {ref, readonly, type Ref, computed} from 'vue';
import { nanoid } from 'nanoid';
import type { StorageAdapter } from '#/share/services/storageAdapter.ts';


/**
 * 定义了存储作用域管理器的公共契约。
 * 任何提供响应式路径的管理器都应实现此接口。
 */
export interface StorageScopeManager {
    /** 所有作用域的根目录 (例如 ['saves']) */
    readonly rootPath: Readonly<Ref<readonly string[]>>;

    /** 当前激活的作用域路径 (例如 ['saves', 'save-slot-1']) */
    readonly activeScopePath: Readonly<Ref<readonly string[] | null>>;

    /** 当前激活的作用域名 (例如 'save-slot-1') */
    readonly activeScopeName: Readonly<Ref<string | null>>;

    /** 所有可用作用域的列表 (例如 ['save-slot-1', 'save-slot-2']) */
    readonly availableScopes: Readonly<Ref<readonly string[]>>;

    /** 切换到指定的作用域 */
    selectScope(scopeName: string | null): Promise<void>;

    /** 创建一个新的作用域 */
    createScope(scopeName: string): Promise<void>;

    /** 删除一个作用域 */
    deleteScope(scopeName: string): Promise<void>;

    /** 复制一个作用域的内容到另一个作用域 */
    copyScope(sourceScopeName: string, targetScopeName: string): Promise<void>;

    /** 从存储中重新加载作用域列表 */
    refreshScopes(): Promise<void>;
}

/**
 * 创建一个作用域管理器的 Composable。
 * @param storageAdapter - 用于底层 I/O 操作的存储适配器。
 * @param rootPath - 所有作用域的根目录 (例如 ['saves'])。
 */
export function useStorageScopeManager(
    storageAdapter: StorageAdapter,
    rootPath: string[]
): StorageScopeManager {
    // 内部状态
    const _rootPath = ref(rootPath);
    const _activeScopePath = ref<string[]|null>(null);
    const _availableScopes = ref<string[]>([]);

    // --- 派生状态 ---
    const activeScopeName = computed<string|null>(() => {
        const path = _activeScopePath.value;
        return path ? path[path.length - 1] : null;
    });

    // --- 核心方法 ---
    async function refreshScopes(): Promise<void> {
        try {
            _availableScopes.value = await storageAdapter.list(rootPath);
        } catch (error) {
            console.error(`在列出 [${rootPath.join('/')}] 下的作用域时失败`, error);
            _availableScopes.value = [];
        }
    }

    async function selectScope(scopeName: string | null): Promise<void> {
        if (scopeName) {
            // 如果提供了有效的 scopeName，则构建并设置路径
            _activeScopePath.value = [...rootPath, scopeName];
            console.log(`改变作用域到：${scopeName}`);
        } else {
            // 如果传入 null，则明确地将 activeScopePath 设为 null
            _activeScopePath.value = null;
            console.log(`已卸载当前作用域。`);
        }
    }

    async function createScope(scopeName: string): Promise<void> {
        // 创建一个 scope 意味着要在其中写入一个文件，
        // 我们让上层 (useSaveSlotManager) 来决定写入什么 (meta.json)。
        // 这个函数现在只负责更新内存中的列表。
        const currentScopes = [..._availableScopes.value]
        if (!_availableScopes.value.includes(scopeName)) {
            _availableScopes.value = [...currentScopes,scopeName];
        }
    }

    async function copyScope(sourceScopeName: string, targetScopeName: string): Promise<void> {
        const sourcePath = [...rootPath, sourceScopeName];
        const targetPath = [...rootPath, targetScopeName];

        console.log(`Copying scope from [${sourcePath.join('/')}] to [${targetPath.join('/')}]...`);

        const filesToCopy = await storageAdapter.list(sourcePath);
        for (const fileName of filesToCopy)
        {
            const content = await storageAdapter.getItem(sourcePath, fileName);
            if (content !== null)
            {
                await storageAdapter.setItem(targetPath, fileName, content);
            }
        }
        console.log("Scope copied successfully.");

        // 复制完成后，确保目标作用域在列表中
        if (!_availableScopes.value.includes(targetScopeName)) {
            _availableScopes.value = [..._availableScopes.value, targetScopeName];
        }
    }

    async function deleteScope(scopeName: string): Promise<void> {
        // TODO 这是一个复杂操作，需要递归删除目录下的所有文件。
        // StorageAdapter 目前没有此功能，但我们可以模拟。
        // 为保持此 composable 简单，我们假设用户知道自己在做什么。
        // 实际游戏中需要更完善的实现。
        console.warn(`Deletion of scope "${scopeName}" is a destructive operation and needs a more robust implementation.`);
        // 从列表中移除
        _availableScopes.value = _availableScopes.value.filter(s => s !== scopeName);
        // 如果删除的是当前作用域，则切换回默认
        if (_activeScopePath.value && _activeScopePath.value.slice(-1)[0] === scopeName) {
            _activeScopePath.value = null; // 如果删除的是当前存档，则退出到未加载状态
        }
    }

    // --- 初始化 ---
    refreshScopes(); // 首次创建时加载一次

    return {
        rootPath: readonly(_rootPath),
        activeScopePath: readonly(_activeScopePath),
        activeScopeName: readonly(activeScopeName),
        availableScopes: readonly(_availableScopes),
        selectScope,
        createScope,
        copyScope,
        deleteScope,
        refreshScopes,
    };
}