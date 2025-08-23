import fs from 'fs-extra';
import path from 'path';
import { execa } from 'execa';

// --- 配置路径 ---
const projectRoot = path.resolve(process.cwd(), '../../..');
const backendProject = path.resolve(projectRoot, 'backend/YAESandBox.AppWeb');
const shellAppSource = path.resolve(projectRoot, 'frontend/apps/shell');

const cacheDir = path.resolve(projectRoot, 'build/cache');
const backendCacheDir = path.resolve(cacheDir, 'backend');
const frontendCacheDir = path.resolve(cacheDir, 'frontend');

async function main() {
    console.log('🚀 [构建脚本] 开始编译所有源文件...');

    // 1. 准备缓存目录
    console.log('🧹 正在清理并准备缓存目录...');
    await fs.emptyDir(cacheDir);
    await fs.ensureDir(backendCacheDir);
    await fs.ensureDir(frontendCacheDir);

    // 2. 构建 .NET 后端到缓存
    console.log(' C# 正在构建 .NET 后端到缓存目录...');
    await execa(
        'dotnet',
        ['publish', backendProject, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '-o', backendCacheDir],
        { stdio: 'inherit' }
    );

    // 3. 构建 Vue 前端到缓存
    console.log('📦 正在构建 Vue 前端到缓存目录...');
    // 先执行 vite build，它会生成 dist 目录
    await execa('pnpm', ['--filter', '@yaesandbox-frontend/shell', 'build'], { cwd: path.resolve(projectRoot, 'frontend'), stdio: 'inherit' });
    // 然后将生成的 dist 目录移动到缓存区
    await fs.move(path.resolve(shellAppSource, 'dist'), frontendCacheDir, { overwrite: true });

    console.log('✅ [构建脚本] 所有源文件已成功编译到:', cacheDir);
}

main().catch(err => {
    console.error('❌ 构建过程中发生错误:', err);
    process.exit(1);
});