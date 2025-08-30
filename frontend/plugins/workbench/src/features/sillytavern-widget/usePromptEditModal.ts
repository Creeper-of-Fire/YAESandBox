// usePromptEditModal.ts
import {h} from 'vue';
import {useModal} from 'naive-ui';
import PromptEditModal from './PromptEditModal.vue';
import type {PromptItem} from './sillyTavernPreset';

export function usePromptEditModal()
{
    const modal = useModal();

    function open(initialData: PromptItem | Partial<PromptItem>)
    {
        return new Promise<PromptItem | null>((resolve) =>
        {
            const modalInstance = modal.create({
                title: initialData.identifier ? '编辑提示词' : '新建提示词',
                preset: 'card',
                style: {width: '90vw', maxWidth: '800px'},
                content: () => h(PromptEditModal, {
                    initialValue: initialData,
                    onSave: (updatedItem: PromptItem) =>
                    {
                        resolve(updatedItem);
                        modalInstance.destroy();
                    },
                    onCancel: () =>
                    {
                        resolve(null);
                        modalInstance.destroy();
                    },
                }),
                // Naive UI 的模态框点击遮罩默认会关闭，这里可以阻止并交由组件内部处理
                maskClosable: false,
            });
        });
    }

    return {open};
}