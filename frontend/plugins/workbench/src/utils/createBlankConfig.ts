// 文件路径: src/app-workbench/utils/createBlankConfig.ts
import {v4 as uuidv4} from 'uuid';
import type {AnyConfigObject, ConfigType} from "@yaesandbox-frontend/core-services/types";
import {
    type AbstractRuneConfig,
    RuneConfigService,
    type TuumConfig,
    type WorkflowConfig
} from "#/types/generated/workflow-config-api-client";

// noinspection JSCommentMatchesSignature
/**
 * 创建一个空白的配置对象。
 * @param type - 要创建的配置类型。
 * @param name - 新配置的名称。
 * @param options - 额外选项，例如创建符文时需要指定 runeType。
 * @returns 一个 Promise，它将解析为一个全新的、符合规范的配置对象。
 */
export async function createBlankConfig(
    type: 'workflow',
    name: string
): Promise<WorkflowConfig>;
export async function createBlankConfig(
    type: 'tuum',
    name: string
): Promise<TuumConfig>;
export async function createBlankConfig(
    type: 'rune',
    name: string,
    options: { runeType: string }
): Promise<AbstractRuneConfig>;
export async function createBlankConfig(
    type: ConfigType,
    name: string,
    options?: { runeType?: string }
): Promise<AnyConfigObject>
{
    const newConfigId = uuidv4();
    let newConfig;

    switch (type)
    {
        case 'workflow':
            newConfig = {
                // 工作流没有内部 configId，它是顶级容器
                name: name,
                workflowInputs: [],
                tuums: [],
                connections: [],
            };
            return Promise.resolve(newConfig);
        case 'tuum':
            newConfig = {
                configId: newConfigId,
                name: name,
                enabled: true,
                runes: [],
                inputMappingsList: [],
                outputMappingsList: [],
            };
            return Promise.resolve(newConfig);
        case 'rune':
            // --- 核心改动 4: 调用后端 API ---
            if (!options?.runeType)
            {
                // 这个检查依然重要
                return Promise.reject(new Error('创建空白符文时必须提供 runeType！'));
            }

            try
            {
                // 1. 调用后端权威端点，获取包含所有默认值的“基础模板”
                const baseRuneConfig = await RuneConfigService.getApiV1WorkflowsConfigsGlobalRunesNewRune({
                    runeTypeName: options.runeType,
                });

                // 2. 在前端为这个模板赋予上下文身份（新的ID和用户指定的名称）
                //    使用对象扩展(...)来合并，并覆盖 id 和 name
                return {
                    ...baseRuneConfig,
                    configId: newConfigId,
                    name: name,
                }; // 在 async 函数中直接返回，它会被自动包装在 Promise 中

            } catch (error)
            {
                // 如果 API 调用失败，将错误抛出，让上层代码的 try/catch 块可以捕获它
                console.error(`从后端获取新的符文配置 '${options.runeType}' 失败:`, error);
                throw error;
            }
        default:
            throw new Error(`未知的配置类型: ${type}`);
    }
}