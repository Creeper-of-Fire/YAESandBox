import {defineConfig} from "vite";
// @ts-ignore
import vue from "@vitejs/plugin-vue";
import {resolve} from 'path';
import VitePluginVueDevTools from "vite-plugin-vue-devtools";

const host = process.env.TAURI_DEV_HOST;

// https://vite.dev/config/
export default defineConfig(async () => ({
    plugins: [
        vue(),
        // VitePluginVueDevTools(),
    ],
    build: {
        // 配置多页面入口
        rollupOptions: {
            input: {
                main: resolve(__dirname, 'index.html'),
                log_viewer: resolve(__dirname, 'log_viewer.html'),
            },
        },
        // 可选：确保输出目录是 Tauri 期望的
        outDir: 'dist',
    },

    // Vite options tailored for Tauri development and only applied in `tauri dev` or `tauri build`
    //
    // 1. prevent Vite from obscuring rust errors
    clearScreen: false,
    // 2. tauri expects a fixed port, fail if that port is not available
    server: {
        port: 1420,
        strictPort: true,
        host: host || false,
        hmr: host
            ? {
                protocol: "ws",
                host,
                port: 1421,
            }
            : undefined,
        watch: {
            // 3. tell Vite to ignore watching `src-tauri`
            ignored: ["**/src-tauri/**"],
        },
    },
}));
