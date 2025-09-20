const fs = require('fs');
const os = require('os');
const path = require('path');
const { execSync } = require('child_process');

const platform = os.platform();
let executableName = 'compiler';
if (platform === 'win32') {
    executableName += '.exe';
}

const destinationDir = 'dist';
const finalExecutablePath = path.join(destinationDir, executableName);

// 1. 创建输出目录
if (!fs.existsSync(destinationDir)) {
    fs.mkdirSync(destinationDir);
}

// 2. 复制 Node.js 可执行文件
console.log(`Copying node executable to ${finalExecutablePath}...`);
fs.copyFileSync(process.execPath, finalExecutablePath);

// 3. 使用 postject 注入 blob
const postjectCommand = [
    'npx postject',
    finalExecutablePath,
    'NODE_SEA_BLOB',
    'sea-prep.blob',
    '--sentinel-fuse NODE_SEA_FUSE_fce680ab2cc467b6e072b8b5df1996b2',
].join(' ');

console.log('Injecting blob with postject...');
console.log(`Executing: ${postjectCommand}`);
execSync(postjectCommand, { stdio: 'inherit' });

// 4. (可选但推荐) 清理临时 blob 文件
fs.unlinkSync('sea-prep.blob');

console.log(`\n✅ Successfully created Single Executable Application at: ${finalExecutablePath}`);

// 为 Linux/macOS 添加执行权限
if (platform === 'linux' || platform === 'darwin') {
    fs.chmodSync(finalExecutablePath, '755');
    console.log('Added execute permissions for Linux/macOS.');
}