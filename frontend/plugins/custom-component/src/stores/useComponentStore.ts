import {defineStore} from 'pinia';
import {type Component, shallowReactive, watch} from 'vue';

// 从 shared-ui 导入注册函数和契约类型
import {
    type ComponentContract,
    type ComponentRegistration,
    registerComponents,
    unregisterComponents
} from '@yaesandbox-frontend/shared-ui/content-renderer';

// JSX 编译器工具函数
import {compileJsx} from "#/utils/compiler";
import {createAsyncComponentFromJs} from '../utils/component-factory';
import {useComponentSaveStore} from "#/saves/useComponentSaveStore.ts";
import {exampleCode, exampleContent, exampleName} from '#/views/example.ts';


// 定义组件状态的数据结构
export interface DynamicComponentState
{
    id: string;
    source: string;
    testContent: string;
    compiledCode: string | null;
    component: Component | null;
    error: string | null;
    status: 'pending' | 'compiling' | 'ready' | 'error';
}

// 定义持久化数据的结构 (只存ID和源码)
type PersistedComponent = { id: string; source: string; testContent: string; };
type PersistedState = Record<string, PersistedComponent>;

const STORAGE_KEY = 'custom-components';

export const useComponentStore = defineStore(STORAGE_KEY, () =>
{
    const components = shallowReactive(new Map<string, DynamicComponentState>());

    // --- 持久化集成 ---
    const saveStore = useComponentSaveStore();
    const {state: persistedComponents, isReady} = saveStore.createState<PersistedState>(STORAGE_KEY, {});

    /**
     * 根据源数据（通常来自持久化层）重新构建内存中的所有组件
     */
    async function rebuildAllFromSource(sourceData: PersistedState)
    {
        console.log("正在从持久化源数据重建所有组件...");

        const newInternalStateMap = new Map<string, DynamicComponentState>();
        const componentsToRegister: Record<string, ComponentRegistration> = {};

        // 1. 遍历持久化数据，使用 addOrUpdateComponent 在临时 map 中构建所有组件
        for (const persisted of Object.values(sourceData))
        {
            const lowerCaseId = persisted.id.toLowerCase();
            const asyncComponent = await addOrUpdateComponent(
                persisted.id,
                persisted.source,
                persisted.testContent,
                {
                    skipPersistence: true,     // 不用再次保存
                    skipRegistration: true,    // 我们将进行批量注册
                    targetMap: newInternalStateMap // 在新的 map 上操作
                }
            );

            // 如果组件成功创建，则加入到批量注册列表
            if (asyncComponent)
            {
                componentsToRegister[lowerCaseId] = {
                    contract: {parseMode: 'raw', whitespace: 'trim'},
                    component: asyncComponent,
                };
            }
        }

        // 2. 执行原子性的更新操作
        unregisterComponents(Array.from(components.keys()));

        if (Object.keys(componentsToRegister).length > 0)
        {
            registerComponents(componentsToRegister);
            console.log("批量注册组件:", Object.keys(componentsToRegister));
        }

        components.clear();
        newInternalStateMap.forEach((value, key) => components.set(key, value));

        console.log("组件重建完成。");
    }

    // 监听持久化状态是否就绪和变化
    watch(isReady, (ready) =>
    {
        if (ready)
        {
            rebuildAllFromSource(persistedComponents.value);
        }
    }, {immediate: true});

    async function addOrUpdateComponent(
        id: string,
        jsxSource: string,
        testContent: string,
        options: { skipPersistence?: boolean, skipRegistration?: boolean, targetMap?: Map<string, DynamicComponentState> } = {}
    )
    {
        const lowerCaseId = id.toLowerCase();
        const target = options.targetMap || components;

        // 1. 初始化或更新状态
        let state = target.get(lowerCaseId);
        if (!state)
        {
            state = {
                id: lowerCaseId,
                source: '',
                testContent: '',
                compiledCode: null,
                component: null,
                error: null,
                status: 'pending'
            };
            target.set(lowerCaseId, state);
        }
        state.status = 'compiling';
        state.source = jsxSource;
        state.testContent = testContent;
        state.error = null;

        try
        {
            // 2. 编译
            const compiledJs = compileJsx(jsxSource);
            state.compiledCode = compiledJs;

            // 3. 创建异步组件 (现在只需一行调用！)
            const asyncComponent = createAsyncComponentFromJs(compiledJs, (error) =>
            {
                // 错误回调：当组件执行出错时，更新 store 中的状态
                state!.status = 'error';
                state!.error = error.message;
            });
            state.component = asyncComponent;

            // 4. 注册到全局渲染系统
            if (!options.skipRegistration)
            {
                registerComponents({
                    [lowerCaseId]: {
                        contract: {parseMode: 'raw', whitespace: 'trim'} as ComponentContract,
                        component: asyncComponent
                    }
                });
            }

            // 5. 更新最终状态
            state.status = 'ready';

            // --- 更新持久化状态 ---
            if (!options.skipPersistence)
            {
                persistedComponents.value = {
                    ...persistedComponents.value,
                    [lowerCaseId]: {id: lowerCaseId, source: jsxSource, testContent: testContent}
                };
            }

            return asyncComponent;

        } catch (compileError: any)
        {
            // 这个 catch 只捕获编译阶段的错误
            state.status = 'error';
            state.error = compileError.message;
        }
    }

    function deleteComponent(id: string)
    {
        const lowerCaseId = id.toLowerCase();
        if (components.has(lowerCaseId))
        {
            components.delete(lowerCaseId);

            // 从持久化状态中移除
            const newState = {...persistedComponents.value};
            delete newState[lowerCaseId];
            persistedComponents.value = newState;

            unregisterComponents([lowerCaseId])

            console.log(`组件 ${lowerCaseId} 已被彻底删除和注销。`);
        }
    }

    return {
        components,
        isReady,
        addOrUpdateComponent,
        deleteComponent,
    };
});