// scripts/copy-monaco.cjs

const fs = require('fs-extra');
const path = require('path');

console.log('🔍 Locating monaco-editor via @guolao/vue-monaco-editor...');

// 第 1 步：找到 @guolao/vue-monaco-editor 的 package.json 文件路径
// `require.resolve` 的第二个参数 `paths` 是关键。它告诉 Node.js 从哪里开始查找。
// 我们从当前工作目录 (process.cwd()) 开始找，这在 pnpm workspace 中是可靠的。
const vueMonacoEditorPkgPath = require.resolve('@guolao/vue-monaco-editor/package.json', { paths: [process.cwd()] });

// 第 2 步：从 @guolao/vue-monaco-editor 的位置，去解析 monaco-editor 的路径
// 再次使用 `paths` 选项，这次的起点是 @guolao/vue-monaco-editor 所在的目录。
// 这模拟了 Node.js 的模块解析算法：一个包总能找到它自己的依赖。
const monacoEditorPkgPath = require.resolve('monaco-editor/package.json', {
    paths: [path.dirname(vueMonacoEditorPkgPath)]
});

// 第 3 步：从 monaco-editor 的 package.json 推断出其根目录，然后找到 'min' 文件夹
const monacoEditorRoot = path.dirname(monacoEditorPkgPath);
const sourceLicenseFile1 = path.join(monacoEditorRoot, 'LICENSE');
const sourceLicenseFile2 = path.join(monacoEditorRoot, 'ThirdPartyNotices.txt');
const sourceDir = path.join(monacoEditorRoot, 'min');

// 目标目录：public/monaco-editor
const destDir = path.join(__dirname, '../public/monaco-editor');

async function copyMonacoFiles() {
    try {
        // 确保目标目录存在，如果不存在则创建
        await fs.ensureDir(destDir);

        // 清空目标目录，以防是旧版本的文件
        await fs.emptyDir(destDir);

        console.log('  > Copying min directory...')
        await fs.copy(sourceDir, path.join(destDir,'min'));

        console.log('  > Copying license file...');
        await fs.copy(sourceLicenseFile1, path.join(destDir, 'LICENSE'));
        await fs.copy(sourceLicenseFile2, path.join(destDir, 'ThirdPartyNotices.txt'));

        console.log('✅ Monaco Editor files copied to public directory successfully!');
    } catch (err) {
        console.error('❌ Error copying Monaco Editor files:', err);
        process.exit(1); // 出错时退出，以便 CI/CD 流程能够捕获到失败
    }
}

copyMonacoFiles();