// src/composables/useAiConfigActions.ts
import {ref, h, type Ref, type ComputedRef, type UnwrapNestedRefs} from 'vue';
import {NInput, useMessage, useDialog} from 'naive-ui'; // 假设这些可以直接在这里用，或者从父组件传入
import type {AiConfigurationSet} from '@/types/generated/aiconfigapi/models/AiConfigurationSet';
import {AiConfigurationsService} from '@/types/generated/aiconfigapi/services/AiConfigurationsService';
import type {AbstractAiProcessorConfig} from "@/types/generated/aiconfigapi/models/AbstractAiProcessorConfig";
import type {DynamicFormRendererInstance} from '@/components/schema/DynamicFormRenderer.vue';
import {cloneDeep} from "lodash-es"; // 假设你导出了这个类型

// 定义传递给可组合函数的参数类型
interface UseAiConfigActionsParams {
    allConfigSets: UnwrapNestedRefs<Record<string, AiConfigurationSet>>;
    selectedConfigSetUuid: Ref<string | null>;
    currentConfigSet: ComputedRef<AiConfigurationSet | null>;
    selectedAiModuleType: Ref<string | null>;
    formDataCopy: Ref<AbstractAiProcessorConfig | null>;
    formChanged: Ref<boolean>;
    currentSchema: Ref<Record<string, any> | null>;
    dynamicFormRendererRef: Ref<DynamicFormRendererInstance | null>;
    callApi: <T>(fn: () => Promise<T>, successMessage?: string, autoHandleError?: boolean) => Promise<T | undefined>;
    fetchAllConfigSets: () => Promise<void>;
    resetFormChangeFlag: () => void; // 或者直接操作 formChanged.value
    // Naive UI services - 最好从调用方传入，而不是在这里重新 useMessage/useDialog
    // 这样可以确保使用的是同一个 message/dialog 上下文
    message: ReturnType<typeof useMessage>;
    dialog: ReturnType<typeof useDialog>;
}

export function useAiConfigActions(params: UseAiConfigActionsParams) {
    const {
        allConfigSets,
        selectedConfigSetUuid,
        currentConfigSet,
        selectedAiModuleType,
        formDataCopy,
        formChanged,
        currentSchema,
        dynamicFormRendererRef,
        callApi,
        fetchAllConfigSets,
        resetFormChangeFlag,
        message,
        dialog,
    } = params;

    // ----------- 配置集操作逻辑 -----------
    // 保存对当前配置集的所有变更 (集成了校验)
    async function handleSaveConfigSet() {
        if (!currentConfigSet.value || !selectedConfigSetUuid.value) {
            message.error('没有选中的配置集可以保存！');
            return;
        }

        // 1. 手动触发表单校验 (通过 DynamicFormRenderer 实例)
        if (dynamicFormRendererRef.value && selectedAiModuleType.value && currentSchema.value) {
            try {
                await dynamicFormRendererRef.value.validate(); // 调用暴露的方法
            } catch (errors: any) {
                message.error('表单校验失败，请检查红色标记的字段。');
                console.warn('表单校验失败:', errors);
                return; // 校验失败，不继续保存
            }
        } else if (selectedAiModuleType.value && currentSchema.value) {
            console.warn('无法访问表单渲染器实例进行校验，将不经验证地保存。');
        }

        // 如果 selectedAiModuleType.value 和 formDataCopy.value 有效，更新配置集中的数据
        // 这部分逻辑保持不变
        if (selectedAiModuleType.value && currentConfigSet.value && formDataCopy.value !== null) {
            currentConfigSet.value.configurations[selectedAiModuleType.value] = cloneDeep(formDataCopy.value);
            resetFormChangeFlag(); // 假设这个函数还在
        }

        // 2. 调用 API 保存 (逻辑不变)
        await callApi(() => AiConfigurationsService.putApiAiConfigurations({
            uuid: selectedConfigSetUuid.value!,
            requestBody: currentConfigSet.value!,
        }), '配置集保存成功！');
        // 保存成功后，由于 formDataCopy 已经是最新状态，originalData 也会是这个状态，
        // 下次 checkFormChange 会是 false。所以 formChanged.value 应该在API调用成功后重置。
        // 如果你是通过 resetFormChangeFlag() 做的，确保它被正确调用。
        // 通常，如果保存成功，我会将 formChanged.value = false;
        formChanged.value = false; // 直接在这里重置
    }

    // 提示并执行新建配置集
    function promptCreateNewSet() {
        const newSetNameInput = ref('');
        dialog.create({
            title: '新建 AI 配置集',
            content: () => h(NInput, {
                value: newSetNameInput.value,
                onUpdateValue: (val) => newSetNameInput.value = val,
                placeholder: '请输入新配置集的名称',
                autofocus: true,
            }),
            positiveText: '创建',
            negativeText: '取消',
            onPositiveClick: async () => {
                const name = newSetNameInput.value.trim();
                if (!name) {
                    message.error('配置集名称不能为空！');
                    return false; //阻止对话框关闭
                }
                const newSetToCreate: AiConfigurationSet = {
                    configSetName: name,
                    configurations: {},
                };
                const newUuid = await callApi(() => AiConfigurationsService.postApiAiConfigurations({requestBody: newSetToCreate}), `配置集 "${name}" 创建成功!`);
                if (newUuid) {
                    await fetchAllConfigSets(); // 重新加载所有配置集
                    selectedConfigSetUuid.value = newUuid; // 自动选中新创建的
                }
            }
        });
    }

    // 提示并执行复制当前配置集
    function promptCloneSet() {
        if (!currentConfigSet.value) return;
        const clonedNameInput = ref(`${currentConfigSet.value.configSetName} (副本)`);
        dialog.create({
            title: '复制配置集',
            content: () => h(NInput, {
                value: clonedNameInput.value,
                onUpdateValue: (val) => clonedNameInput.value = val,
                placeholder: '请输入副本配置集的名称'
            }),
            positiveText: '复制',
            negativeText: '取消',
            onPositiveClick: async () => {
                const name = clonedNameInput.value.trim();
                if (!name) {
                    message.error('配置集名称不能为空！');
                    return false;
                }
                const setToClone: AiConfigurationSet = {
                    configSetName: name,
                    configurations: JSON.parse(JSON.stringify(currentConfigSet.value!.configurations)) // 深拷贝
                };
                const newUuid = await callApi(() => AiConfigurationsService.postApiAiConfigurations({requestBody: setToClone}), `配置集 "${name}" (副本) 创建成功!`);
                if (newUuid) {
                    await fetchAllConfigSets();
                    selectedConfigSetUuid.value = newUuid;
                }
            }
        });
    }

    // 提示并执行修改当前配置集名称
    function promptRenameSet() {
        if (!currentConfigSet.value || !selectedConfigSetUuid.value) return;
        const newNameInput = ref(currentConfigSet.value.configSetName);
        const originalUuid = selectedConfigSetUuid.value; // 捕获当前uuid

        dialog.create({
            title: '修改配置集名称',
            content: () => h(NInput, {
                value: newNameInput.value,
                onUpdateValue: (val) => newNameInput.value = val,
                placeholder: '请输入新的配置集名称'
            }),
            positiveText: '保存名称',
            negativeText: '取消',
            onPositiveClick: async () => {
                const name = newNameInput.value.trim();
                if (!name) {
                    message.error('配置集名称不能为空！');
                    return false;
                }
                if (name === allConfigSets[originalUuid]?.configSetName) { // 名称未改变
                    return;
                }

                // Optimistic UI update or prepare for save
                // Here, we choose to update and mark for "Save Changes"
                // or, if you want immediate save:
                const setToUpdate: AiConfigurationSet = {
                    ...allConfigSets[originalUuid], // 获取最新的数据
                    configSetName: name,
                };

                await callApi(() => AiConfigurationsService.putApiAiConfigurations({
                    uuid: originalUuid,
                    requestBody: setToUpdate
                }), `配置集名称已修改为 "${name}"`);

                // Manually update the name in the local reactive store for immediate UI feedback
                if (allConfigSets[originalUuid]) {
                    allConfigSets[originalUuid].configSetName = name;
                }
                // If the currentConfigSet is the one being renamed, its name will update reactively.
                // No need to re-fetch all, just update local state.
            }
        });
    }

    // 执行删除当前配置集
    async function executeDeleteSet() {
        if (!selectedConfigSetUuid.value || !currentConfigSet.value) return;
        const setName = currentConfigSet.value.configSetName; // 保存名称用于提示
        await callApi(() => AiConfigurationsService.deleteApiAiConfigurations({uuid: selectedConfigSetUuid.value!}));

        message.success(`配置集 "${setName}" 已删除!`);
        // 后端保证至少有一个配置集，但如果删除的是最后一个，前端需要有合理行为
        // 通常是重新拉取列表，如果列表空了，UI应有提示
        const oldSelectedUuid = selectedConfigSetUuid.value;
        selectedConfigSetUuid.value = null; // 清空选择
        selectedAiModuleType.value = null;
        currentSchema.value = null;

        // 从 allConfigSets 中移除已删除的项
        if (allConfigSets[oldSelectedUuid]) {
            delete allConfigSets[oldSelectedUuid];
        }
        // 如果 allConfigSets 为空，或者需要选择一个默认项，可以在这里处理
        // 考虑到后端保证至少有一个，这里可能不需要特殊处理空列表的情况
        // 但如果删除了当前选中的，最好是清空选择，或者选中列表中的第一个（如果存在）
        if (Object.keys(allConfigSets).length > 0 && !selectedConfigSetUuid.value) {
            // selectedConfigSetUuid.value = Object.keys(allConfigSets)[0]; // 可选：默认选中第一个
        } else if (Object.keys(allConfigSets).length === 0) {
            // 如果删到空了（理论上后端不应允许），这里可以再次拉取确保同步
            await fetchAllConfigSets();
        }
    }

    // ----------- AI 类型与表单逻辑 -----------
    // 删除当前选中的 AI 配置
    async function handleRemoveCurrentAiConfig() {
        // 可用性已在模板 popconfirm 的 v-if 和 disabled 中控制，此处不再需要额外的 if 检查参数有效性
        const moduleTypeToRemove = selectedAiModuleType.value!;
        const currentConfigSetData = currentConfigSet.value!; // 已确保存在

        if (currentConfigSetData.configurations && currentConfigSetData.configurations[moduleTypeToRemove]) {
            // **核心修改：删除对应的键值对**
            delete currentConfigSetData.configurations[moduleTypeToRemove];

            message.info(`模型 "${moduleTypeToRemove}" 的配置已从配置集中移除。请点击“保存变更”以应用。`);
            selectedAiModuleType.value = null;

            // 删除后，当前选中的 AI 类型实际上已经没有对应的配置数据了
            // 如果希望 UI 立即反映移除状态（表单消失），可以清空 selectedAiModuleType
            // selectedAiModuleType.value = null; // 可选，取决于交互流程
            // 清空后需要重新触发选择AI类型或显示空状态
            // 如果不清空，表单区域也会因为 v-if 条件不满足而消失
            // 为了避免 watch selectedAiModuleType 再次触发加载 schema/数据，不清空可能更好
            // 让表单区域消失，并提示用户选择其他AI类型

            // 强制 vue-form 重新渲染通常不再需要，因为 v-if 条件变化会触发其销毁和重建（如果下次再选中）
            // formRenderKey.value++; // 移除或保留看实际测试效果
        } else {
            // 理论上不会走到这里，因为按钮已禁用
            message.warning('当前AI模型配置不存在或已被移除。');
        }
    }

    return {
        handleSaveConfigSet,
        promptCreateNewSet,
        promptCloneSet,
        promptRenameSet,
        executeDeleteSet,
        handleRemoveCurrentAiConfig,
    };
}