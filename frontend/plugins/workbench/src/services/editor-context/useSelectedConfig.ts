import {computed, type DeepReadonly, inject, type InjectionKey, provide, readonly, ref, type Ref, shallowRef} from "vue";
import {EditorContext} from "#/services/editor-context/EditorContext.ts";
import type {AnyConfigObject, ConfigType} from "#/services/GlobalEditSession.ts";
import {useWorkbenchStore} from "#/stores/workbenchStore.ts";
import {useDialog, useMessage} from "naive-ui";

// --- 定义注入载荷的类型 ---
interface EditorControlPayload
{
    activeContext: Ref<EditorContext | null>;
    isLoading: Readonly<Ref<boolean>>;
    switchContext: (type: ConfigType, storeId: string) => Promise<void>;
    closeContext: () => void;
    deepCloseContext: () => void;
}

const EditorControlPayloadKey: InjectionKey<EditorControlPayload> = Symbol('ActiveEditorContext');

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
     * @param storeId - 资源的存储 ID
     */
    async function switchContext(type: ConfigType, storeId: string): Promise<void>
    {
        // 防止重复加载同一个会话
        if (_activeContext.value?.storeId === storeId)
        {
            // 如果已经是当前会话，则将根对象设为选中状态
            if (_activeContext.value.data)
            {
                _activeContext.value.select(_activeContext.value.session.getData().value);
            }
            return;
        }

        _isLoading.value = true;
        _activeContext.value = null; // 切换时先清空，UI会显示加载状态

        try
        {
            const session = await workbenchStore.acquireEditSession(type, storeId);
            if (session)
            {
                _activeContext.value = new EditorContext(session);
            }
            else
            {
                message.error(`无法开始编辑 “${storeId}”。资源可能不存在或已损坏。`);
            }
        } catch (error)
        {
            console.error(`切换到会话 (${type}, ${storeId}) 时发生意外错误:`, error);
            message.error('开始编辑时发生未知错误。');
            _activeContext.value = null;
        } finally
        {
            _isLoading.value = false;
        }
    }

    /**
     * 关闭当前激活的会话。
     * 只是简单的关闭，不会关闭“后台”的会话。
     * 对于大多数情况下的需求，请使用 closeContext()，因为我们在切换视图时并不希望真正的关闭会话，彻底关闭会话可能导致糟糕的UI/UX体验，甚至有可能导致非预期的错误。
     */
    function closeContext(): void
    {
        const context = _activeContext.value;
        if (!context) return;
        _activeContext.value = null;
    }

    /**
     * 彻底关闭当前激活的会话。
     * 如果有未保存的更改，会弹出确认框。
     */
    function deepCloseContext(): void
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
                    workbenchStore.closeSession(context.storeId);
                    _activeContext.value = null;
                    message.info('编辑会话已关闭。');
                },
            });
        }
        else
        {
            workbenchStore.closeSession(context.storeId);
            _activeContext.value = null;
        }
    }

    // --- 将状态和方法打包成一个对象 ---
    const payload: EditorControlPayload = {
        activeContext: _activeContext,
        isLoading: readonly(_isLoading),
        switchContext,
        closeContext,
        deepCloseContext,
    };

    // --- 提供完整的载荷对象 ---
    provide(EditorControlPayloadKey, payload);

    // 3. 返回状态和方法，供提供者组件使用
    return {
        activeContext: readonly(_activeContext), // 对外提供只读版本
        isLoading: readonly(_isLoading),
        switchContext,
        closeContext,
    };
}

/**
 * @description 在后代组件中注入并使用 activeEditorContext 及其控制器。
 */
export function useEditorControlPayload()
{
    const payload = inject(EditorControlPayloadKey);

    if (!payload)
    {
        throw new Error('useEditorControlPayload() 必须在 createActiveEditorContextProvider() 的后代组件中使用。');
    }

    return payload;
}

export function useSelectedConfig(selfID?: Ref<string>)
{
    const editorControlPayload = useEditorControlPayload();
    const activeContext = editorControlPayload.activeContext;

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
    const isReadOnly = computed(() => activeContext.value?.isReadOnly.value ?? false);
    return {
        switchContext: editorControlPayload.switchContext,
        closeContext: editorControlPayload.closeContext,
        isLoading: editorControlPayload.isLoading,
        selectedContext,
        selectedType,
        updateSelectedConfig,
        activeContext,
        isSelected,
        isReadOnly,
    };
}