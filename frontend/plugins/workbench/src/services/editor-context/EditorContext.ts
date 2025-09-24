import type {AnyConfigObject, GlobalEditSession} from "#/services/GlobalEditSession.ts";
import {computed, type DeepReadonly, readonly, ref, type Ref} from 'vue';
import {get} from 'lodash-es';
import {type AnySelectionContext, createSelectionContext} from "#/services/editor-context/SelectionContext.ts";

export class EditorContext
{
    public readonly session: GlobalEditSession;

    // --- 选中状态管理 ---
    private readonly _selectedConfig = ref<AnyConfigObject | null>(null);
    public readonly selectedConfig: DeepReadonly<Ref<AnyConfigObject | null>> = readonly(this._selectedConfig);
    private readonly _selectedPath = ref<string>(''); // e.g., "tuums[0].runes[1]"
    public readonly selectedPath: Readonly<Ref<string>> = readonly(this._selectedPath);

    /**
     * 动态计算出的、类型安全的当前选择上下文。
     * 这是UI组件将直接消费的对象。
     */
    public readonly selectedContext = computed<AnySelectionContext | null>(() =>
    {
        if (!this._selectedConfig.value)
        {
            return null;
        }
        return createSelectionContext(this._selectedConfig.value, this._selectedPath.value, this);
    });

    constructor(session: GlobalEditSession)
    {
        this.session = session;
        // 默认选中根对象
        this.select(session.getData().value, '');
    }

    // --- 数据访问代理 ---
    public get data(): Readonly<AnyConfigObject>
    {
        return this.session.getData().value;
    }

    public get globalId(): string
    {
        return this.session.globalId;
    }

    // --- 操作代理 ---
    public get isDirty(): Ref<boolean>
    {
        return this.session.getIsDirty();
    }

    /**
     * 核心方法：更新当前选中的项。
     * @param configObject - 被选中的配置对象。
     * @param path - 该对象在根配置中的路径。
     */
    public select(configObject: AnyConfigObject | null, path: string): void
    {
        this._selectedConfig.value = configObject;
        this._selectedPath.value = path;
    }

    public getItemByPath(path: string): AnyConfigObject | undefined
    {
        if (!path) return this.data;
        return get(this.data, path);
    }

    public save()
    {
        return this.session.save();
    }

    public discard()
    {
        return this.session.discard();
    }
}