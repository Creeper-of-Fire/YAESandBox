import {defineStore} from 'pinia';
import {computed, reactive, ref} from 'vue';
import {useMessage} from 'naive-ui';
import {v4 as uuidv4} from 'uuid'; // 用于在客户端生成UUID
import {cloneDeep} from 'lodash-es';
import {type AiConfigurationSet, AiConfigurationsService} from '#/types/generated/ai-config-api-client';

// 这是一个独立的工具函数，用于调用API并处理通用逻辑
async function callApi<T>(fn: () => Promise<T>, options?: { successMessage?: string, errorMessagePrefix?: string }): Promise<T | undefined>
{
    try
    {
        return await fn();
    } catch (error: any)
    {
        const detail = error.body?.detail || error.message || '未知错误';
        console.error("API Error:", error);
        throw new Error(detail);
    }
}

export const useAiConfigurationStore = defineStore('aiConfiguration', () =>
{
    // --- State ---
    const allConfigSets = reactive<Record<string, AiConfigurationSet>>({});
    const selectedUuid = ref<string | null>(null);
    const isLoading = ref(false);

    // --- Getters (Computed) ---
    const configSetOptions = computed(() =>
        Object.entries(allConfigSets)
            .map(([uuid, set]) => ({
                label: set.configSetName,
                value: uuid,
            }))
            .sort((a, b) => a.label.localeCompare(b.label))
    );

    const currentConfigSet = computed<AiConfigurationSet | null>(() =>
    {
        return selectedUuid.value ? allConfigSets[selectedUuid.value] ?? null : null;
    });

    // --- Actions ---

    async function fetchAllConfigSets()
    {
        isLoading.value = true;
        const response = await callApi(() => AiConfigurationsService.getApiAiConfigurations(), {
            errorMessagePrefix: '获取配置集列表失败'
        });

        // 清空并重新填充，确保响应性
        Object.keys(allConfigSets).forEach(key => delete allConfigSets[key]);
        if (response)
        {
            for (const uuid in response)
            {
                allConfigSets[uuid] = reactive(response[uuid]);
            }
        }

        // 验证当前选择是否仍然有效
        if (selectedUuid.value && !allConfigSets[selectedUuid.value])
        {
            selectedUuid.value = null;
        }
        isLoading.value = false;
    }

    /**
     * 保存当前选中的配置集。由于后端是幂等的PUT，此操作同时处理创建和更新。
     * @param configSetData 完整的、待保存的配置集对象
     */
    async function saveConfigSet(configSetData: AiConfigurationSet)
    {
        if (!selectedUuid.value)
        {
            throw new Error('没有选定的配置集可供保存。');
        }

        isLoading.value = true;
        await callApi(() => AiConfigurationsService.putApiAiConfigurations({
            uuid: selectedUuid.value!,
            requestBody: configSetData
        }), {
            successMessage: `配置集 "${configSetData.configSetName}" 保存成功！`,
            errorMessagePrefix: '保存失败'
        });

        // 保存成功后，用返回的数据（如果有）或传入的数据更新本地状态
        allConfigSets[selectedUuid.value] = reactive(configSetData);
        isLoading.value = false;
    }

    async function createNewSet(name: string)
    {
        if (!name.trim())
        {
            throw new Error('配置集名称不能为空！');
        }

        const newUuid = uuidv4(); // 在客户端生成UUID
        const newSet: AiConfigurationSet = {
            configSetName: name,
            configurations: {},
        };

        isLoading.value = true;
        // 使用PUT进行创建
        await callApi(() => AiConfigurationsService.putApiAiConfigurations({
            uuid: newUuid,
            requestBody: newSet,
        }), {
            successMessage: `配置集 "${name}" 创建成功！`,
            errorMessagePrefix: '创建失败'
        });

        // 重新拉取列表以确保同步，并选中新的
        await fetchAllConfigSets();
        selectedUuid.value = newUuid;
        isLoading.value = false;
    }

    async function cloneCurrentSet(newName: string)
    {
        if (!currentConfigSet.value) return;

        const newUuid = uuidv4();
        const clonedSet: AiConfigurationSet = {
            ...cloneDeep(currentConfigSet.value),
            configSetName: newName,
        };

        isLoading.value = true;
        await callApi(() => AiConfigurationsService.putApiAiConfigurations({
            uuid: newUuid,
            requestBody: clonedSet,
        }), {
            successMessage: `配置集副本 "${newName}" 创建成功！`,
            errorMessagePrefix: '复制失败'
        });

        await fetchAllConfigSets();
        selectedUuid.value = newUuid;
        isLoading.value = false;
    }

    async function renameCurrentSet(newName: string)
    {
        if (!currentConfigSet.value || !selectedUuid.value) return;
        if (newName === currentConfigSet.value.configSetName) return;

        const updatedSet = {
            ...currentConfigSet.value,
            configSetName: newName,
        };

        isLoading.value = true;
        await callApi(() => AiConfigurationsService.putApiAiConfigurations({
            uuid: selectedUuid.value!,
            requestBody: updatedSet
        }), {
            successMessage: '名称修改成功！',
            errorMessagePrefix: '修改名称失败'
        });

        // 乐观更新UI
        if (allConfigSets[selectedUuid.value])
        {
            allConfigSets[selectedUuid.value].configSetName = newName;
        }
        isLoading.value = false;
    }

    async function deleteCurrentSet()
    {
        if (!selectedUuid.value || !currentConfigSet.value) return;

        const uuidToDelete = selectedUuid.value;
        const nameToDelete = currentConfigSet.value.configSetName;

        isLoading.value = true;
        await callApi(() => AiConfigurationsService.deleteApiAiConfigurations({uuid: uuidToDelete}), {
            // 成功时不显示消息，因为删除后UI状态会变化
            errorMessagePrefix: `删除配置集 "${nameToDelete}" 失败`
        });

        // 乐观更新UI
        delete allConfigSets[uuidToDelete];
        selectedUuid.value = null;

        // 如果删除后一个都没有了，最好再拉取一次，以防万一（例如后端自动创建了默认的）
        if (Object.keys(allConfigSets).length === 0)
        {
            await fetchAllConfigSets();
        }
        isLoading.value = false;
    }

    return {
        // State
        allConfigSets,
        selectedUuid,
        isLoading,
        // Getters
        configSetOptions,
        currentConfigSet,
        // Actions
        fetchAllConfigSets,
        saveConfigSet,
        createNewSet,
        cloneCurrentSet,
        renameCurrentSet,
        deleteCurrentSet,
    };
});