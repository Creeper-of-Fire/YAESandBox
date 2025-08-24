// scripts/build.mjs
import fs from 'fs';
import path from 'path';
import { execSync } from 'child_process';
import archiver from 'archiver';

// --- 配置 ---
// 从 tauri.conf.json 读取应用信息，避免硬编码
const tauriConfig = JSON.parse(fs.readFileSync('./src-tauri/tauri.conf.json', 'utf-8'));
const appName = tauriConfig.productName;
const appVersion = tauriConfig.version;

// 定义路径
const projectRoot = process.cwd();
const releaseDir = path.join(projectRoot, 'src-tauri/target/release');
const distDir = path.join(projectRoot, 'dist');
const portableDirName = `${appName}-v${appVersion}-portable`;
const portableDirPath = path.join(distDir, portableDirName);

// --- 脚本主逻辑 ---
async function main() {
    try {
        console.log('🚀 开始构建绿色版启动器...');

        // 1. 清理旧的输出目录
        console.log('🧹 清理旧的构建产物...');
        if (fs.existsSync(distDir)) {
            fs.rmSync(distDir, { recursive: true, force: true });
        }
        fs.mkdirSync(distDir, { recursive: true });

        // 2. 执行 Tauri 构建
        console.log('🛠️ 正在执行 `pnpm tauri build`... (这可能需要几分钟)');
        // stdio: 'inherit' 让子进程的输出直接显示在当前终端
        execSync('pnpm tauri build', { stdio: 'inherit' });
        console.log('✅ Tauri 构建成功!');

        // 3. 创建便携版目录并复制文件
        console.log(`📦 正在创建便携版目录: ${portableDirName}`);
        fs.mkdirSync(portableDirPath, { recursive: true });

        const filesToCopy = fs.readdirSync(releaseDir).filter(
            (file) => file.endsWith('.exe') || file.endsWith('.dll')
        );

        console.log('📄 正在复制以下文件:');
        for (const file of filesToCopy) {
            const sourcePath = path.join(releaseDir, file);
            const destPath = path.join(portableDirPath, file);
            console.log(`   - ${file}`);
            fs.copyFileSync(sourcePath, destPath);
        }

        console.log(`\n🎉 构建完成! 你的绿色版应用在这里:`);
        console.log(`   ${portableDirPath}`);

    } catch (error) {
        console.error('\n❌ 构建过程中发生错误:');
        console.error(error);
        process.exit(1); // 以错误码退出
    }
}

// 辅助函数：创建 ZIP
function createZipArchive(sourceDir, outPath) {
    const output = fs.createWriteStream(outPath);
    const archive = archiver('zip', {
        zlib: { level: 9 } // 最高压缩级别
    });

    return new Promise((resolve, reject) => {
        output.on('close', resolve);
        archive.on('error', (err) => reject(err));

        archive.pipe(output);
        archive.directory(sourceDir, false); // 第二个参数 false 表示不包含外层目录
        archive.finalize();
    });
}

// 运行主函数
main();