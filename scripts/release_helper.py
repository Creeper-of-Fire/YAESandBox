# release_helper.py
import hashlib
import json
import os
import zipfile
from pathlib import Path

import requests
from dotenv import load_dotenv
from rich.console import Console
from rich.progress import Progress, BarColumn, DownloadColumn, TransferSpeedColumn, TimeRemainingColumn
from rich.prompt import Prompt, Confirm
from rich.table import Table

# --- 配置区 ---
# GitHub 仓库信息
REPO_OWNER = "Creeper-of-Fire"
REPO_NAME = "YAESandBox"

# --- 路径配置 (根据你的描述调整) ---
# 假设此脚本放在解决方案根目录下的 'scripts' 文件夹
SOLUTION_ROOT = Path(__file__).parent.resolve().parent
BUILD_DIR = SOLUTION_ROOT / 'build'

# 源目录
LAUNCHER_SOURCE_DIR = BUILD_DIR / 'launcher'
FRONTEND_SOURCE_DIR = BUILD_DIR / 'frontend'
BACKEND_SOURCE_DIR = BUILD_DIR / 'backend'
BACKEND_SLIM_SOURCE_DIR = BUILD_DIR / 'backend' / 'slim'
PLUGINS_SOURCE_DIR = BUILD_DIR / 'Plugins'

# 输出目录 (用于存放打包好的 zip 和 manifest)
DIST_DIR = SOLUTION_ROOT / 'build' / 'dist'

# 状态文件
STATE_FILE = DIST_DIR / '.release_state.json'
# --- 配置区结束 ---

# 初始化 Rich Console
console = Console()


def get_dir_hash(directory: Path, filter_func=None) -> str:
    """计算目录内容的 SHA256 哈希值"""
    sha256 = hashlib.sha256()
    # 确保哈希计算是确定性的，对文件路径进行排序
    files = sorted(
        [p for p in directory.rglob('*') if p.is_file() and (not filter_func or filter_func(p))],
        key=lambda p: p.relative_to(directory)
    )

    if not files:
        return "empty"

    for file_path in files:
        # 将相对路径也加入哈希计算，这样文件重命名也会被检测到
        sha256.update(str(file_path.relative_to(directory)).encode())
        with open(file_path, 'rb') as f:
            while chunk := f.read(8192):
                sha256.update(chunk)
    return sha256.hexdigest()


def get_file_hash(file_path: Path) -> str:
    """计算单个文件的 SHA256 哈希值"""
    sha256 = hashlib.sha256()
    with open(file_path, 'rb') as f:
        while chunk := f.read(8192):
            sha256.update(chunk)
    return sha256.hexdigest()


def load_last_state():
    """加载上一次的发布状态，包含完整的 manifest 数据和哈希"""
    if not STATE_FILE.exists():
        return {"hashes": {}, "manifests": {}}
    with open(STATE_FILE, 'r', encoding='utf-8') as f:
        state = json.load(f)
        # 兼容旧格式
        if "hashes" not in state:
            return {"hashes": state, "manifests": {}}
        return state


def save_current_state(hashes, manifests):
    """保存当前的哈希和 manifest 数据"""
    STATE_FILE.parent.mkdir(exist_ok=True)
    state = {"hashes": hashes, "manifests": manifests}
    with open(STATE_FILE, 'w', encoding='utf-8') as f:
        json.dump(state, f, indent=4, ensure_ascii=False)


def package_component(name: str, src_dir: Path, filter_func=None) -> Path:
    """通用打包函数"""
    zip_path = DIST_DIR / f"{name}.zip"
    console.print(f"📦 正在打包 [cyan]{name}[/cyan] 从 [yellow]{src_dir}[/yellow] 到 [yellow]{zip_path}[/yellow]")

    with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
        files_to_zip = [p for p in src_dir.rglob('*') if p.is_file() and (not filter_func or filter_func(p))]
        for file in files_to_zip:
            arcname = file.relative_to(src_dir)
            zipf.write(file, arcname)
    console.print(f"✅ [cyan]{name}[/cyan] 打包完成.")
    return zip_path


def backend_filter(path: Path) -> bool:
    """后端打包的特殊过滤器"""
    return path.name.lower().endswith('.exe') or path.name.lower() == 'appsettings.json'


def find_unique_exe(directory: Path) -> Path | None:
    """在指定目录中查找唯一的 .exe 文件"""
    exes = list(directory.glob('*.exe'))
    if len(exes) == 1:
        return exes[0]
    if len(exes) > 1:
        console.print(f"[bold yellow]警告：[/bold yellow] 在 {directory} 中发现多个 .exe 文件，无法确定要发布的独立可执行文件。")
    return None


def main():
    """主执行函数"""
    # 加载环境变量 (GH_RELEASE_TOKEN)
    load_dotenv()
    token = os.getenv("GH_RELEASE_TOKEN")
    if not token:
        console.print("[bold red]错误：[/bold red] 未找到 GH_RELEASE_TOKEN 环境变量。请在 .env 文件中配置。")
        return

    # 准备工作
    DIST_DIR.mkdir(exist_ok=True)
    last_state = load_last_state()
    last_hashes = last_state.get("hashes", {})
    current_hashes = {}

    components = []

    # 1. 定义和检测核心组件
    core_components_def = [
        {"id": "launcher", "name": "启动器", "path": LAUNCHER_SOURCE_DIR, "publish_exe": True, "manifest_type": ["full", "slim"]},
        {"id": "app", "name": "前端应用", "path": FRONTEND_SOURCE_DIR, "manifest_type": ["full", "slim"]},
        {"id": "backend", "name": ".NET 后端 (完整版)", "path": BACKEND_SOURCE_DIR, "filter": backend_filter, "manifest_type": ["full"]},
        {"id": "backend-slim", "name": ".NET 后端 (精简版)", "path": BACKEND_SLIM_SOURCE_DIR, "filter": backend_filter, "manifest_type": ["slim"]},
    ]

    for comp in core_components_def:
        if comp['path'].exists():
            current_hash = get_dir_hash(comp['path'], comp.get('filter'))
            current_hashes[comp['id']] = current_hash
            comp_data = {
                **comp,
                "changed": last_hashes.get(comp['id']) != current_hash,
                "is_plugin": False
            }
            components.append(comp_data)

    # 2. 定义和检测插件
    current_hashes['plugins'] = {}
    if PLUGINS_SOURCE_DIR.exists():
        for plugin_dir in PLUGINS_SOURCE_DIR.iterdir():
            if plugin_dir.is_dir():
                plugin_id = plugin_dir.name
                current_hash = get_dir_hash(plugin_dir)
                current_hashes['plugins'][plugin_id] = current_hash
                last_plugin_hashes = last_hashes.get('plugins', {})
                components.append({
                    "id": plugin_id,
                    "name": f"插件: {plugin_id}",
                    "path": plugin_dir,
                    "filter": None,
                    "changed": last_plugin_hashes.get(plugin_id) != current_hash,
                    "is_plugin": True
                })

    # 3. 显示变更并让用户确认
    table = Table(title="🔍 检测到文件变更")
    table.add_column("组件名称", style="cyan")
    table.add_column("ID", style="magenta")
    table.add_column("状态", style="green")

    changed_components = [c for c in components if c['changed']]
    if not changed_components:
        console.print("[bold green]✅ 所有组件都是最新的，无需发布。[/bold green]")
        return

    for comp in components:
        status = "[bold yellow]有变更[/bold yellow]" if comp['changed'] else "无变更"
        table.add_row(comp['name'], comp['id'], status)

    console.print(table)

    if not Confirm.ask("[bold yellow]是否基于以上变更创建一个新版本？[/bold yellow]", default=True):
        console.print("操作已取消。")
        return

    # 4. 获取发布信息
    console.print("\n--- 请输入新版本信息 ---")
    version = Prompt.ask("请输入版本号 (例如: v1.2.3)")
    release_title = Prompt.ask("请输入发布标题", default=f"Release {version}")
    console.print("请输入更新日志 (输入 'EOF' 结束):")
    release_notes_lines = []
    while True:
        line = input()
        if line == 'EOF':
            break
        release_notes_lines.append(line)
    release_notes = "\n".join(release_notes_lines)

    # 5. 用户选择要发布的组件
    console.print("\n--- 请选择要包含在此次发布中的组件 ---")
    to_release = []
    for comp in changed_components:
        if Confirm.ask(f"是否发布 [cyan]{comp['name']}[/cyan]?", default=True):
            to_release.append(comp)

    if not to_release:
        console.print("没有选择任何组件进行发布。操作已取消。")
        return

    # 6. 打包所选组件
    packaged_files = []
    extra_assets_to_upload = []
    for comp in to_release:
        # 打包zip文件
        package_name = comp['id']
        zip_path = package_component(package_name, comp['path'], comp['filter'])
        file_hash = get_file_hash(zip_path)
        packaged_files.append({
            "id": comp['id'],
            "name": comp['name'],
            "zip_path": zip_path,
            "hash": file_hash,
            "is_plugin": comp['is_plugin']
        })

        # 检查是否需要发布独立的 .exe
        if comp.get('publish_exe'):
            exe_path = find_unique_exe(comp['path'])
            if exe_path:
                console.print(f"➕ 找到并准备发布独立的启动器: [green]{exe_path.name}[/green]")
                extra_assets_to_upload.append(exe_path)

    # 7. 创建 GitHub Release
    console.print(f"\n🚀 准备在 [cyan]{REPO_OWNER}/{REPO_NAME}[/cyan] 创建 Release [green]{version}[/green]...")
    if not Confirm.ask("确认执行？", default=True):
        console.print("操作已取消。")
        return

    headers = {
        "Authorization": f"token {token}",
        "Accept": "application/vnd.github.v3+json"
    }
    release_data = {
        "tag_name": version,
        "name": release_title,
        "body": release_notes,
        "draft": False,
        "prerelease": False
    }

    try:
        response = requests.post(
            f"https://api.github.com/repos/{REPO_OWNER}/{REPO_NAME}/releases",
            headers=headers,
            json=release_data
        )
        response.raise_for_status()
        release_info = response.json()
        upload_url = release_info['upload_url'].split('{')[0]
        console.print(f"✅ GitHub Release 创建成功: [link={release_info['html_url']}]{release_info['html_url']}[/link]")

    except requests.exceptions.RequestException as e:
        console.print(f"[bold red]❌ 创建 GitHub Release 失败: {e}[/bold red]")
        console.print(f"响应内容: {e.response.text}")
        return

    # 8. 上传打包文件作为 Assets
    console.print("\n📤 正在上传组件文件...")
    uploaded_assets_info = []
    all_zips_and_exes = [asset['zip_path'] for asset in packaged_files] + extra_assets_to_upload

    with Progress(BarColumn(), DownloadColumn(), TransferSpeedColumn(), TimeRemainingColumn(), console=console) as progress:
        for file_path in all_zips_and_exes:
            file_name = file_path.name
            task = progress.add_task(f"[cyan]{file_name}", total=file_path.stat().st_size)

            content_type = "application/zip" if file_name.endswith('.zip') else "application/vnd.microsoft.portable-executable"
            headers_upload = {"Authorization": f"token {token}", "Accept": "application/vnd.github.v3+json", "Content-Type": content_type}

            with open(file_path, 'rb') as f:
                try:
                    upload_response = requests.post(f"{upload_url}?name={file_name}", headers=headers_upload, data=f)
                    upload_response.raise_for_status()
                    asset_info = upload_response.json()
                    progress.update(task, completed=file_path.stat().st_size)

                    if file_path.name.endswith('.zip'):
                        original_asset = next((p for p in packaged_files if p['zip_path'] == file_path), None)
                        if original_asset:
                            original_asset['url'] = asset_info['browser_download_url']
                            uploaded_assets_info.append(original_asset)

                except requests.exceptions.RequestException as e:
                    console.print(f"[bold red]❌ 上传 {file_name} 失败: {e}[/bold red]\n响应内容: {e.response.text}")
                    continue

    # 9. 生成 Manifest 文件
    console.print("\n📝 正在生成 Manifest 文件...")
    last_manifests = last_state.get("manifests", {})



    # --- 核心组件清单 (完整版 & 精简版) ---
    last_full_manifest = last_manifests.get("full", {})
    last_slim_manifest = last_manifests.get("slim", {})
    full_core_map = {item['id']: item for item in last_full_manifest.get("components", [])}
    slim_core_map = {item['id']: item for item in last_slim_manifest.get("components", [])}

    core_assets = [a for a in uploaded_assets_info if not a['is_plugin']]
    for asset in core_assets:
        manifest_entry = {
            "name": asset['name'], "version": version.lstrip('v'), "notes": release_notes,
            "url": asset['url'], "hash": asset['hash'],
        }
        # 根据组件定义，决定更新哪个清单
        if 'full' in asset['manifest_type']:
            full_core_map[asset['id']] = {"id": asset['id'], **manifest_entry}
        if 'slim' in asset['manifest_type']:
            # 关键：对于 slim 清单，backend-slim 的 id 必须是 'backend'
            target_id = 'backend' if asset['id'] == 'backend-slim' else asset['id']
            slim_core_map[target_id] = {"id": target_id, **manifest_entry}

    new_full_manifest = {"components": list(full_core_map.values())}
    core_manifest_path = DIST_DIR / "core_components_manifest.json"
    with open(core_manifest_path, 'w', encoding='utf-8') as f:
        json.dump(new_full_manifest, f, ensure_ascii=False, indent=4)
    console.print(f"  -> 已生成核心组件清单 (完整版): [green]{core_manifest_path.name}[/green]")

    new_slim_manifest = {"components": list(slim_core_map.values())}
    slim_manifest_path = DIST_DIR / "core_components_slim_manifest.json"
    with open(slim_manifest_path, 'w', encoding='utf-8') as f:
        json.dump(new_slim_manifest, f, ensure_ascii=False, indent=4)
    console.print(f"  -> 已生成核心组件清单 (精简版): [green]{slim_manifest_path.name}[/green]")

    # --- 插件清单 ---
    last_plugins_manifest = last_state.get("manifests", {}).get("plugins", [])
    plugins_map = {item['id']: item for item in last_plugins_manifest}
    # ✨ 核心改动 2: 无条件地处理插件
    plugin_assets = [a for a in uploaded_assets_info if a['is_plugin']]
    for asset in plugin_assets:
        plugins_map[asset['id']] = {
            "id": asset['id'], "name": asset['name'].replace("插件: ", ""), "version": version.lstrip('v'),
            "description": f"发布于 {version}:\n{release_notes}", "url": asset['url'], "hash": asset['hash'],
        }

    # 将更新后的 map 转换回清单格式
    new_plugins_manifest = list(plugins_map.values())
    # 始终定义并写入文件路径
    plugins_manifest_path = DIST_DIR / "plugins_manifest.json"
    with open(plugins_manifest_path, 'w', encoding='utf-8') as f:
        json.dump(new_plugins_manifest, f, ensure_ascii=False, indent=4)
    # 同样，根据是否有更新来决定打印样式
    if plugin_assets:
        console.print(f"  -> 已更新并生成插件清单: [green]{plugins_manifest_path.name}[/green]")
    else:
        console.print(f"  -> 插件无变更，已沿用旧版内容重新生成清单: [dim green]{plugins_manifest_path.name}[/dim green]")

    # ✨ 核心改动 3: 无条件地准备上传两个清单文件
    console.print("\n📤 正在准备上传所有 Manifest 文件...")
    # 现在这两个路径总是有效的 Path 对象，不再需要检查 None
    manifests_to_upload = [core_manifest_path, slim_manifest_path, plugins_manifest_path]

    with Progress(BarColumn(), DownloadColumn(), console=console) as progress:
        for file_path in manifests_to_upload:
            file_name = file_path.name
            task = progress.add_task(f"[cyan]{file_name}", total=file_path.stat().st_size)
            headers_upload = {"Authorization": f"token {token}", "Accept": "application/vnd.github.v3+json", "Content-Type": "application/json"}

            with open(file_path, 'rb') as f:
                try:
                    upload_response = requests.post(f"{upload_url}?name={file_name}", headers=headers_upload, data=f)
                    upload_response.raise_for_status()
                    progress.update(task, completed=file_path.stat().st_size)
                except requests.exceptions.RequestException as e:
                    console.print(f"[bold red]❌ 上传 {file_name} 失败: {e}[/bold red]\n响应内容: {e.response.text}")
                    continue

    # 成功后，保存新的完整状态
    new_manifests_state = {"full": new_full_manifest, "slim": new_slim_manifest, "plugins": new_plugins_manifest}
    save_current_state(current_hashes, new_manifests_state)
    console.print(f"\n✅ 状态已更新到 [yellow]{STATE_FILE}[/yellow]")
    console.print("\n[bold green]🎉 全部完成！发布成功！[/bold green]")
    console.print(f"查看你的新 Release: [link={release_info['html_url']}]{release_info['html_url']}[/link]")


if __name__ == "__main__":
    main()
