// --- 自定义 Vite 插件，用于重写导入路径 ---
import {defineConfig, mergeConfig} from 'vite';
import {visualizer} from "rollup-plugin-visualizer";
import {createMonorepoViteConfig} from "../../vite.config.shared";
import * as path from "node:path";

export default defineConfig(({command, mode}) => {
    const isBuild = mode === 'production';

    // 1. 使用构建器创建基础应用配置
    const baseAppConfig = createMonorepoViteConfig({
        packageDir: __dirname,
        type: 'app',
        plugins: [
            // 将 visualizer 添加到插件列表的末尾
            // 通常建议放到末尾，以确保它能分析到所有其他插件处理后的最终结果
            visualizer({
                // 常用配置项：
                open: false,             // 在默认浏览器中自动打开报告
                gzipSize: true,         // 显示 Gzip 压缩后的大小 (非常有用)
                brotliSize: true,       // 显示 Brotli 压缩后的大小 (如果你的服务器支持 Brotli，这个更准确)
                filename: 'stats.html', // 生成的报告文件名 (默认是 stats.html，会放在项目根目录)
                // template: 'treemap', // 可选 'treemap', 'sunburst', 'network' (默认为 treemap)
                // projectRoot: process.cwd(), // 通常不需要修改
                // sourcemap: false, // 是否分析 sourcemap (如果开启，分析时间会更长，但能更精确到原始代码行)
            }) as any,
        ],
        build: {
            cssCodeSplit: false,
            rollupOptions: {
                output: {
                    /**
                     * 手动分包，把大块的依赖拆分出来
                     * @param {string} id - 符文的路径
                     * @returns {string | undefined} - 返回自定义的 chunk 名称
                     */
                    manualChunks(id: string): "vendor-naive-ui" | "vendor-vue" | "vendor-sortable" | "vendor-signalr" | "vendor" | 'vendor-vscode' {
                        // 将 node_modules 中的依赖单独打包
                        if (id.includes('node_modules')) {
                            // 重点优化 naive-ui，它通常是体积大户
                            if (id.includes('naive-ui')) {
                                return 'vendor-naive-ui';
                            }
                            // 重点优化 vue 全家桶
                            if (id.includes('vue') || id.includes('@vue')) {
                                return 'vendor-vue';
                            }
                            // 重点优化 sortablejs (vue-draggable-plus 的依赖)
                            if (id.includes('sortablejs')) {
                                return 'vendor-sortable';
                            }
                            // 重点优化 SignalR
                            if (id.includes('@microsoft/signalr')) {
                                return 'vendor-signalr';
                            }
                            if (id.includes('@codingame') || id.includes('vscode')) {
                                return 'vendor-vscode';
                            }
                            // 其他所有 node_modules 的依赖都打包到 vendor 文件
                            return 'vendor';
                        }
                    },
                }
            }
        }
    });

    // 2. 定义只有 Shell 才有的顶级配置 (如 server)
    const shellSpecificConfig = defineConfig({
        server: {
            port: 5173, // 你前端的运行端口
            proxy: {
                // 将所有以 /api 开头的请求代理到后端服务器
                '/api': {
                    target: 'http://localhost:7018', // <--- 你的后端 API 服务器地址和端口
                    changeOrigin: true, // 必须，对于虚拟主机站点是必需的
                    // secure: false, // 如果后端是 https 且证书无效，可能需要
                    // rewrite: (path) => path.replace(/^\/api/, '/api') // 通常不需要，除非后端路径也需要调整
                },
                '/plugins': {
                    target: 'http://localhost:7018', // <--- 替换成你的后端实际运行地址和端口
                    changeOrigin: true, // 改变源，使其看起来像是从目标服务器发出的请求
                    // 如果你的后端是 HTTPS，并且使用了自签名证书，你可能还需要添加 secure: false
                    // target: 'https://localhost:5001',
                    // secure: false,
                    // rewrite: (path) => path.replace(/^\/plugins/, '/plugins'), // 如果路径需要重写，但这里通常不需要
                },
                // 代理所有 /hubs 开头的请求 (用于 SignalR)
                '/hubs': {
                    target: 'http://localhost:7018', // <--- 替换成你的后端地址!
                    changeOrigin: true, // 必须设置为 true
                    ws: true, // <--- SignalR 必须启用 WebSocket 代理
                    // secure: false, // 如果你的后端是 https 且证书是自签名的，需要这个
                }
            }
        },
        resolve: {}
    });
    if (isBuild) {
        shellSpecificConfig.resolve = {
            alias: {
                '@yaesandbox-frontend/plugin-workbench': path.resolve(__dirname, '../../plugins/workbench/dist/index.js'),
                '@yaesandbox-frontend/plugin-dialog-test': path.resolve(__dirname, '../../plugins/dialog-test/dist/index.js'),
                '@yaesandbox-frontend/plugin-era-lite': path.resolve(__dirname, '../../plugins/era-lite/dist/index.js')
            },
        };
    }
    return mergeConfig(baseAppConfig, shellSpecificConfig);
});
