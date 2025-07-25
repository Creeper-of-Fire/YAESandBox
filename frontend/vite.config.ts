import {defineConfig} from 'vite'
import vue from '@vitejs/plugin-vue'
import license from 'rollup-plugin-license';
import path from 'path'
import AutoImport from 'unplugin-auto-import/vite'
import {NaiveUiResolver} from 'unplugin-vue-components/resolvers'
import Components from 'unplugin-vue-components/vite'
import {visualizer} from "rollup-plugin-visualizer";

// https://vite.dev/config/
export default defineConfig({
    plugins: [
        vue(),
        // --- 添加 license 插件配置 ---
        license({
            sourcemap: true,
            banner: {
                commentStyle: 'ignored',
                content: {
                    // 可以指向你的项目的 LICENSE 文件，或者直接写字符串
                    file: path.join(__dirname, 'LICENSE'), // 假设你的项目根目录有 LICENSE 文件
                    // 或者直接用字符串:
                    // content: `/*! <%= pkg.name %> v<%= pkg.version %> | (c) ${new Date().getFullYear()} Your Name | MIT License */`
                },
                // 可选：传递额外数据给模板
                // data: { projectStartDate: 2024 }
            },

            // --- 配置生成第三方许可证文件 ---
            thirdParty: {
                // includePrivate: false, // 默认 false，不包含私有包 (通常不需要开启)
                // includeSelf: false,   // 默认 false，不包含你自己的包信息 (通常不需要开启)
                // multipleVersions: false, // 默认 false，如果一个库有多个版本，只显示一次。如果需要区分版本，设为 true
                includeSelf: true, // 包含本项目在内
                allow: '(MIT OR Apache-2.0 OR BSD-2-Clause OR BSD-3-Clause)', // 可选但推荐：只允许特定的宽松许可证，如果有不符合的会发出警告。你可以根据需要调整这个 SPDX 表达式。
                // 配置输出文件
                output: {
                    file: path.join(__dirname, './', 'THIRD_PARTY_LICENSES.txt'), // 输出到构建目录 (dist) 下的文件名
                    encoding: 'utf-8', // 文件编码

                    // --- 自定义输出格式 (可选) ---
                    // 你可以选择一个模板函数来自定义输出格式，默认格式通常也够用
                    // 默认会包含包名、版本、作者、仓库、许可证类型和许可证文本
                    /*
                    template(dependencies) {
                      // 这是一个简单的自定义模板示例，只输出 包名@版本 - 许可证类型
                      return dependencies.map(
                        dep => `${dep.name}@${dep.version} - ${dep.license}`
                      ).join('\n\n');
                    }
                    */
                    // 或者使用 Lodash 模板字符串
                    /*
                    template: `
                      -------------------------
                      Package: <%= name %>@<%= version %>
                      License: <%= license %>
                      Author: <%= author?.name || 'N/A' %>
                      Repository: <%= repository?.url || 'N/A' %>
                      -------------------------
                      <%= licenseText %>
          
                      =========================
                    `
                    */
                },

                // --- 配置许可证检查失败时的行为 (可选) ---
                // 如果你需要更严格的检查
                /*
                allow: {
                  test: '(MIT OR Apache-2.0 OR BSD*)', // SPDX 表达式
                  failOnUnlicensed: true,  // 如果依赖没有许可证信息，构建失败 (默认为 false)
                  failOnViolation: true,   // 如果依赖的许可证不符合 test 要求，构建失败 (默认为 false)
                }
                */
            }
        }),
        // --- license 插件配置结束 ---
        // 按需引入Naive UI组件
        Components({
            resolvers: [NaiveUiResolver()] // 配置 Naive UI 解析器
        }),
        // 按需自动导入 API (可选)
        AutoImport({
            imports: [
                'vue',
                {
                    'naive-ui': [
                        'useDialog',
                        'useMessage',
                        'useNotification',
                        'useLoadingBar'
                    ]
                }
            ]
        }),

        // 2. 将 visualizer 添加到插件列表的末尾
        //    通常建议放到末尾，以确保它能分析到所有其他插件处理后的最终结果
        visualizer({
            // 常用配置项：
            open: true,             // 在默认浏览器中自动打开报告
            gzipSize: true,         // 显示 Gzip 压缩后的大小 (非常有用)
            brotliSize: true,       // 显示 Brotli 压缩后的大小 (如果你的服务器支持 Brotli，这个更准确)
            filename: 'stats.html', // 生成的报告文件名 (默认是 stats.html，会放在项目根目录)
            // template: 'treemap', // 可选 'treemap', 'sunburst', 'network' (默认为 treemap)
            // projectRoot: process.cwd(), // 通常不需要修改
            // sourcemap: false, // 是否分析 sourcemap (如果开启，分析时间会更长，但能更精确到原始代码行)
        }),
    ],
    build: {
        rollupOptions: {
            output: {
                /**
                 * 手动分包，把大块的依赖拆分出来
                 * @param {string} id - 模块的路径
                 * @returns {string | undefined} - 返回自定义的 chunk 名称
                 */
                manualChunks(id: string) {
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
                        // 其他所有 node_modules 的依赖都打包到 vendor 文件
                        return 'vendor';
                    }
                },
            },
        },
    },
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
        }
    },
    resolve: {
        alias: {
            '@': path.resolve(__dirname, './src'), // <--- 检查这行或类似配置是否存在且正确
        }
    }
})
