import {ref} from 'vue';
import type {EntityFieldSchema} from '@yaesandbox-frontend/core-services/composables';
import EntityFormPanel from './EntityFormPanel.vue';
import type {ModalPromise} from "@yaesandbox-frontend/shared-ui/modal";
import {useSaveableModal,type SubmitExpose} from "@yaesandbox-frontend/shared-ui/modal";

// 定义 open 方法的选项
interface OpenEditorOptions<T extends Record<string, any>, TMode extends 'create' | 'edit' | 'complete'>
{
    mode: TMode;
    entityName: string;
    schema: EntityFieldSchema[];
    initialData: TMode extends 'edit' ? T : (Partial<T> | null);
    /**
     * 当用户点击保存且表单验证成功后执行的业务逻辑。
     * 这个函数是整个保存流程的一部分。如果它抛出异常，模态框将不会关闭。
     * @param data 经过验证的表单数据。
     */
    onSave: (data: SaveDataType<T, TMode>) => void | Promise<void>;
    /**
     * 当用户点击取消时执行的逻辑。
     */
    onCancel?: () => void;
}

// 推断 Promise 返回的类型
type SaveDataType<T, TMode> = TMode extends 'edit' | 'complete' ? T : Omit<T, 'id'>;

export function useEntityEditorModal<T extends Record<string, any>>()
{
    const saveableModal = useSaveableModal();

    function open<TMode extends 'create' | 'edit' | 'complete'>(
        options: OpenEditorOptions<T, TMode>
    ): ModalPromise<SaveDataType<T, TMode>>
    {

        const formPanelRef = ref<SubmitExpose<SaveDataType<T, TMode>> | null>(null);

        return saveableModal.open({
            title: `${options.mode === 'edit' ? '编辑' : (options.mode === 'complete' ? '补全' : '新建')}${options.entityName}`,
            style: {width: '90vw', maxWidth: '600px'},
            preset: 'card',

            // 提供 content
            content: () => (
                <EntityFormPanel
                    ref={formPanelRef}
                    schema={options.schema}
                    initialData={options.initialData}
                />
            ),

            // 提供 onSave 逻辑
            onSave: async () =>
            {
                // 触发表单验证和提交
                const savedData = await formPanelRef.value?.submit();
                if (savedData)
                {
                    // 表单验证成功，执行用户传入的业务逻辑
                    // 我们 await 它，以确保 loading 状态覆盖其执行时间
                    await options.onSave(savedData)
                    // 用户的业务逻辑成功后，返回数据以通知 useSaveableModal 关闭模态框
                    return savedData;
                }
                // 表单验证失败，`submit` 返回 null。
                return null;
            },
        });
    }

    return {open};
}