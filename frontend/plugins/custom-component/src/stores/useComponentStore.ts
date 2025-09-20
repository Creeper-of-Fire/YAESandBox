import {defineStore} from 'pinia';
import {type Component, defineAsyncComponent, defineComponent, h, shallowReactive} from 'vue';

// 从 shared-ui 导入注册函数和契约类型
import {type ComponentContract, registerComponents} from '@yaesandbox-frontend/shared-ui/content-renderer';

// JSX 编译器工具函数
import {compileJsx} from "#/utils/compiler";
import {injectionKeys, injectionValues} from "#/injection.ts";
import {createAsyncComponentFromJs} from '../utils/component-factory';


// 定义组件状态的数据结构
interface DynamicComponentState
{
    id: string;
    source: string;
    compiledCode: string | null;
    component: Component | null;
    error: string | null;
    status: 'pending' | 'compiling' | 'ready' | 'error';
}

const STORAGE_KEY = 'custom-components';

export const useComponentStore = defineStore(STORAGE_KEY, () =>
{
    const components = shallowReactive(new Map<string, DynamicComponentState>());

    async function addOrUpdateComponent(id: string, jsxSource: string)
    {
        const lowerCaseId = id.toLowerCase();

        // 1. 初始化或更新状态
        let state = components.get(lowerCaseId);
        if (!state)
        {
            state = {id: lowerCaseId, source: '', compiledCode: null, component: null, error: null, status: 'pending'};
            components.set(lowerCaseId, state);
        }
        state.status = 'compiling';
        state.source = jsxSource;
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
            registerComponents({
                [lowerCaseId]: {
                    contract: {parseMode: 'raw', whitespace: 'trim'} as ComponentContract,
                    component: asyncComponent
                }
            });

            // 5. 更新最终状态
            state.status = 'ready';

        } catch (compileError: any)
        {
            // 这个 catch 只捕获编译阶段的错误
            state.status = 'error';
            state.error = compileError.message;
        }
    }

    return {
        components,
        addOrUpdateComponent
    };
});