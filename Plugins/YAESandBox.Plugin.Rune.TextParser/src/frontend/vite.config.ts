import {fileURLToPath, URL} from 'node:url'
import {defineConfig} from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'node:path'

// https://vite.dev/config/
export default defineConfig({
    plugins: [
        vue(),
    ],
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        }
    },
    build: {
        // 输出目录，我们希望它在插件的 wwwroot 下
        outDir: path.resolve(__dirname, '../wwwroot'),
        // outDir: 'dist',
        // 清空输出目录
        emptyOutDir: true,
        // 关键：库模式配置
        lib: {
            // 入口文件，它会导出我们所有的组件
            entry: path.resolve(__dirname, 'src/main.ts'),
            // 库的名字，主程序可以通过这个名字访问
            name: 'YAESandBox_Plugin_Rune_TextParser',
            // 输出格式为 iife (立即执行函数)，适合通过 <script> 标签加载
            formats: ['iife'],
            // 输出的文件名
            fileName: () => 'vue-bundle.js'
        },
        // 为了减小包体积，我们让 Vue 由主程序提供
        rollupOptions: {
            external: [
                'vue'
            ],
            output: {
                globals: {
                    vue: 'Vue'
                },
                assetFileNames: (assetInfo) =>
                {
                    // 不再关心原始文件名，只要是CSS文件就进行重命名
                    if (assetInfo.name && assetInfo.name.endsWith('.css'))
                    {
                        return 'vue-bundle.css';
                    }
                    // 对于其他可能的资源（如字体、图片），保持其原有名称
                    return assetInfo.name ?? 'unknown-asset';
                },
            }
        },
    }
})
