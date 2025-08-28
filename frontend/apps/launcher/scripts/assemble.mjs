// scripts/assemble.mjs
import fs from 'fs';
import path from 'path';

// --- 路径配置 ---
const solutionRoot = path.resolve(process.cwd(), '../../..');
const buildDir = path.join(solutionRoot, 'build');

// 源目录
const launcherSourceDir = path.join(buildDir, 'launcher');
const frontendSourceDir = path.join(buildDir, 'frontend');
const backendSourceDir = path.join(buildDir, 'backend');
const pluginsSourceDir = path.join(buildDir, 'Plugins');

// 最终输出目录
const outputDir = path.join(buildDir, 'YAESandBox');

// --- 脚本主逻辑 ---
function main() {
    try {
        console.log('🚀 开始组装最终应用包: YAESandBox...');

        // 1. 清理并创建最终输出目录
        // console.log(`🧹 清理旧的输出目录: ${outputDir}`);
        // if (fs.existsSync(outputDir)) {
        //     fs.rmSync(outputDir, { recursive: true, force: true });
        // }
        fs.mkdirSync(outputDir, { recursive: true });
        console.log('✅ 输出目录已准备就绪。');

        // 2. 组装各个部分
        // 使用 fs.cpSync 来递归复制目录，这是 Node.js v16.7.0+ 的现代高效方法

        // 步骤 2.1: 复制 Launcher
        copyDirectory(launcherSourceDir, outputDir, '启动器 (Launcher)');

        // --- Backend 复制逻辑修改开始 ---
        // 步骤 2.2: 复制 Backend (只复制 .exe 和 appsettings.json)
        console.log(`\n📦 正在组装 后端 (Backend)...`);
        const backendDest = path.join(outputDir, 'backend');

        if (!fs.existsSync(backendSourceDir)) {
            console.warn(`   ⚠️  警告: 源目录 ${backendSourceDir} 不存在，跳过 后端 的复制。`);
        } else {
            // 确保目标目录存在
            fs.mkdirSync(backendDest, { recursive: true });
            console.log(`   - 目标目录: ${backendDest}`);
            let copiedCount = 0;

            // 查找并复制所有 .exe 文件
            const backendFiles = fs.readdirSync(backendSourceDir);
            const exeFiles = backendFiles.filter(file => path.extname(file).toLowerCase() === '.exe');

            if (exeFiles.length === 0) {
                console.warn(`   ⚠️  警告: 在 ${backendSourceDir} 中未找到 .exe 文件。`);
            } else {
                for (const exeFile of exeFiles) {
                    const sourcePath = path.join(backendSourceDir, exeFile);
                    const destPath = path.join(backendDest, exeFile);
                    fs.copyFileSync(sourcePath, destPath);
                    console.log(`   - 已复制: ${exeFile}`);
                    copiedCount++;
                }
            }

            // 复制 appsettings.json
            const appSettingsFile = 'appsettings.json';
            const appSettingsSourcePath = path.join(backendSourceDir, appSettingsFile);
            if (fs.existsSync(appSettingsSourcePath)) {
                const appSettingsDestPath = path.join(backendDest, appSettingsFile);
                fs.copyFileSync(appSettingsSourcePath, appSettingsDestPath);
                console.log(`   - 已复制: ${appSettingsFile}`);
                copiedCount++;
            } else {
                console.warn(`   ⚠️  警告: 在 ${backendSourceDir} 中未找到 ${appSettingsFile}。`);
            }

            if (copiedCount > 0) {
                console.log(`   ✅ 后端 的 ${copiedCount} 个文件复制成功!`);
            } else {
                console.error(`   ❌ 未能从 ${backendSourceDir} 复制任何指定的后端文件。`);
            }
        }
        // --- Backend 复制逻辑修改结束 ---

        // 步骤 2.3: 复制 Frontend
        const frontendDest = path.join(outputDir, 'wwwroot');
        // 无需手动创建 app/wwwroot，因为 copyDirectory 辅助函数会处理
        copyDirectory(frontendSourceDir, frontendDest, '前端 (Frontend)');

        // 步骤 2.4: 复制 Plugins
        const pluginsDest = path.join(outputDir, 'Plugins');
        copyDirectory(pluginsSourceDir, pluginsDest, '插件 (Plugins)');

        console.log(`\n🎉 组装完成! 你的应用包在这里:`);
        console.log(`   ${outputDir}`);

    } catch (error) {
        console.error('\n❌ 组装过程中发生错误:');
        console.error(error);
        process.exit(1);
    }
}

/**
 * 辅助函数：复制目录内容
 * @param {string} source - 源目录路径
 * @param {string} destination - 目标目录路径
 * @param {string} componentName - 用于日志记录的组件名称
 */
function copyDirectory(source, destination, componentName) {
    console.log(`\n📦 正在组装 ${componentName}...`);

    // 检查源目录是否存在
    if (!fs.existsSync(source)) {
        console.warn(`   ⚠️  警告: 源目录 ${source} 不存在，跳过 ${componentName} 的复制。`);
        return;
    }

    // 创建目标目录（如果尚不存在）
    fs.mkdirSync(destination, { recursive: true });

    // 执行复制
    fs.cpSync(source, destination, { recursive: true });

    console.log(`   - 从: ${source}`);
    console.log(`   - 到:   ${destination}`);
    console.log(`   ✅ ${componentName} 复制成功!`);
}


// 运行主函数
main();