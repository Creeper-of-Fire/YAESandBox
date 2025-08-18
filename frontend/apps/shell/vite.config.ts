// --- 自定义 Vite 插件，用于重写导入路径 ---
import {defineConfig, mergeConfig, Plugin} from 'vite';
import {visualizer} from "rollup-plugin-visualizer";
import {createMonorepoViteConfig} from "../../vite.config.shared";
import fs from 'fs';
import path from 'path';

// 使用一个缓存来避免重复的文件系统查找，提升性能
const packageJsonCache = new Map<string, { path: string, content: any } | null>();

// 辅助函数：向上查找 package.json (你的实现很好，无需改动)
function findPackageJson(startDir: string): { path: string, content: any } | null
{
    let dir = startDir;
    // 确保我们从一个绝对路径开始
    if (!path.isAbsolute(dir))
    {
        console.warn(`[plugin-resolver] findPackageJson received a relative path: ${dir}. This should not happen.`);
        return null;
    }
    // 防止无限循环到根目录之外
    if (!path.isAbsolute(dir))
    {
        dir = path.resolve(dir);
    }
    while (dir !== path.dirname(dir)) {
        const cacheKey = dir;
        if (packageJsonCache.has(cacheKey)) {
            return packageJsonCache.get(cacheKey)!;
        }
        const filePath = path.join(dir, 'package.json');
        if (fs.existsSync(filePath)) {
            try {
                const content = JSON.parse(fs.readFileSync(filePath, 'utf8'));
                const result = { path: filePath, content: content };
                packageJsonCache.set(cacheKey, result);
                return result;
            } catch (e) {
                packageJsonCache.set(cacheKey, null);
                return null;
            }
        }
        dir = path.dirname(dir);
    }
    packageJsonCache.set(startDir, null);
    return null;
}

/**
 * 这是一个更健壮、更正确的 Vite 插件，用于在 Monorepo 中解析插件内部的 '@/' 别名。
 * 它使用 `resolveId` 钩子，在 Vite 解析模块的第一时间介入。
 */
function resolvePluginImportsPlugin(): Plugin
{
    let projectRoot: string;
    let rootPackageName: string | undefined;

    return {
        name: 'vite-plugin-resolve-plugin-imports',
        enforce: 'pre',

        configResolved(config)
        {
            projectRoot = config.root; // 保存项目根目录
            const rootPackageInfo = findPackageJson(projectRoot);
            if (rootPackageInfo)
            {
                rootPackageName = rootPackageInfo.content.name;
            }
        },

        // 我们放弃 resolveId，改用 transform
        transform(code, id)
        {
            // id 是正在被转换的文件的绝对路径
            // 只处理 JS/TS/Vue 文件，排除虚拟模块和非代码文件
            if (!/\.(js|ts|vue|jsx|tsx)$/.test(id) || id.includes('node_modules/.vite'))
            {
                return null;
            }

            const importerPackageInfo = findPackageJson(path.dirname(id));

            if (!importerPackageInfo || !importerPackageInfo.content.name)
            {
                return null;
            }

            const importerPackageName = importerPackageInfo.content.name;

            // 如果是主应用自己的文件，或者文件内容里没有'@/'，我们不做任何事
            if (importerPackageName === rootPackageName || !code.includes('@/'))
            {
                return null;
            }

            // 【核心逻辑】
            // 这是插件的文件，我们需要把里面的 '@/' 替换掉
            const packageRootDir = path.dirname(importerPackageInfo.path);
            const packageSrcDir = path.resolve(packageRootDir, 'src');

            const currentFileDir = path.dirname(id);

            // 计算从当前文件到其所在包的 src 目录的相对路径
            const relativePathToSrc = path.relative(currentFileDir, packageSrcDir);

            // 将路径统一为 POSIX 风格 (斜杠 /)
            const replacement = path.normalize(relativePathToSrc).replace(/\\/g, '/');

            // console.log(`[transform] Transforming ${id.replace(projectRoot, '')}`);
            // console.log(`  - Found in package: ${importerPackageName}`);
            // console.log(`  - Replacing '@/' with '${replacement}'`);

            // 使用一个安全的正则表达式来替换，只替换导入路径中的 '@/'
            // from '@/...' or import('@/...')
            const newCode = code.replace(/(from\s+['"]|import\s*\(\s*['"])@\//g, `$1${replacement}/`);

            return {
                code: newCode,
                // 如果你有 sourcemaps，需要设置 map: null 来确保 sourcemap 链正确
                map: null
            };
        },

        async resolveId(source, importer, options)
        {
            // 1. 只处理 '@/' 开头的导入
            if (!source.startsWith('@/'))
            {
                return null;
            }

            // 2. importer 必须存在，才能确定上下文
            if (!importer)
            {
                return null;
            }

            const absoluteImporter = path.isAbsolute(importer)
                ? importer
                : path.resolve(projectRoot, importer);

            // 【修复二】移除对 'node_modules' 的判断，因为它在 monorepo 中是合法的

            // 3. 找到 importer 文件所属的包
            const importerPackageInfo = findPackageJson(path.dirname(absoluteImporter));

            if (!importerPackageInfo)
            {
                return null;
            }

            const importerPackageName = importerPackageInfo.content.name;

            // 4. 如果是主应用自己引用，则不处理，交由 Vite 的 alias 配置处理
            if (importerPackageName === rootPackageName)
            {
                return null;
            }

            // 5. 【修复一：核心逻辑】
            // 如果导入方是一个插件，我们构建一个新的目标路径，然后让 Vite 自己去解析
            const packageRootDir = path.dirname(importerPackageInfo.path);
            const relativePath = source.substring(2); // 移除 '@/'
            const newSource = path.resolve(packageRootDir, 'src', relativePath);

            // console.log(`[plugin-resolver] Remapping '${source}' to '${newSource}' for importer in '${importerPackageName}'`);

            // 调用 this.resolve，让 Vite 继续解析这个已经没有别名的、更明确的路径
            // skipSelf: true 确保不会再次触发我们自己的这个 resolveId 钩子，防止死循环
            const resolution = await this.resolve(newSource, importer, {...options, skipSelf: true});

            // 如果 Vite 能解析到（找到了 .vue, .ts 等），就返回它的结果
            // 否则返回 null，让其他插件或默认行为继续
            return resolution;
        }
    };
}

export default defineConfig(({command, mode}) =>
{
    // 1. 使用构建器创建基础应用配置
    const baseAppConfig = createMonorepoViteConfig({
        packageDir: __dirname,
        type: 'app',
        plugins: [
            resolvePluginImportsPlugin(),

            // 将 visualizer 添加到插件列表的末尾
            // 通常建议放到末尾，以确保它能分析到所有其他插件处理后的最终结果
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
                     * @param {string} id - 符文的路径
                     * @returns {string | undefined} - 返回自定义的 chunk 名称
                     */
                    manualChunks(id: string): "vendor-naive-ui" | "vendor-vue" | "vendor-sortable" | "vendor-signalr" | "vendor"
                    {
                        // 将 node_modules 中的依赖单独打包
                        if (id.includes('node_modules'))
                        {
                            // 重点优化 naive-ui，它通常是体积大户
                            if (id.includes('naive-ui'))
                            {
                                return 'vendor-naive-ui';
                            }
                            // 重点优化 vue 全家桶
                            if (id.includes('vue') || id.includes('@vue'))
                            {
                                return 'vendor-vue';
                            }
                            // 重点优化 sortablejs (vue-draggable-plus 的依赖)
                            if (id.includes('sortablejs'))
                            {
                                return 'vendor-sortable';
                            }
                            // 重点优化 SignalR
                            if (id.includes('@microsoft/signalr'))
                            {
                                return 'vendor-signalr';
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
            }
        }
    });
    return mergeConfig(baseAppConfig, shellSpecificConfig);
});
