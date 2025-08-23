// afterPack.js
const { execSync } = require('child_process');
const path = require('path');

exports.default = async function(context) {
    // 1. 获取 afterPack.ps1 脚本的绝对路径
    const psScriptPath = path.join(__dirname, 'afterPack.ps1');

    // 2. 只从 context 对象中提取我们需要的 appOutDir 属性
    const appOutDir = context.appOutDir;

    // 3. 构建 PowerShell 执行命令
    //    我们直接把 appOutDir 这个字符串作为参数传递
    const command = `powershell -ExecutionPolicy Bypass -File "${psScriptPath}" -appOutDir "${appOutDir}"`;

    console.log('Executing afterPack PowerShell script...');
    console.log(`Command: ${command}`);

    try {
        // 4. 同步执行命令
        execSync(command, { stdio: 'inherit' });
        console.log('PowerShell script executed successfully.');
    } catch (error) {
        console.error('Error executing PowerShell script:');
        throw error;
    }
};