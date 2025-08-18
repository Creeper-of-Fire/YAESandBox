// /scripts/process-licenses.mjs (NEW AND IMPROVED SCRIPT - CHINESE VERSION)
import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

// --- 配置区 ---
// 1. 定义许可证白名单
const allowedLicenses = new Set([
    'MIT',
    'ISC',
    'Apache-2.0',
    'Apache 2.0',
    'BSD-2-Clause',
    'BSD-3-Clause',
    'MPL-2.0',
    'LGPL-2.1-or-later',
    'Unlicense',
    'BSD',
]);

// 2. 定义许可证文件输出路径
const outputFilePath = './apps/shell/public/THIRD_PARTY_LICENSES.txt';

// --- 主函数 ---
async function processLicenses() {
    console.log('🚀 开始处理许可证...');

    // --- 步骤 1: 检查许可证合规性 ---
    console.log('1/2: 正在检查许可证合规性...');
    try {
        await checkCompliance();
        console.log('✅ 所有生产环境依赖的许可证均合规。');
    } catch (error) {
        console.error('❌ 许可证检查失败！', error.message);
        // 明确地以错误码退出
        process.exit(1);
    }

    // --- 步骤 2: 生成许可证文件 ---
    console.log(`2/2: 正在生成许可证文件到 "${outputFilePath}"...`);
    try {
        await generateDisclaimer();
        console.log('✅ 许可证文件已成功生成。');
    } catch (error) {
        console.error('❌ 生成许可证文件失败！', error.message);
        process.exit(1);
    }

    console.log('🎉 许可证处理流程成功完成！');
}

// --- 辅助函数：检查合规性 ---
async function checkCompliance() {
    let licensesJson;
    try {
        // 运行 pnpm licenses 命令并获取 JSON 输出
        const { stdout } = await execAsync('pnpm licenses ls --prod --json');
        licensesJson = JSON.parse(stdout);
    } catch (error) {
        throw new Error(`执行 "pnpm licenses" 命令失败。 ${error.stderr}`);
    }

    const violations = [];
    for (const packagePath in licensesJson) {
        const packages = licensesJson[packagePath];
        for (const pkg of packages) {
            let license = pkg.license;

            if (!license || license === 'UNKNOWN') {
                violations.push({ name: pkg.name, version: pkg.version, license: '未声明 (UNLICENSED)' });
                continue;
            }

            // 简单的规范化处理，例如 'LGPL-2.1+' 变为 'LGPL-2.1-or-later'
            if (license.endsWith('+'))
                license = license.slice(0, -1) + '-or-later';

            // 处理复合许可证，如 '(MIT OR Apache-2.0)'
            const licenses = license.replace(/[()]/g, '').split(/ AND | OR /);
            // 检查是否所有子许可证都在白名单中
            const isAllowed = licenses.every(l => allowedLicenses.has(l.trim()));

            if (!isAllowed) {
                violations.push({ name: pkg.name, version: pkg.version, license: pkg.license });
            }
        }
    }

    if (violations.length > 0) {
        console.log('发现以下不合规或未声明许可证的依赖包：');
        console.table(violations);
        throw new Error(`请检查上表中的包。如果某个许可证是可接受的，请将其添加到 'scripts/process-licenses.mjs' 文件中的 'allowedLicenses' 集合里。`);
    }
}

// --- 辅助函数：生成文件 ---
async function generateDisclaimer() {
    const command = `pnpm-licenses generate-disclaimer --prod --output-file=${outputFilePath}`;
    try {
        const { stdout, stderr } = await execAsync(command);
        if (stderr) {
            // pnpm-licenses 有时会把进度信息输出到 stderr，我们需要检查是否是真正的错误
            if(stderr.toLowerCase().includes('error')) {
                throw new Error(stderr);
            }
            console.log(stderr); // 打印进度信息
        }
        console.log(stdout); // 打印成功信息
    } catch (error) {
        throw new Error(`命令 "${command}" 执行失败。请确保已在工作区根目录安装了 'pnpm-licenses'。 \n原始错误: ${error.message}`);
    }
}

// --- 运行主函数 ---
await processLicenses();