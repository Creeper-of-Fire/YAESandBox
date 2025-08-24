import fs from 'fs';
import path from 'path';

// --- 路径配置 ---
// 脚本的当前工作目录
const projectRoot = process.cwd();

// 源目录：前端构建后生成的 dist 文件夹
const sourceDir = path.join(projectRoot, 'dist');

// 目标目录：向上返回三级到解决方案根目录，然后进入 build/frontend
const solutionRoot = path.resolve(projectRoot, '../../..');
const outputDir = path.join(solutionRoot, 'build', 'frontend');

// --- 脚本主逻辑 ---
function main() {
    try {
        console.log('🚀 开始将前端构建产物部署到解决方案 build 目录...');

        // 1. 检查源目录是否存在
        // 确保前端构建已经成功执行
        if (!fs.existsSync(sourceDir)) {
            console.error(`❌ 错误: 源目录 ${sourceDir} 不存在。`);
            console.error('   请先执行前端构建命令 (例如: pnpm build) 以生成 dist 目录。');
            process.exit(1); // 以错误码退出
        }

        // 2. 清理并创建目标目录
        // 确保每次部署都是一个干净的状态
        console.log(`🧹 清理并准备输出目录: ${outputDir}`);
        if (fs.existsSync(outputDir)) {
            fs.rmSync(outputDir, { recursive: true, force: true });
        }
        fs.mkdirSync(outputDir, { recursive: true });

        // 3. 复制构建产物
        console.log('📦 正在复制构建产物...');
        console.log(`   - 从: ${sourceDir}`);
        console.log(`   - 到: ${outputDir}`);

        // 使用 fs.cpSync 递归复制整个目录的内容
        fs.cpSync(sourceDir, outputDir, { recursive: true });

        console.log(`\n🎉 前端构建产物部署成功!`);
        console.log(`   文件已复制到: ${outputDir}`);

    } catch (error) {
        console.error('\n❌ 部署过程中发生错误:');
        console.error(error);
        process.exit(1);
    }
}

// 运行主函数
main();