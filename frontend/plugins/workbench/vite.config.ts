import {defineConfig} from 'vite';
import {createMonorepoViteConfig} from '../../vite.config.shared';
import {visualizer} from "rollup-plugin-visualizer";
export default defineConfig(
    createMonorepoViteConfig({
        packageDir: __dirname,
        type: 'plugin',
        plugins: [
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
    })
);