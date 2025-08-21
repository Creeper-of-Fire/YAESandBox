import type {App} from 'vue';
import type {RouteRecordRaw} from 'vue-router';

/**
 * 定义一个标准前端插件模块应该导出的内容
 */
export interface PluginModule
{
    /**
     * Vue 插件对象，必须包含一个 install 方法
     */
    plugin: {
        install: (app: App) => void;
    };
    /**
     * 插件提供的路由数组
     */
    routes: RouteRecordRaw[];
    /**
     * 插件的元数据，例如导航栏显示信息
     */
    meta: {
        name: string; // 唯一标识符，如 'workbench'
        /**
         * 一个全局唯一的、持久化的标识符。
         * 用于需要持久化存储的场景 (如 useScopedStorage)，以确保 key 的稳定性。
         * 推荐使用 UUID 或其他不会冲突的硬编码字符串。
         */
        uniqueName: string;
        navEntry?: {
            label: string;
            order?: number; // 用于排序
            // icon?: Component;
        };
    };
}