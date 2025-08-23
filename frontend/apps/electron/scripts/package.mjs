import fs from 'fs-extra';
import path from 'path';
import {execa} from 'execa';

// --- 配置路径 ---
// 使用 path.resolve 确保我们得到的是绝对路径，避免在不同工作目录下运行脚本时出错
const projectRoot = path.resolve(process.cwd(), '../../..'); // 从 shell/scripts 返回到 YAESandBox 根目录
const electronRoot = path.resolve(projectRoot, 'frontend/apps/electron');

// 源路径
const backendProject = path.resolve(projectRoot, 'backend/YAESandBox.AppWeb');
const pluginsSource = path.resolve(projectRoot, 'Plugins');
const shellAppSource = path.resolve(projectRoot, 'frontend/apps/shell');

// 目标路径 (所有东西最终都组装到这里)
const buildDir = path.resolve(projectRoot, 'build');
const appDir = path.resolve(buildDir, 'app'); // 这就是你想要的那个 app 文件夹
const backendDest = path.resolve(appDir, "backend"); // 后端直接放在 app 根目录
const pluginsDest = path.resolve(buildDir, 'Plugins');
const frontendDest = path.resolve(appDir, 'wwwroot'); // 将前端构建产物放入 wwwroot，保持清晰

async function main() {
    console.log('🚀 开始为 Electron 手动打包进行组装...');

    // 1. 清理旧的构建目录
    console.log('🧹 清理旧目录...');
    await fs.emptyDir(buildDir);
    await fs.ensureDir(appDir);

    // 2. 构建 .NET 后端
    console.log(' C# 后端发布中...');
    await execa(
        'dotnet',
        ['publish', backendProject, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '-o', appDir],
        { stdio: 'inherit' }
    );

    // 3. 构建 Vue 前端
    console.log('📦 前端构建中...');
    await execa('pnpm', ['--filter', '@yaesandbox-frontend/shell', 'build'], { cwd: path.resolve(projectRoot, 'frontend'), stdio: 'inherit' });

    // 4. 组装文件
    console.log('🚚 组装最终 app 目录...');

    // 4.1 移动前端产物到 wwwroot
    await fs.move(path.resolve(shellAppSource, 'dist'), frontendDest, {overwrite: true});

    // 4.2 复制 Plugins
    if (await fs.pathExists(pluginsSource)) {
        console.log('  - 复制 Plugins 文件夹...');
        await fs.copy(pluginsSource, pluginsDest);
    }

    // 4.3 复制 Electron 主进程入口
    console.log('  - 复制主进程文件...');
    await fs.copy(path.resolve(electronRoot, 'main.js'), path.resolve(appDir, 'main.js'));

    // 4.4 ✨ 复制 electron 应用的生产 package.json ✨
    // 这是 electron-builder 运行时需要的
    const electronPackageJson = await fs.readJson(path.resolve(electronRoot, 'package.json'));
    const productionPackageJson = {
        name: electronPackageJson.name,
        version: electronPackageJson.version,
        main: electronPackageJson.main,
        dependencies: electronPackageJson.dependencies || {} // 只保留生产依赖
    };
    await fs.writeJson(path.resolve(appDir, 'package.json'), productionPackageJson, { spaces: 2 });

    console.log('✅ 组装完成！最终的应用结构已准备好在:', appDir);
}

main().catch(err => {
    console.error('❌ 构建过程中发生错误:', err);
    process.exit(1);
});