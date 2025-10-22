// src/composables/useConfigImportExport.ts
import {useMessage} from 'naive-ui';
import type {StoredConfig} from '#/stores/workbenchStore';
import {useWorkbenchStore} from '#/stores/workbenchStore';
import type {AnyResourceItemSuccess} from '#/services/GlobalEditSession';
import type {AnyConfigObject} from "@yaesandbox-frontend/core-services/types";

/**
 * 提供全局配置导入导出功能的 Composable
 */
export function useConfigImportExport()
{
    const message = useMessage();
    const workbenchStore = useWorkbenchStore();

    /**
     * 导出指定的配置项为 JSON 文件。
     * @param item - 要导出的完整资源项 (必须是成功的项)。
     */
    const exportConfig = (item: AnyResourceItemSuccess) =>
    {
        if (!item || !item.isSuccess)
        {
            message.error('无法导出已损坏或不存在的配置。');
            return;
        }

        try
        {
            // 1. 从前端视图模型 (AnyResourceItemSuccess) 重构为后端的 StoredConfig 结构
            const storedConfigToExport: StoredConfig<AnyConfigObject> = {
                content: item.data,
                isReadOnly: item.isReadOnly,
                meta: item.meta,
                storeRef: item.storeRef,
            };

            // 2. 转换为 JSON 字符串
            const jsonString = JSON.stringify(storedConfigToExport, null, 2);
            const blob = new Blob([jsonString], {type: 'application/json'});
            const url = URL.createObjectURL(blob);

            // 3. 创建并触发下载
            const a = document.createElement('a');
            a.href = url;
            // 文件名设置为 config.name.json
            a.download = `${item.data.name}.json`;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);

            message.success(`已开始导出 "${item.data.name}"`);
        } catch (error)
        {
            console.error('导出配置时出错:', error);
            message.error('导出失败，请查看控制台获取详情。');
        }
    };

    /**
     * 触发文件选择器以导入 JSON 配置文件。
     */
    const importConfig = () =>
    {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = '.json,application/json';

        input.onchange = (event) =>
        {
            const file = (event.target as HTMLInputElement).files?.[0];
            if (!file) return;

            const reader = new FileReader();
            reader.onload = async (e) =>
            {
                try
                {
                    const content = e.target?.result as string;
                    const importedObject = JSON.parse(content);

                    // 4. 简单校验导入对象的结构是否符合 StoredConfig
                    if (importedObject && typeof importedObject.content === 'object' && typeof importedObject.content.name === 'string')
                    {
                        const storedConfig = importedObject as StoredConfig<AnyConfigObject>;

                        // 5. 调用 store 的方法创建新的全局配置
                        // createGlobalConfig 内部会处理ID刷新
                        await workbenchStore.createGlobalConfig(storedConfig.content, {
                            meta: storedConfig.meta,
                            storeRef: storedConfig.storeRef
                        });

                        message.success(`成功导入配置: "${storedConfig.content.name}"`);
                    }
                    else
                    {
                        throw new Error('文件格式无效，缺少 "content" 或 "content.name" 字段。');
                    }
                } catch (err)
                {
                    console.error('导入并解析文件时出错:', err);
                    message.error(`导入失败: ${(err as Error).message}`);
                }
            };
            reader.readAsText(file);
        };

        input.click();
    };

    return {
        exportConfig,
        importConfig,
    };
}