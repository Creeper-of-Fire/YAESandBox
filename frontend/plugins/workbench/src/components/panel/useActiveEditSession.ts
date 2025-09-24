import { computed, ref, readonly } from 'vue';
import { useDialog, useMessage } from 'naive-ui';
import type { EditSession, ConfigType } from '#/services/EditSession';
import { useWorkbenchStore } from '#/stores/workbenchStore';

/**
 * 管理工作台中当前激活的编辑会話的 Composable。
 * 它封装了会话的获取、切换、关闭和保存逻辑，并向上层组件提供响应式状态。
 */
export function useActiveEditSession() {
    // 内部依赖
    const workbenchStore = useWorkbenchStore();
    const message = useMessage();
    const dialog = useDialog();

    // --- 内部状态 ---
    const _activeSession = ref<EditSession | null>(null);
    const _isLoading = ref(false);

    // --- 响应式状态 (向外暴露) ---
    const activeSession = readonly(_activeSession); // 外部只能读取，不能直接修改
    const isLoading = readonly(_isLoading);
    const isDirty = computed(() => _activeSession.value?.getIsDirty().value ?? false);

    /**
     * 切换或开始一个新的编辑会话。
     * @param type - 资源类型
     * @param id - 资源的全局 ID
     */
    async function switchSession(type: ConfigType, id: string): Promise<void> {
        // 防止重复加载同一个会话
        if (_activeSession.value?.globalId === id) {
            return;
        }

        _isLoading.value = true;
        _activeSession.value = null; // 切换时先清空，UI会显示加载状态

        try {
            const session = await workbenchStore.acquireEditSession(type, id);
            if (session) {
                _activeSession.value = session;
                // [可选] 可以在这里添加一些会话切换后的默认行为，
                // 例如默认选中根节点等，进一步简化 WorkbenchView 的逻辑。
            } else {
                message.error(`无法开始编辑 “${id}”。资源可能不存在或已损坏。`);
            }
        } catch (error) {
            console.error(`切换到会话 (${type}, ${id}) 时发生意外错误:`, error);
            message.error('开始编辑时发生未知错误。');
            _activeSession.value = null;
        } finally {
            _isLoading.value = false;
        }
    }

    /**
     * 关闭当前激活的会话。
     * 如果有未保存的更改，会弹出确认框。
     */
    function closeSession(): void {
        if (isDirty.value) {
            dialog.warning({
                title: '关闭前确认',
                content: '当前有未保存的更改，您确定要关闭吗？所有未保存的更改都将丢失。',
                positiveText: '确定关闭',
                negativeText: '取消',
                onPositiveClick: () => {
                    if (_activeSession.value) {
                        workbenchStore.closeSession(_activeSession.value);
                        _activeSession.value = null;
                        message.info('编辑会话已关闭。');
                    }
                },
            });
        } else {
            if (_activeSession.value) {
                workbenchStore.closeSession(_activeSession.value);
                _activeSession.value = null;
            }
        }
    }

    /**
     * 保存当前激活会话的更改。
     */
    async function saveSession(): Promise<void> {
        if (!_activeSession.value || !isDirty.value) {
            message.info('没有需要保存的更改。');
            return;
        }

        const session = _activeSession.value;
        const result = await session.save();

        if (result.success) {
            message.success(`“${result.name}” 已保存!`);
        } else {
            message.error(`保存失败：${result.name}，请检查内容或查看控制台。`);
            console.error(`${result.name} 保存失败，详情:`, result.error);
        }
    }

    // 返回所有暴露给组件的 API
    return {
        activeSession,
        isLoading,
        isDirty,
        switchSession,
        closeSession,
        saveSession,
    };
}