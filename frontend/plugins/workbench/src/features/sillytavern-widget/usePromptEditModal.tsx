import PromptEditPanel from './PromptEditPanel.vue';
import type {PromptItem} from './sillyTavernPreset';
import {ModalPromise, type SubmitExpose, useSaveableModal} from "@yaesandbox-frontend/shared-ui/modal";
import {ref} from "vue";

export function usePromptEditModal()
{
    const saveableModal = useSaveableModal();

    function open(initialData: PromptItem | Partial<PromptItem>): ModalPromise<PromptItem>
    {
        const formPanelRef = ref<SubmitExpose<PromptItem> | null>(null);

        return saveableModal.open({
            title: initialData.identifier ? '编辑提示词' : '新建提示词',
            preset: 'card',
            style: {width: '90vw', maxWidth: '800px'},

            // 提供 content
            content: () => (
                <PromptEditPanel
                    ref={formPanelRef}
                    initialValue={initialData}
                />
            ),

            // 提供 onSave 逻辑
            onSave: async () =>
            {
                const updatedItem = await formPanelRef.value?.submit();
                if (updatedItem)
                {
                    // 表单验证成功，返回数据
                    return updatedItem;
                }
                // 表单验证失败，抛出异常以阻止模态框关闭
                return null;
            }
        });
    }

    return {open};
}