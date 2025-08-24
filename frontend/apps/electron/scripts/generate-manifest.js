// generate-manifest.mjs
import fs from 'fs-extra';
import path from 'path';
import crypto from 'crypto';

// --- 配置 ---
const projectRoot = path.resolve(process.cwd(), '../../..');
const assembledDir = path.resolve(projectRoot, 'build/YAESandBox'); // 组装完成的目录
const releaseOutputDir = path.resolve(projectRoot, 'build/release'); // 最终发布产物目录

// 从你的 package.json 读取版本号
const packageJsonPath = path.resolve(assembledDir, 'app/package.json');
const version = (await fs.readJson(packageJsonPath)).version;

const serverBaseUrl = `https://your-server.com/releases/v${version}/`;

async function getFilesRecursively(dir) {
    const dirents = await fs.readdir(dir, { withFileTypes: true });
    const files = await Promise.all(dirents.map((dirent) => {
        const res = path.resolve(dir, dirent.name);
        return dirent.isDirectory() ? getFilesRecursively(res) : res;
    }));
    return Array.prototype.concat(...files);
}

function calculateFileHash(filePath) {
    const buffer = fs.readFileSync(filePath);
    const hash = crypto.createHash('sha256');
    hash.update(buffer);
    return hash.digest('hex');
}

async function main() {
    console.log(`🚀 [清单生成] 开始为版本 v${version} 生成发布文件...`);

    await fs.emptyDir(releaseOutputDir);

    // 1. 生成详细清单 (manifest.json)
    const manifest = {
        version: version,
        baseUrl: serverBaseUrl,
        files: {}
    };

    const allFiles = await getFilesRecursively(assembledDir);

    for (const filePath of allFiles) {
        const relativePath = path.relative(assembledDir, filePath).replace(/\\/g, '/');
        manifest.files[relativePath] = {
            hash: calculateFileHash(filePath),
            size: (await fs.stat(filePath)).size
        };
    }

    const manifestPath = path.resolve(releaseOutputDir, 'manifest.json');
    await fs.writeJson(manifestPath, manifest, { spaces: 2 });
    console.log(`  ✅ manifest.json 已生成到: ${manifestPath}`);

    // 2. 生成顶层版本信息 (latest-version.json)
    const latestVersionInfo = {
        version: version,
        manifestUrl: serverBaseUrl + 'manifest.json'
    };
    const latestVersionPath = path.resolve(releaseOutputDir, 'latest-version.json');
    await fs.writeJson(latestVersionPath, latestVersionInfo, { spaces: 2 });
    console.log(`  ✅ latest-version.json 已生成到: ${latestVersionPath}`);

    // 3. 复制所有组装好的文件，用于上传
    const filesDestDir = path.resolve(releaseOutputDir, `v${version}`);
    await fs.copy(assembledDir, filesDestDir);
    console.log(`  ✅ 应用文件已复制到: ${filesDestDir}`);

    console.log('\n🎉 发布文件准备就绪！');
    console.log('下一步操作:');
    console.log(`  1. 将 "build/release/v${version}" 目录下的所有内容上传到服务器的 "${serverBaseUrl}" 路径下。`);
    console.log(`  2. 将 "build/release/latest-version.json" 文件上传到服务器的根路径 "https://your-server.com/releases/"。`);
}

main().catch(err => {
    console.error('❌ 生成清单时出错:', err);
    process.exit(1);
});