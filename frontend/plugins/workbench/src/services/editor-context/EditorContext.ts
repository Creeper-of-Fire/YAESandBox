import type {AnyConfigObject, GlobalEditSession} from "#/services/GlobalEditSession.ts";
import {computed, type DeepReadonly, readonly, ref, type Ref, watch} from 'vue';
import {get} from 'lodash-es';
import {type AnySelectionContext, createSelectionContext} from "#/services/editor-context/SelectionContext.ts";
import {findPathByReference} from '#/utils/pathFinder';

export class EditorContext
{
    public readonly session: GlobalEditSession;

    // --- 选中状态管理 ---
    private readonly _selectedId = ref<string | null>(null);
    public readonly selectedId: Readonly<Ref<string | null>> = readonly(this._selectedId);
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
        // 默认选中根对象。根对象可能没有 configId，我们用一个特殊值，例如 '__root__'
        this.select(session.getData().value);

        // // 监听数据变化，以应对删除操作
        // watch(() => this.session.getData().value, (newData) =>
        // {
        //     // 如果之前选中的对象在新数据中已经不存在了，则取消选中
        //     // (findPathByReference 找不到时会返回 undefined)
        //     if (this._selectedConfig.value && !findPathByReference(newData, this._selectedConfig.value))
        //     {
        //         this.select(null);
        //     }
        // }, {deep: true});
    }

    // --- 数据访问代理 ---
    public get data(): DeepReadonly<Ref<AnyConfigObject>>
    {
        return readonly(this.session.getData());
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
     * 核心方法：只接受被选中的对象。
     * @param configObject - 被选中的配置对象，或 null 表示取消选中。
     */
    public select(configObject: AnyConfigObject | null): void
    {
        if (!configObject)
        {
            this._selectedId.value = null;
            this._selectedPath.value = '';
            this._selectedConfig.value = null;
            return;
        }


        // *** 核心逻辑：在这里计算路径 ***
        const path = findPathByReference(this.session.getData().value, configObject);

        if (path !== undefined)
        {
            this._selectedConfig.value = configObject;
            this._selectedPath.value = path;
            this._selectedId.value = this.getStableId(configObject);
        }
        else
        {
            // 这种情况理论上不应发生，除非传入的对象不属于当前 session 的数据树
            console.error("EditorContext.select: 传入的对象不在当前编辑会话的数据中。");
            this.select(null);
        }
    }

    public getItemByPath(path: string): DeepReadonly<Ref<AnyConfigObject>> | undefined
    {
        if (!path)
            return this.data;
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

    // 辅助函数，获取对象的稳定ID
    private getStableId(config: AnyConfigObject): string
    {
        return 'configId' in config ? config.configId : '__root__';
    }
}