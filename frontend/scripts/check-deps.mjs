// scripts/check-deps.mjs
import { exec } from 'child_process';
import { readFile } from 'fs/promises';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const rootDir = resolve(__dirname, '..');

const colors = {
    reset: "\x1b[0m",
    bright: "\x1b[1m",
    cyan: "\x1b[36m",
    red: "\x1b[31m",
    green: "\x1b[32m",
    yellow: "\x1b[33m",
};

async function main() {
    console.log(`${colors.bright}Running dependency check for all packages...${colors.reset}\n`);

    // 1. 获取所有包的信息
    let packages;
    try {
        const stdout = await new Promise((res, rej) => {
            exec('pnpm list -r --depth -1 --json', { cwd: rootDir, maxBuffer: 10 * 1024 * 1024 }, (err, stdout, stderr) => {
                if (err) {
                    console.error(`${colors.red}Failed to list packages. pnpm error:${colors.reset}\n${stderr}`);
                    return rej(err);
                }
                res(stdout);
            });
        });
        // 【关键修复】直接将整个 stdout 解析为一个 JSON 数组
        packages = JSON.parse(stdout);
    } catch (e) {
        console.error(`${colors.red}Failed to parse the output of 'pnpm list'. Is pnpm installed and working correctly?${colors.reset}`);
        console.error(e);
        process.exit(1);
    }

    let hasErrors = false;
    // 过滤掉根项目，我们通常不检查它
    const workspacePackages = packages.filter(pkg => pkg.path !== rootDir);

    // 2. 遍历每个包并执行 depcheck
    for (const pkg of workspacePackages) {
        if (!pkg.name || !pkg.path) continue;

        console.log(`--- ${colors.bright}${colors.cyan}${pkg.name}${colors.reset} (${pkg.path.replace(rootDir, '.')}) ---`);

        const { error, stdout, stderr } = await new Promise((res) => {
            exec('depcheck', { cwd: pkg.path }, (error, stdout, stderr) => {
                res({ error, stdout, stderr });
            });
        });

        // 【关键修复】接受并直接输出非 JSON 部分
        // 任何非空的 stdout 或 stderr 都值得打印
        if (stdout.trim()) {
            console.log(stdout.trim());
        }
        if (stderr.trim()) {
            console.error(`${colors.yellow}${stderr.trim()}${colors.reset}`);
        }

        if (error) {
            hasErrors = true;
            // depcheck 返回非零退出码，打印一个明确的失败信息
            console.error(`${colors.red}Check failed for ${pkg.name} with exit code: ${error.code}${colors.reset}`);
        } else if (!stdout.trim() && !stderr.trim()) {
            // 如果没有任何输出，并且没有错误，说明检查通过
            console.log(`${colors.green}✅ No dependency issues found.${colors.reset}`);
        }
        console.log(''); // 添加分隔
    }

    // 3. 决定最终退出码
    if (hasErrors) {
        console.log(`${colors.bright}${colors.red}Dependency check finished with errors.${colors.reset}`);
        process.exit(1);
    } else {
        console.log(`${colors.bright}${colors.green}All packages passed dependency check!${colors.reset}`);
    }
}

main().catch(err => {
    console.error(`${colors.red}An unexpected error occurred in the script runner:`, err, colors.reset);
    process.exit(1);
});