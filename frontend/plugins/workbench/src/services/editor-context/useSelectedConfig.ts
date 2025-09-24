import {computed, inject, type InjectionKey, provide, readonly, ref, type Ref, shallowRef} from "vue";
import {EditorContext} from "#/services/editor-context/EditorContext.ts";
import type {AnyConfigObject, ConfigType} from "#/services/GlobalEditSession.ts";
import {useWorkbenchStore} from "#/stores/workbenchStore.ts";
import {useDialog, useMessage} from "naive-ui";

type EditorContextRef = Ref<EditorContext | null>;
const ActiveEditorContextKey: InjectionKey<EditorContextRef> = Symbol('ActiveEditorContext');

/**
 * @description 在顶层组件（如 WorkbenchView）中创建并提供 activeEditorContext。
 * 这个函数封装了 EditorContext 的创建、切换和关闭逻辑，并将其通过 provide 提供给后代组件。
 *
 * @returns 返回一个对象，包含响应式的 context 状态和控制其生命周期的方法，
 *          以便提供者组件自身也能使用和控制。
 */
export function createActiveEditorContextProvider()
{
    // --- 内部依赖 ---
    const workbenchStore = useWorkbenchStore();
    const message = useMessage();
    const dialog = useDialog();

    // --- 内部状态 ---
    const _activeContext = shallowRef<EditorContext | null>(null);
    const _isLoading = ref(false);

    /**
     * 切换或开始一个新的编辑会话，并创建对应的 EditorContext。
     * @param type - 资源类型
     * @param id - 资源的全局 ID
     */
    async function switchContext(type: ConfigType, id: string): Promise<void>
    {
        // 防止重复加载同一个会话
        if (_activeContext.value?.globalId === id)
        {
            return;
        }

        _isLoading.value = true;
        _activeContext.value = null; // 切换时先清空，UI会显示加载状态

        try
        {
            const session = await workbenchStore.acquireEditSession(type, id);
            if (session)
            {
                _activeContext.value = new EditorContext(session);
            }
            else
            {
                message.error(`无法开始编辑 “${id}”。资源可能不存在或已损坏。`);
            }
        } catch (error)
        {
            console.error(`切换到会话 (${type}, ${id}) 时发生意外错误:`, error);
            message.error('开始编辑时发生未知错误。');
            _activeContext.value = null;
        } finally
        {
            _isLoading.value = false;
        }
    }

    /**
     * 关闭当前激活的会话。
     * 如果有未保存的更改，会弹出确认框。
     */
    function closeContext(): void
    {
        const context = _activeContext.value;
        if (!context) return;

        if (context.isDirty)
        {
            dialog.warning({
                title: '关闭前确认',
                content: '当前有未保存的更改，您确定要关闭吗？所有未保存的更改都将丢失。',
                positiveText: '确定关闭',
                negativeText: '取消',
                onPositiveClick: () =>
                {
                    workbenchStore.closeSession(context.globalId);
                    _activeContext.value = null;
                    message.info('编辑会话已关闭。');
                },
            });
        }
        else
        {
            workbenchStore.closeSession(context.globalId);
            _activeContext.value = null;
        }
    }

    // 2. 使用 provide 将状态提供出去
    // 我们提供的是一个 Ref，这样后代组件就能响应其变化
    provide(ActiveEditorContextKey, _activeContext);

    // 3. 返回状态和方法，供提供者组件使用
    return {
        activeContext: readonly(_activeContext), // 对外提供只读版本
        isLoading: readonly(_isLoading),
        switchContext,
        closeContext,
    };
}

/**
 * @description 在后代组件中注入并使用 activeEditorContext。
 *
 * @returns 返回一个对当前激活 EditorContext 的只读 Ref。
 *          如果当前没有激活的 context，其 .value 将为 null。
 *          如果在提供者外部使用，将抛出错误。
 */
export function useActiveEditorContext()
{
    const activeContext = inject(ActiveEditorContextKey);

    if (!activeContext)
    {
        throw new Error('useActiveEditorContext() 必须在 createActiveEditorContextProvider() 的后代组件中使用。');
    }

    // 返回注入的 Ref，子组件可以通过 .value 访问 EditorContext 实例
    // 并且当 context 切换时，组件会自动更新
    return activeContext;
}

export function useSelectedConfig(selfID?: Ref<string>)
{
    const activeContext = useActiveEditorContext();
    const selectedContext = computed(() => activeContext.value?.selectedContext.value ?? null);
    const selectedType = computed(() => selectedContext.value?.type);
    const updateSelectedConfig = (configObject: (AnyConfigObject | null)) =>
    {
        return activeContext.value?.select(configObject);
    };
    const isSelected = computed(() =>
    {
        if (!selfID)
            return false;
        return activeContext.value?.selectedId.value === selfID.value;
    });
    return {
        selectedContext,
        selectedType,
        updateSelectedConfig,
        activeContext,
        isSelected,
    };
}