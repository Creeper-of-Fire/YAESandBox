import {defineStore} from 'pinia';
import {computed, ref} from 'vue';
import {
    AiConfigSchemasService,
    type AiConfigTypeWithSchemaDto
} from '#/types/generated/ai-config-api-client';

export const useAiConfigSchemaStore = defineStore('aiConfigSchema', () => {
    // --- State ---
    const definitions = ref<Record<string, AiConfigTypeWithSchemaDto>>({});
    const isLoading = ref(false);
    const error = ref<string | null>(null);

    // --- Action ---
    async function fetchAllDefinitions() {
        if (Object.keys(definitions.value).length > 0) return; // 已经加载过了

        isLoading.value = true;
        error.value = null;
        try {
            const response = await AiConfigSchemasService.getApiAiConfigurationManagementDefinitions();
            const defsMap: Record<string, AiConfigTypeWithSchemaDto> = {};
            for (const def of response) {
                defsMap[def.value] = def;
            }
            definitions.value = defsMap;
        } catch (e: any) {
            const errorMessage = `加载AI模型类型定义失败: ${e.body?.detail || e.message || '未知错误'}`;
            error.value = errorMessage;
            console.error(errorMessage, e);
        } finally {
            isLoading.value = false;
        }
    }

    // --- Getters ---
    const availableTypesOptions = computed(() =>
        Object.values(definitions.value)
            .map(def => ({
                label: def.label,
                value: def.value,
            }))
            .sort((a, b) => a.label.localeCompare(b.label))
    );

    const allSchemas = computed(() => Object.values(definitions.value));

    function getSchemaByName(typeName: string): Record<string, any> | undefined {
        return definitions.value[typeName]?.schema;
    }

    return {
        // State
        isLoading,
        error,
        // Getter
        availableTypesOptions,
        allSchemas,
        // Action
        fetchAllDefinitions,
        getSchemaByName,
    };
});