﻿// 文件路径: src/app-workbench/utils/createBlankConfig.ts
import {v4 as uuidv4} from 'uuid';
import type {ConfigObject, ConfigType,} from '@/app-workbench/services/EditSession';
import type {
    AbstractModuleConfig,
    StepProcessorConfig,
    WorkflowProcessorConfig
} from "@/app-workbench/types/generated/workflow-config-api-client";

// noinspection JSCommentMatchesSignature
/**
 * 创建一个空白的配置对象。
 * @param type - 要创建的配置类型。
 * @param name - 新配置的名称。
 * @param options - 额外选项，例如创建模块时需要指定 moduleType。
 * @returns 一个全新的、符合规范的空白配置对象。
 */
export function createBlankConfig(
    type: 'workflow',
    name: string
): WorkflowProcessorConfig;
export function createBlankConfig(
    type: 'step',
    name: string
): StepProcessorConfig;
export function createBlankConfig(
    type: 'module',
    name: string,
    options: { moduleType: string }
): AbstractModuleConfig;
export function createBlankConfig(
    type: ConfigType,
    name: string,
    options?: { moduleType?: string }
): ConfigObject
{
    const newConfigId = uuidv4();

    switch (type)
    {
        case 'workflow':
            return {
                // 工作流没有内部 configId，它是顶级容器
                name: name,
                triggerParams: [],
                steps: [],
            };
        case 'step':
            return {
                configId: newConfigId,
                name: name,
                enabled: true,
                modules: [],
                inputMappings: {},
                outputMappings: {},
            };
        case 'module':
            if (!options?.moduleType)
            {
                throw new Error('创建空白模块时必须提供 moduleType！');
            }
            return {
                configId: newConfigId,
                name: name,
                enabled: true,
                moduleType: options.moduleType,
                // 其他模块特定的默认值可以由 schema-viewer 表单来处理
            };
        default:
            throw new Error(`未知的配置类型: ${type}`);
    }
}