// src/stores/schemaStore.ts
import {defineStore} from 'pinia';
import {ref} from 'vue';
import {AiConfigSchemasService} from '@/types/generated/aiconfigapi/services/AiConfigSchemasService.ts'; // Adjust path

export const useAiConfigSchemaStore = defineStore('schemaStore', () =>
{
    const schemas = ref<Record<string, Record<string, any>>>({});
    const isLoading = ref<Record<string, boolean>>({});
    const fetchError = ref<Record<string, string | null>>({});

    async function getOrFetchSchema(configTypeName: string): Promise<Record<string, any> | undefined>
    {
        if (schemas.value[configTypeName])
        {
            return schemas.value[configTypeName];
        }
        if (isLoading.value[configTypeName])
        {
            return undefined; // Indicate loading
        }

        isLoading.value[configTypeName] = true;
        fetchError.value[configTypeName] = null;

        try
        {
            const schema = await AiConfigSchemasService.getApiAiConfigurationManagementSchemas({configTypeName});
            if (schema)
            {
                schemas.value[configTypeName] = schema; // Cache it
                return schema;
            } else
            {
                fetchError.value[configTypeName] = `Received empty schema response for ${configTypeName}.`;
                console.error(fetchError.value[configTypeName]);
                return undefined;
            }
        } catch (error: any)
        {
            const errorMessage = `Failed to fetch schema for ${configTypeName}: ${error.body?.detail || error.message || 'Unknown error'}`;
            fetchError.value[configTypeName] = errorMessage;
            console.error(errorMessage, error);
            return undefined; // Indicate error
        } finally
        {
            isLoading.value[configTypeName] = false;
        }
    }

    // Getters (暴露给组件使用)
    const getSchema = (configTypeName: string): Record<string, any> | undefined => schemas.value[configTypeName];
    const isSchemaLoading = (configTypeName: string): boolean => !!isLoading.value[configTypeName];
    const getSchemaError = (configTypeName: string): string | null => fetchError.value[configTypeName] ?? null;

    return {
        getOrFetchSchema,
        getSchema,
        isSchemaLoading,
        getSchemaError,
    };
});