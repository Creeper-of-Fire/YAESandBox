import {type BuildOptions, mergeConfig, type PluginOption, type ResolvedConfig, type ResolveOptions, type UserConfig} from 'vite';
import vue from '@vitejs/plugin-vue';
import path from 'path';
import AutoImport from 'unplugin-auto-import/vite';
import {NaiveUiResolver} from 'unplugin-vue-components/resolvers';
import Components from 'unplugin-vue-components/vite';
import VitePluginVueDevTools from 'vite-plugin-vue-devtools';

export interface MonorepoViteConfigOptions
{
    /** 包的根目录 (通常是 __dirname) */
    packageDir: string;
    /**
     * 构建类型:
     * 'app' - 用于最终的可运行应用 (如 shell)
     * 'plugin' - 用于需要被引用的库 (如 workbench 插件)
     */
    type: 'app' | 'plugin';
    /**
     * 附加的 Vite 插件，它们将被追加到共享插件列表的末尾。
     */
    plugins?: PluginOption[];
    /**
     * 自定义的构建选项，将与默认的构建选项进行深度合并。
     */
    build?: BuildOptions;
}

// 导出一个函数，这样每个子包可以传入自己的特定配置
export function createMonorepoViteConfig(options: MonorepoViteConfigOptions): UserConfig
{
    const {packageDir, type, plugins = [], build = {}} = options;

    // 1. 定义所有包共享的基础配置
    const baseConfig: UserConfig = {
        plugins: [
            vue(),
            VitePluginVueDevTools(),
            Components({
                resolvers: [NaiveUiResolver()],
            }),
            AutoImport({
                imports: [
                    'vue',
                    {
                        'naive-ui': [
                            'useDialog',
                            'useMessage',
                            'useNotification',
                            'useLoadingBar',
                        ],
                    },
                ],
            }),
            ...plugins,
        ],
        resolve: {
            alias: {
                // 让每个包的 '@' 都指向自己的 'src' 目录
                '@': path.resolve(packageDir, './src'),
            },
        },
    };

    // 2. 根据类型定义特定配置
    let typeSpecificConfig: UserConfig = {};

    if (type === 'plugin')
    {
        typeSpecificConfig = {
            build: {
                lib: {
                    entry: path.resolve(packageDir, 'src/index.ts'),
                    name: path.basename(packageDir), // 自动使用包名
                    fileName: (format) => `${path.basename(packageDir)}.${format}.js`,
                },
                rollupOptions: {
                    // 插件需要将核心依赖外部化
                    external: ['vue', 'vue-router', 'pinia', 'naive-ui'],
                    output: {
                        globals: {
                            vue: 'Vue',
                            'vue-router': 'VueRouter',
                            pinia: 'Pinia',
                            'naive-ui': 'naive',
                        },
                    },
                },
            },
        };
    }

    if (type === 'app')
    {
        typeSpecificConfig = {
            build: {
                // 应用不需要 external，它需要打包所有东西
                rollupOptions: {
                    external: [], // <-- 明确覆盖为[]
                },
            },
        };
    }

    // 3. 深度合并所有配置层级
    // 顺序: 基础配置 -> 类型特定配置 -> 用户传入的自定义配置
    return mergeConfig(mergeConfig(baseConfig, typeSpecificConfig), {build});
}