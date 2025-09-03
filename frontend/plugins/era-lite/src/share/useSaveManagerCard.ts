import {computed, h, ref} from 'vue';
import {NInput, useDialog, useMessage} from 'naive-ui';
import {useEraLiteSaveStore} from "#/stores/useEraLiteSaveStore.ts";

/**
 * 这是一个专门为 SaveManagerCard.vue 服务的 Composable (Presenter)。
 * 它包含了所有与UI交互相关的逻辑，如弹窗、确认、消息提示等。
 */
export function useSaveManagerCard()
{
    const dialog = useDialog();
    const message = useMessage();
    const {saveSlotManager} = useEraLiteSaveStore();

    // 直接从管理器暴露状态
    const {slots, activeSlotId} = saveSlotManager;
    const activeSlot = computed(() => slots.value.find(s => s.id === activeSlotId.value));

    /**
     * 处理用户点击存档槽的核心逻辑。
     */
    async function handleSlotClick(slotId: string)
    {
        const targetSlot = slots.value.find(s => s.id === slotId);
        if (!targetSlot) return;

        if (targetSlot.type === 'autosave')
        {
            // 点击自动存档，直接切换
            await saveSlotManager.selectAutosave(targetSlot.id);
            message.success(`已切换到自动存档: ${targetSlot.name}`);
        }
        else
        {
            // 点击快照，检查是否存在同名自动存档
            const baseName = targetSlot.name;
            const existingAutosave = saveSlotManager.findAutosaveByName(baseName);

            if (existingAutosave)
            {
                // 如果存在同名自动存档，弹窗询问用户是切换，还是创建一个带后缀的新自动存档
                dialog.info({
                    title: `加载快照 "${baseName}"`,
                    content: `已存在一个名为 "${baseName}" 的自动存档。你想直接切换到它，还是从快照创建一个新的副本？`,
                    positiveText: '切换到现有自动存档',
                    negativeText: '创建新副本', // 这里的 "取消" 按钮变成了更有用的操作
                    closable: true, // 允许用户点击关闭按钮取消操作
                    onPositiveClick: async () =>
                    {
                        await saveSlotManager.selectAutosave(existingAutosave.id);
                        message.info(`已切换到现有自动存档: ${existingAutosave.name}`);
                    },
                    onNegativeClick: async () =>
                    {
                        // --- 自动寻找一个不冲突的新名字 ---
                        let newAutosaveName = '';
                        let counter = 1;
                        // 循环查找 "存档名 (1)", "存档名 (2)" ... 直到找到一个可用的
                        do
                        {
                            newAutosaveName = `${baseName} (${counter})`;
                            counter++;
                        } while (saveSlotManager.findAutosaveByName(newAutosaveName));

                        message.loading(`正在从快照创建新副本 "${newAutosaveName}"...`, {duration: 3000});
                        await saveSlotManager.loadFromSnapshot(targetSlot.id, newAutosaveName);
                        message.success(`已创建并加载新的自动存档: ${newAutosaveName}`);
                    }
                });

            }
            else
            {
                // 如果不存在同名自动存档，直接从快照加载
                await saveSlotManager.loadFromSnapshot(targetSlot.id, baseName);
                message.success(`已从快照 "${baseName}" 创建并加载新的自动存档`);
            }
        }
    }

    /**
     * 处理创建新自动存档的UI流程。
     */
    function handleCreateAutosave()
    {
        const inputValue = ref('');
        dialog.create({
            title: '新建自动存档',
            content: () => h(NInput, {
                value: inputValue.value,
                'onUpdate:value': v => (inputValue.value = v),
                placeholder: '请输入自动存档的名称'
            }),
            positiveText: '创建',
            onPositiveClick: async () =>
            {
                if (inputValue.value)
                {
                    await saveSlotManager.createAutosave(inputValue.value);
                    message.success(`自动存档 "${inputValue.value}" 已创建`);
                }
            }
        });
    }

    /**
     * 处理创建快照的UI流程。
     */
    function handleCreateSnapshot()
    {
        const inputValue = ref('');
        dialog.create({
            title: '创建永久快照',
            content: () => h(NInput, {
                value: inputValue.value,
                'onUpdate:value': v => (inputValue.value = v),
                placeholder: '请输入快照名称'
            }),
            positiveText: '创建',
            onPositiveClick: async () =>
            {
                if (inputValue.value)
                {
                    await saveSlotManager.createSnapshot(inputValue.value);
                    message.success(`快照 "${inputValue.value}" 已创建`);
                }
            }
        });
    }

    return {
        slots,
        activeSlot,
        activeSlotId,
        handleSlotClick,
        handleCreateAutosave,
        handleCreateSnapshot
    };
}