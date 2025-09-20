// 在 SEA 环境下，默认的 require 是受限的。
// 我们需要使用 module.createRequire 来创建一个功能完整的 require 函数，
// 它可以正确地从 node_modules 加载依赖。
const { createRequire } = require('node:module');
const customRequire = createRequire(__filename);

const esbuild = customRequire('esbuild');
const vue = customRequire('unplugin-vue/esbuild');
const yargs = customRequire('yargs');
const { hideBin } = customRequire('yargs/helpers');

const fs = require('fs');
const path = require('path');

// 使用 yargs 解析命令行参数
const argv = yargs(hideBin(process.argv))
    .option('input', {
        alias: 'i',
        type: 'string',
        description: 'Path to the input source file (.vue or .jsx)',
        demandOption: true,
    })
    .option('output', {
        alias: 'o',
        type: 'string',
        description: 'Path for the output compiled JavaScript file',
        demandOption: true,
    })
    .help()
    .argv;

async function main() {
    try {
        const sourceCode = fs.readFileSync(argv.input, 'utf-8');

        const result = await esbuild.build({
            entryPoints: [argv.input],
            bundle: true,
            write: false,
            format: 'esm',
            plugins: [vue()],
            external: ['vue'], // 将 'vue' 设为外部依赖，不打包进去
            outdir: path.dirname(argv.output),
        });

        if (result.outputFiles && result.outputFiles.length > 0) {
            // 假定第一个输出文件是 JS bundle
            const outputFile = result.outputFiles.find(f => f.path.endsWith(path.basename(argv.output)));
            if(outputFile) {
                fs.writeFileSync(argv.output, outputFile.text);
                console.log(`Compilation successful. Output written to ${argv.output}`);
            } else {
                throw new Error('Could not find the expected output file in the build result.');
            }
        } else {
            throw new Error('esbuild did not produce any output files.');
        }
    } catch (error) {
        // **关键**: 将错误信息输出到 stderr，这样 C# 父进程才能捕获到错误详情
        console.error('Compilation failed:', error.message);
        // 以非 0 状态码退出，表示失败
        process.exit(1);
    }
}

main();