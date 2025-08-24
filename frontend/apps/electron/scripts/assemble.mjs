import fs from 'fs-extra';
import path from 'path';

// --- 配置路径 ---
const projectRoot = path.resolve(process.cwd(), '../../..');
const electronRoot = path.resolve(projectRoot, 'frontend/apps/electron');

const electronDistSource = path.resolve(electronRoot, 'node_modules/electron/dist');

const cacheDir = path.resolve(projectRoot, 'build/cache');
const backendCacheDir = path.resolve(cacheDir, 'backend');
const frontendCacheDir = path.resolve(cacheDir, 'frontend');

const pluginsSource = path.resolve(projectRoot, 'build/Plugins');
const launcherSource = path.resolve(projectRoot, 'build/Launcher/launcher.exe');

// 目标路径
const outputRoot = path.resolve(projectRoot, 'build/YAESandBox'); // 最终产品根目录
const appDestDir = path.resolve(outputRoot, 'app');             // app 子目录
const pluginsDestDir = path.resolve(outputRoot, 'Plugins');     // Plugins 子目录

async function main() {
    console.log('🚀 [组装脚本] 正在从缓存和源文件组装应用程序...');

    // 1. 检查缓存是否存在
    if (!await fs.pathExists(backendCacheDir) || !await fs.pathExists(frontendCacheDir)) {
        throw new Error('缓存目录不存在。请先运行完整的构建流程 (`pnpm package:full`)。');
    }
    if (!await fs.pathExists(electronDistSource)) {
        throw new Error(`Electron 运行时未找到，路径: ${electronDistSource}。\n请在 'frontend/apps/electron' 目录下运行 'pnpm install'。`);
    }

    // 2. 准备最终的 app 目录
    console.log('🧹 正在清理并准备最终的应用目录...');
    await fs.emptyDir(outputRoot);
    await fs.ensureDir(appDestDir);
    await fs.ensureDir(pluginsDestDir);

    // 3. 组装文件
    console.log('🚚 正在从缓存和源文件复制文件...');

    // 3.1: 复制 Electron 运行时作为基础
    console.log('  - 正在复制 Electron 运行时...');
    await fs.copy(electronDistSource, appDestDir);

    // 3.2: 重命名主程序
    const originalExePath = path.resolve(appDestDir, 'electron.exe');
    const newExePath = path.resolve(appDestDir, 'YAESandBox.exe'); // 你的目标名称
    if (await fs.pathExists(originalExePath)) {
        console.log(`  - 正在将 electron.exe 重命名为 YAESandBox.exe...`);
        await fs.rename(originalExePath, newExePath);
    } else {
        console.warn(`[组装脚本] 未找到 electron.exe，跳过重命名。请检查 Electron 版本或平台。`);
    }

    // 3.1 从缓存复制后端
    console.log('  - 正在从缓存复制后端文件...');
    await fs.copy(backendCacheDir, appDestDir);

    // 3.2 从缓存复制前端到 wwwroot
    console.log('  - 正在从缓存复制前端文件到 wwwroot...');
    await fs.copy(frontendCacheDir, path.resolve(appDestDir, 'wwwroot'));

    // 3.3 复制 Plugins 文件夹到顶层
    if (await fs.pathExists(pluginsSource)) {
        console.log('  - 正在复制 Plugins 文件夹到顶层目录...');
        await fs.copy(pluginsSource, pluginsDestDir);
    }

    // 3.4 复制 Electron 主进程入口和生产 package.json
    console.log('  - 正在复制主进程文件和 package.json...');
    await fs.copy(path.resolve(electronRoot, 'main.js'), path.resolve(appDestDir, 'main.js'));

    const electronPackageJson = await fs.readJson(path.resolve(electronRoot, 'package.json'));
    const productionPackageJson = {
        name: "yaesandbox", // 保持简单，避免特殊字符
        version: electronPackageJson.version,
        main: 'main.js',
        dependencies: electronPackageJson.dependencies || {}
    };
    await fs.writeJson(path.resolve(appDestDir, 'package.json'), productionPackageJson, { spaces: 2 });

    // 3.5 复制启动器到顶层
    if (await fs.pathExists(launcherSource)) {
        console.log('  - 正在复制启动器到顶层目录...');
        await fs.copy(launcherSource, path.resolve(outputRoot, 'YAESandBoxLauncher.exe'));
    } else {
        console.warn(`[组装脚本] 在以下路径未找到启动器可执行文件: ${launcherSource}。正在跳过。`);
    }

    console.log('✅ [组装脚本] 组装完成！最终的应用结构已在以下目录准备就绪:', appDestDir);
}

main().catch(err => {
    console.error('❌ 组装过程中发生错误:', err);
    process.exit(1);
});