import {useWorkbenchStore} from '#/stores/workbenchStore';
import type {AbstractRuneConfig, TuumConfig, WorkflowConfig} from '#/types/generated/workflow-config-api-client';
import type {EditorContext} from './EditorContext';
import {type AnyConfigObject, getConfigObjectType} from "#/services/GlobalEditSession.ts";

// --- 具体选择上下文的定义 ---

export interface WorkflowSelectionContext
{
    readonly type: 'workflow';
    readonly data: WorkflowConfig;
    readonly path: string;
    readonly context: EditorContext;
}

export interface TuumSelectionContext
{
    readonly type: 'tuum';
    readonly data: TuumConfig;
    readonly path: string;
    readonly context: EditorContext;
}

export interface RuneSelectionContext
{
    readonly type: 'rune';
    readonly data: AbstractRuneConfig;
    readonly path: string;
    readonly context: EditorContext;
    readonly schema: Record<string, any> | null;
}

export type AnySelectionContext = WorkflowSelectionContext | TuumSelectionContext | RuneSelectionContext;

// --- 工厂函数 ---

export function createSelectionContext(
    config: AnyConfigObject,
    path: string,
    context: EditorContext
): AnySelectionContext
{
    const {type,config:typedConfig} = getConfigObjectType(config)

    switch (type)
    {
        case 'workflow':
            return {type: 'workflow', data: typedConfig, path, context};

        case 'tuum':
            return {type: 'tuum', data: typedConfig, path, context};

        case 'rune':
            const workbenchStore = useWorkbenchStore();
            const schema = workbenchStore.runeSchemasAsync.state?.[typedConfig.runeType] || null;
            return {type: 'rune', data: typedConfig, path, context, schema};
    }
}