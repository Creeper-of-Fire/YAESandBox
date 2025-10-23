import type {App} from 'vue';
import type {PluginModule} from '@yaesandbox-frontend/core-services';
import {routes,routeName} from './routes';
import {useWorkflowConfigProviderStore} from "#/stores/workflowConfigProviderStore.ts";
import {type IWorkflowConfigProvider, WorkflowConfigProviderKey} from "@yaesandbox-frontend/core-services/inject-key";
import {type Pinia, storeToRefs} from "pinia";

const WorkbenchPluginModule: PluginModule = {
    // Vue 插件对象
    plugin: {
        install: (app: App, pinia: Pinia) =>
        {
            console.log('Workbench plugin installed.');
            // 实例化我们的数据提供者 store
            // 1. 在 setup 上下文之外使用 store，需要先传入 pinia 实例
            const workflowConfigStore = useWorkflowConfigProviderStore(pinia);

            // 2. 使用 storeToRefs 从 store 中提取 getters，它们会变成响应式的 ref。
            //    Getters (computed) 会被转换为 Ref<T>，这与 ComputedRef<T> 是兼容的。
            const { state, isLoading, isReady, error } = storeToRefs(workflowConfigStore);

            // 3. 从 store 实例中直接获取 actions (方法)
            const { execute } = workflowConfigStore;

            // 4. 创建一个完全符合 IWorkflowConfigProvider 接口的普通对象
            const workflowProvider: IWorkflowConfigProvider = {
                state,
                isLoading,
                isReady,
                error,
                execute,
            };

            // 使用定义的 InjectionKey 将其提供给所有后代组件
            app.provide(WorkflowConfigProviderKey, workflowProvider);
        }
    },
    // 插件的路由
    routes,
    // 插件的元数据
    meta: {
        name: routeName,
        uniqueName: 'workbench-D1D3E394-8025-45A5-AD6E-9C1B8BCF3B38',
        navEntry: {
            label: '编辑器',
            order: 1
        }
    }
};

export default WorkbenchPluginModule;