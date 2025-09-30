# release_helper.py
import hashlib
import json
import os
import threading
import time
import zipfile
from pathlib import Path

import requests
from dotenv import load_dotenv
from rich.console import Console
from rich.progress import Progress, BarColumn, DownloadColumn, TransferSpeedColumn, TimeRemainingColumn
from rich.prompt import Prompt, Confirm
from rich.table import Table

# --- 配置区 ---
REPO_OWNER = "Creeper-of-Fire"
REPO_NAME = "YAESandBox"
SOLUTION_ROOT = Path(__file__).parent.resolve().parent
BUILD_DIR = SOLUTION_ROOT / 'build'
LAUNCHER_SOURCE_DIR = BUILD_DIR / 'launcher'
FRONTEND_SOURCE_DIR = BUILD_DIR / 'frontend'
BACKEND_SOURCE_DIR = BUILD_DIR / 'backend'
BACKEND_SLIM_SOURCE_DIR = BUILD_DIR / 'backend-slim'
PLUGINS_SOURCE_DIR = BUILD_DIR / 'Plugins'
DIST_DIR = SOLUTION_ROOT / 'build' / 'dist'
STATE_FILE = DIST_DIR / '.release_state.json'
# --- 配置区结束 ---

console = Console()


def get_dir_hash(directory: Path, filter_func=None) -> str:
    sha256 = hashlib.sha256()
    if not directory.exists(): return "not_found"
    files = sorted([p for p in directory.rglob('*') if p.is_file() and (not filter_func or filter_func(p))], key=lambda p: p.relative_to(directory))
    if not files: return "empty"
    for file_path in files:
        sha256.update(str(file_path.relative_to(directory)).encode())
        with open(file_path, 'rb') as f:
            while chunk := f.read(8192): sha256.update(chunk)
    return sha256.hexdigest()


def get_file_hash(file_path: Path) -> str:
    sha256 = hashlib.sha256()
    with open(file_path, 'rb') as f:
        while chunk := f.read(8192): sha256.update(chunk)
    return sha256.hexdigest()


def load_last_state():
    if not STATE_FILE.exists(): return {"hashes": {}, "manifests": {}}
    with open(STATE_FILE, 'r', encoding='utf-8') as f: return json.load(f)


def save_current_state(hashes, manifests):
    STATE_FILE.parent.mkdir(exist_ok=True)
    state = {"hashes": hashes, "manifests": manifests}
    with open(STATE_FILE, 'w', encoding='utf-8') as f: json.dump(state, f, indent=4, ensure_ascii=False)


def package_component(name: str, src_dir: Path, filter_func=None) -> Path:
    zip_path = DIST_DIR / f"{name}.zip"
    console.print(f"📦 [bold]打包组件:[/] [cyan]{name}[/cyan] from [yellow]{src_dir}[/yellow]")
    with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
        files_to_zip = [p for p in src_dir.rglob('*') if p.is_file() and (not filter_func or filter_func(p))]
        for file in files_to_zip:
            zipf.write(file, file.relative_to(src_dir))
    console.print(f"✅ [green]打包完成:[/green] {zip_path.name}")
    return zip_path


def backend_filter(path: Path) -> bool:
    return path.name.lower().endswith('.exe') or path.name.lower() == 'appsettings.json'

def plugin_filter(path: Path) -> bool:
    return not path.name.lower().endswith('.pdb')


def find_unique_exe(directory: Path) -> Path | None:
    exes = list(directory.glob('*.exe'))
    if len(exes) == 1: return exes[0]
    if len(exes) > 1: console.print(f"[yellow]警告: 在 {directory} 发现多个.exe, 无法确定独立可执行文件。[/yellow]")
    return None


def increment_version_tag(tag: str) -> str | None:
    """尝试将版本号标签的最后一部分加一 (e.g., v1.2.3 -> v1.2.4)"""
    original_tag = tag
    prefix = ""
    if tag.startswith('v'):
        prefix = 'v'
        tag = tag[1:]

    parts = tag.split('.')
    if not parts:
        return None

    try:
        last_part_num = int(parts[-1])
        parts[-1] = str(last_part_num + 1)
        return prefix + ".".join(parts)
    except (ValueError, IndexError):
        # 如果最后一部分不是数字，则无法自动递增
        return None


def get_suggested_version(token: str) -> str | None:
    """从 GitHub 获取最新的 release tag 并建议下一个版本号"""
    console.print("🔍 [bold]正在获取上一个版本号...[/bold]")
    headers = {"Authorization": f"token {token}", "Accept": "application/vnd.github.v3+json"}
    url = f"https://api.github.com/repos/{REPO_OWNER}/{REPO_NAME}/releases/latest"
    try:
        response = requests.get(url, headers=headers, timeout=5)
        if response.status_code == 404:
            console.print("  -> 未找到上一个版本，请手动输入。")
            return None
        response.raise_for_status()
        latest_release = response.json()
        last_tag = latest_release.get("tag_name")
        if last_tag:
            suggested_tag = increment_version_tag(last_tag)
            if suggested_tag:
                console.print(f"  -> [green]建议版本号:[/green] {suggested_tag} (基于上个版本 [cyan]{last_tag}[/cyan])")
                return suggested_tag
            else:
                console.print(f"  -> 上一个版本号 '{last_tag}' 格式无法自动递增。")
        return None
    except requests.exceptions.RequestException as e:
        console.print(f"[yellow]警告: 获取最新版本失败: {e}[/yellow]")
        return None


class UploadProgressReader:
    """
    一个包装文件对象的类，用于在requests上传时实时更新rich进度条。
    """

    def __init__(self, fp, progress: Progress, task_id):
        self.fp = fp
        self.progress = progress
        self.task_id = task_id
        self.total_size = os.fstat(fp.fileno()).st_size
        # 确保任务的总大小已设置
        self.progress.update(self.task_id, total=self.total_size)

    def read(self, size=-1):
        chunk = self.fp.read(size)
        if chunk:
            # 读取到数据块时，推进进度条
            self.progress.advance(self.task_id, len(chunk))
        return chunk

    def __len__(self):
        return self.total_size


def upload_worker(url: str, headers: dict, data: UploadProgressReader, result: dict):
    """
    执行文件上传的函数，设计为在单独的线程中运行。
    """
    try:
        response = requests.post(url, headers=headers, data=data)
        response.raise_for_status()
        result['response'] = response
        result['exception'] = None
    except requests.exceptions.RequestException as e:
        result['response'] = None
        result['exception'] = e


def main():
    # --- 1. 初始化和环境检查 ---
    load_dotenv()
    token = os.getenv("GH_RELEASE_TOKEN")
    if not token:
        console.print("[bold red]错误: 未找到 GH_RELEASE_TOKEN 环境变量。[/bold red]")
        return
    DIST_DIR.mkdir(exist_ok=True)
    last_state = load_last_state()
    last_hashes = last_state.get("hashes", {})
    current_hashes = {}
    components = []

    # --- 2. 扫描与分析组件变更 ---
    console.print("[bold]Step 1: 扫描文件变更...[/bold]")
    # 核心组件定义
    core_components_def = [
        {"id": "launcher", "name": "启动器", "path": LAUNCHER_SOURCE_DIR, "publish_exe": True, "manifest_type": ["full", "slim"]},
        {"id": "app", "name": "前端应用", "path": FRONTEND_SOURCE_DIR, "manifest_type": ["full", "slim"]},
        {"id": "backend", "name": ".NET 后端 (完整版)", "path": BACKEND_SOURCE_DIR, "filter": backend_filter, "manifest_type": ["full"]},
        {"id": "backend-slim", "name": ".NET 后端 (精简版)", "path": BACKEND_SLIM_SOURCE_DIR, "filter": backend_filter, "manifest_type": ["slim"]},
    ]
    for comp_def in core_components_def:
        current_hash = get_dir_hash(comp_def['path'], comp_def.get('filter'))
        if current_hash == "not_found":
            console.print(f"[yellow]警告: 组件 '{comp_def['name']}' 的源目录不存在，已跳过: {comp_def['path']}[/yellow]")
            continue
        current_hashes[comp_def['id']] = current_hash
        components.append({**comp_def, "changed": last_hashes.get(comp_def['id']) != current_hash, "is_plugin": False})

    # 插件定义
    current_hashes['plugins'] = {}
    if PLUGINS_SOURCE_DIR.exists():
        for plugin_dir in PLUGINS_SOURCE_DIR.iterdir():
            if plugin_dir.is_dir():
                plugin_id = plugin_dir.name
                current_hash = get_dir_hash(plugin_dir)
                current_hashes['plugins'][plugin_id] = current_hash
                last_plugin_hashes = last_hashes.get('plugins', {})
                components.append({
                    "id": plugin_id, "name": f"插件: {plugin_id}", "path": plugin_dir, "filter": plugin_filter,
                    "changed": last_plugin_hashes.get(plugin_id) != current_hash, "is_plugin": True
                })

    changed_components = [c for c in components if c['changed']]
    if not changed_components:
        console.print("[bold green]✅ 所有组件都是最新的，无需发布。[/bold green]")
        return

    table = Table(title="🔍 检测到文件变更")
    table.add_column("组件", style="cyan")
    table.add_column("ID", style="magenta")
    table.add_column("状态", style="green")
    for comp in components:
        table.add_row(comp['name'], comp['id'], "[bold yellow]有变更[/bold yellow]" if comp['changed'] else "无变更")
    console.print(table)

    # --- 3. 用户输入与确认 ---
    if not Confirm.ask("\n[bold]是否基于以上变更创建新版本？[/bold]", default=True): return

    console.print("\n[bold]Step 2: 请输入新版本信息...[/bold]")
    suggested_version = get_suggested_version(token)
    version = Prompt.ask("版本号 (e.g., v1.2.3)", default=suggested_version)
    release_title = Prompt.ask("发布标题", default=f"Release {version}")
    console.print("更新日志 (输入 'EOF' 结束):")
    release_notes_lines = []
    while True:
        line = input()
        if line == 'EOF': break
        release_notes_lines.append(line)
    release_notes = "\n".join(release_notes_lines)

    is_latest = Confirm.ask("\n[bold]将此版本标记为 'latest' (最新) 版本吗？[/bold]", default=True)

    console.print("\n[bold]Step 3: 请选择要发布的组件...[/bold]")
    to_release = [c for c in changed_components if Confirm.ask(f"发布 [cyan]{c['name']}[/cyan]?", default=True)]
    if not to_release:
        console.print("未选择任何组件，操作取消。")
        return

    # --- 4. 本地构建与生成 (原子事务) ---
    console.print("\n[bold]Step 4: 正在本地构建所有产物...[/bold]")
    packaged_assets = []
    assets_to_upload = []

    # 4a. 打包组件
    for comp in to_release:
        zip_path = package_component(comp['id'], comp['path'], comp.get('filter'))
        file_hash = get_file_hash(zip_path)
        packaged_assets.append({**comp, "zip_path": zip_path, "hash": file_hash})
        assets_to_upload.append(zip_path)
        if comp.get('publish_exe'):
            exe_path = find_unique_exe(comp['path'])
            if exe_path:
                console.print(f"➕ [green]添加独立可执行文件:[/green] {exe_path.name}")
                assets_to_upload.append(exe_path)

    # 4b. 生成清单
    console.print("\n📝 [bold]生成最终清单文件...[/bold]")
    base_download_url = f"https://github.com/{REPO_OWNER}/{REPO_NAME}/releases/download/{version}"
    last_manifests = last_state.get("manifests", {})

    # 核心组件清单
    last_full_manifest = last_manifests.get("full", {})
    last_slim_manifest = last_manifests.get("slim", {})
    full_core_map = {item['id']: item for item in last_full_manifest.get("components", [])}
    slim_core_map = {item['id']: item for item in last_slim_manifest.get("components", [])}

    core_assets = [a for a in packaged_assets if not a['is_plugin']]
    for asset in core_assets:
        manifest_entry = {
            "name": asset['name'], "version": version.lstrip('v'), "notes": release_notes, "hash": asset['hash'],
            "url": f"{base_download_url}/{asset['zip_path'].name}"
        }
        if 'full' in asset['manifest_type']:
            full_core_map[asset['id']] = {"id": asset['id'], **manifest_entry}
        if 'slim' in asset['manifest_type']:
            target_id = 'backend' if asset['id'] == 'backend-slim' else asset['id']
            slim_core_map[target_id] = {"id": target_id, **manifest_entry}

    new_full_manifest = {"components": list(full_core_map.values())}
    core_manifest_path = DIST_DIR / "core_components_manifest.json"
    with open(core_manifest_path, 'w', encoding='utf-8') as f:
        json.dump(new_full_manifest, f, ensure_ascii=False, indent=4)
    console.print(f"  -> 已生成 [green]{core_manifest_path.name}[/green]")
    assets_to_upload.append(core_manifest_path)

    new_slim_manifest = {"components": list(slim_core_map.values())}
    slim_manifest_path = DIST_DIR / "core_components_slim_manifest.json"
    with open(slim_manifest_path, 'w', encoding='utf-8') as f:
        json.dump(new_slim_manifest, f, ensure_ascii=False, indent=4)
    console.print(f"  -> 已生成 [green]{slim_manifest_path.name}[/green]")
    assets_to_upload.append(slim_manifest_path)

    # 插件清单
    last_plugins_manifest = last_manifests.get("plugins", [])
    plugins_map = {item['id']: item for item in last_plugins_manifest}
    plugin_assets = [a for a in packaged_assets if a['is_plugin']]
    for asset in plugin_assets:
        plugins_map[asset['id']] = {
            "id": asset['id'], "name": asset['name'].replace("插件: ", ""), "version": version.lstrip('v'),
            "description": f"发布于 {version}:\n{release_notes}", "hash": asset['hash'],
            "url": f"{base_download_url}/{asset['zip_path'].name}"
        }
    new_plugins_manifest = list(plugins_map.values())
    plugins_manifest_path = DIST_DIR / "plugins_manifest.json"
    with open(plugins_manifest_path, 'w', encoding='utf-8') as f:
        json.dump(new_plugins_manifest, f, ensure_ascii=False, indent=4)
    console.print(f"  -> 已生成 [green]{plugins_manifest_path.name}[/green]")
    assets_to_upload.append(plugins_manifest_path)

    # --- 5. 最终确认 ---
    console.print("\n[bold]Step 5: 最终确认...[/bold]")
    console.print(f"将在 [cyan]{REPO_OWNER}/{REPO_NAME}[/cyan] 创建 Release [green]{version}[/green]")
    console.print("[bold]将上传以下文件:[/bold]")
    for asset_path in assets_to_upload:
        console.print(f"  - {asset_path.name}")

    if not Confirm.ask("\n[bold red]确认执行发布？此操作不可逆！[/bold red]", default=False):
        console.print("操作已取消。")
        return

    # --- 6. 执行发布 ---
    console.print("\n[bold]Step 6: 执行发布...[/bold]")

    # 6a. 创建 Release (注意：此时不设为 latest)
    try:
        release_data = {
            "tag_name": version,
            "name": release_title,
            "body": release_notes,
            "draft": False,
            "prerelease": False,
            "make_latest": "false"
        }
        headers = {"Authorization": f"token {token}", "Accept": "application/vnd.github.v3+json"}
        response = requests.post(f"https://api.github.com/repos/{REPO_OWNER}/{REPO_NAME}/releases", headers=headers, json=release_data)
        response.raise_for_status()
        release_info = response.json()
        upload_url_template = release_info['upload_url']
        console.print(f"✅ [green]GitHub Release 创建成功:[/green] [link={release_info['html_url']}]{release_info['html_url']}[/link]")
    except requests.exceptions.RequestException as e:
        console.print(f"[bold red]❌ 创建 GitHub Release 失败: {e}\n响应内容: {e.response.text}[/bold red]")
        return

    # 6b. 上传所有文件
    with Progress(BarColumn(), DownloadColumn(), TransferSpeedColumn(), TimeRemainingColumn(), console=console) as progress:
        upload_task = progress.add_task("[bold]上传文件中...", total=len(assets_to_upload))
        for file_path in assets_to_upload:
            file_name, file_size = file_path.name, file_path.stat().st_size
            item_task = progress.add_task(f"[cyan]{file_name}", total=file_size)

            content_type = {".zip": "application/zip", ".exe": "application/vnd.microsoft.portable-executable", ".json": "application/json"}.get(
                file_path.suffix, "application/octet-stream")
            headers_upload = {"Authorization": f"token {token}", "Content-Type": content_type}
            upload_url = upload_url_template.split('{')[0] + f"?name={file_name}"

            with open(file_path, 'rb') as f:
                progress_reader = UploadProgressReader(f, progress, item_task)

                # --- 使用线程执行上传 ---
                result = {}
                thread = threading.Thread(target=upload_worker, args=(upload_url, headers_upload, progress_reader, result))
                thread.start()

                # 主线程等待，同时允许rich更新UI
                while thread.is_alive():
                    time.sleep(0.1)

                # 检查线程执行结果
                upload_exception = result.get('exception')
                if upload_exception:
                    progress.stop()
                    e = upload_exception
                    console.print(f"[bold red]❌ 上传 {file_name} 失败: {e}\n响应内容: {e.response.text if e.response else 'N/A'}[/bold red]")
                    return

                upload_response = result['response']
                asset_info = upload_response.json()

                # 更新清单中的 URL
                file_url = asset_info['browser_download_url']
                if file_path == core_manifest_path:
                    for comp in new_full_manifest["components"]:
                        comp_zip_name = f"{comp['id']}.zip"
                        if any(p['zip_path'].name == comp_zip_name for p in packaged_assets):
                            comp['url'] = f"{release_info['html_url'].replace('tag', 'download')}/{comp_zip_name}"
                elif file_path == slim_manifest_path:
                    for comp in new_slim_manifest["components"]:
                        comp_id_source = 'backend-slim' if comp['id'] == 'backend' else comp['id']
                        comp_zip_name = f"{comp_id_source}.zip"
                        if any(p['zip_path'].name == comp_zip_name for p in packaged_assets):
                            comp['url'] = f"{release_info['html_url'].replace('tag', 'download')}/{comp_zip_name}"
                elif file_path == plugins_manifest_path:
                    for comp in new_plugins_manifest:
                        comp_zip_name = f"{comp['id']}.zip"
                        if any(p['zip_path'].name == comp_zip_name for p in packaged_assets):
                            comp['url'] = f"{release_info['html_url'].replace('tag', 'download')}/{comp_zip_name}"

                progress.advance(upload_task)

    console.print(f"✅ [green]所有文件上传成功！[/green]")

    # 6c. 更新 Release 状态 (如果需要)
    if is_latest:
        console.print("\n➡️  [bold]正在将 Release 更新为 'latest'...[/bold]")
        try:
            update_data = {"make_latest": "true"}
            release_id = release_info['id']
            update_url = f"https://api.github.com/repos/{REPO_OWNER}/{REPO_NAME}/releases/{release_id}"
            response = requests.patch(update_url, headers=headers, json=update_data)
            response.raise_for_status()
            console.print(f"✅ [green]Release 已成功标记为 'latest'！[/green]")
        except requests.exceptions.RequestException as e:
            console.print(f"[bold red]❌ 更新 Release 状态失败: {e}\n响应内容: {e.response.text}[/bold red]")
            console.print("[yellow]警告: 文件已全部上传，但 Release 未能标记为 'latest'。您可能需要手动去 GitHub 页面设置。[/yellow]")

    # --- 7. 保存状态 ---
    new_manifests_state = {"full": new_full_manifest, "slim": new_slim_manifest, "plugins": new_plugins_manifest}
    save_current_state(current_hashes, new_manifests_state)
    console.print(f"\n✅ [green]状态已更新到[/green] [yellow]{STATE_FILE}[/yellow]")
    console.print("\n[bold green]🎉 全部完成！发布成功！[/bold green]")
    console.print(f"查看你的新 Release: [link={release_info['html_url']}]{release_info['html_url']}[/link]")


if __name__ == "__main__":
    main()
