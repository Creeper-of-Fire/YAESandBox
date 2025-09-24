import { useWorkbenchStore } from '#/stores/workbenchStore';
import type { WorkflowConfig, TuumConfig, AbstractRuneConfig } from '#/types/generated/workflow-config-api-client';
import type { EditorContext } from './EditorContext';
import type {AnyConfigObject} from "#/services/GlobalEditSession.ts";

// --- 具体选择上下文的定义 ---

export interface WorkflowSelectionContext {
    readonly type: 'workflow';
    readonly data: WorkflowConfig;
    readonly path: string;
    readonly context: EditorContext;
}

export interface TuumSelectionContext {
    readonly type: 'tuum';
    readonly data: TuumConfig;
    readonly path: string;
    readonly context: EditorContext;
}

export interface RuneSelectionContext {
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
): AnySelectionContext {
    // 使用简单的类型守卫来决定创建哪种上下文
    if ('workflowInputs' in config) {
        return { type: 'workflow', data: config, path, context };
    }

    if ('runes' in config) {
        return { type: 'tuum', data: config, path, context };
    }

    // Rune 是最具体的，所以放最后
    if ('runeType' in config) {
        const workbenchStore = useWorkbenchStore();
        const schema = workbenchStore.runeSchemasAsync.state?.[config.runeType] || null;
        return { type: 'rune', data: config, path, context, schema };
    }

    throw new Error("无法识别的配置对象类型");
}