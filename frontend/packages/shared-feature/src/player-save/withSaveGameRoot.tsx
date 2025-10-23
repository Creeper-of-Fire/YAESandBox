import {type Component, computed, defineComponent, h, type VNode} from 'vue';
import StartupView from './StartupView.vue';
import type {IGameSaveService} from '@yaesandbox-frontend/core-services/player-save';

/**
 * HOC 的配置项接口
 */
export interface SaveRootOptions
{
    /**
     * 用于创建和提供游戏存档服务的工厂函数。
     * 例如: createAndProvideEraMapGameSaveService
     */
    createSaveService: () => IGameSaveService;
    /**
     * 在 StartupView 中显示的应用标题。
     * 例如: 'Era-Map'
     */
    appTitle: string;
}

/**
 * 一个高阶组件 (HOC)，用于包装应用的主视图，并自动处理存档加载逻辑。
 * 它会根据是否存在激活的存档，来决定是渲染主视图还是启动引导页。
 *
 * @param WrappedComponent - 当存档被激活时要渲染的主组件。
 * @param options - 包含 createSaveService 函数和 appTitle 的配置对象。
 * @returns 一个新的 Vue 组件定义。
 */
export function withSaveGameRoot(WrappedComponent: Component, options: SaveRootOptions)
{
    return defineComponent({
        name: 'WithSaveGameRoot',
        setup()
        {
            // 1. 调用传入的工厂函数，初始化对应项目的存档服务
            // 这是最关键的一步，它决定了整个应用使用的存档系统实例
            const saveService = options.createSaveService();

            // 2. 获取激活的存档槽
            const activeSlot = computed(() => saveService.activeSlot.value);

            // 3. 返回渲染函数 (TSX)
            //    这里我们将实现你指出的 v-if 和 v-show 的精确控制
            return (): VNode => (
                // 使用一个根元素来包裹两个视图
                <>

                    {/*
                    主应用视图 (WrappedComponent):
                    - 使用逻辑与 (&&) 实现 v-if 的效果。
                    - 只有当 activeSlot 存在时，它才会被渲染到 VDOM 中。
                    - 绑定 key 到 slot.id，确保切换存档时组件会重新创建，避免状态污染。
                    - 调用 h(component, props) 来创建 VNode。
                    - h 函数的类型定义能够正确处理从 .vue 文件导入的组件对象。
                    */}
                    {activeSlot.value && h(WrappedComponent, {key: activeSlot.value.id})}

                    {/*
                    启动引导页 (StartupView):
                    - 使用 style.display 来模拟 v-show 的效果。
                    - 无论 activeSlot 是否存在，它始终在 VDOM 中，只是被隐藏了。
                    - 这可以保留 StartupView 内部的状态（例如弹窗的显示状态）。
                    */}
                    <StartupView
                        appTitle={options.appTitle}
                        style={{display: activeSlot.value ? 'none' : ''}}
                    />
                </>
            );
        },
    });
}