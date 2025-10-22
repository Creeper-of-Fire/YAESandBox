import os
import threading
import time
from pathlib import Path

import requests
from rich.progress import Progress

from . import config, utils, ui


class UploadProgressReader:
    """包装文件对象，用于在requests上传时实时更新rich进度条。"""

    def __init__(self, fp, progress: Progress, task_id):
        self.fp = fp
        self.progress = progress
        self.task_id = task_id
        self.total_size = os.fstat(fp.fileno()).st_size
        self.progress.update(self.task_id, total=self.total_size)

    def read(self, size=-1):
        chunk = self.fp.read(size)
        if chunk:
            self.progress.advance(self.task_id, len(chunk))
        return chunk

    def __len__(self):
        return self.total_size


def get_suggested_version(token: str) -> str | None:
    """从 GitHub 获取最新的 release tag 并建议下一个版本号"""
    ui.console.print("🔍 [bold]正在获取上一个版本号...[/bold]")
    headers = {"Authorization": f"token {token}", "Accept": "application/vnd.github.v3+json"}
    url = f"https://api.github.com/repos/{config.REPO_OWNER}/{config.REPO_NAME}/releases/latest"
    try:
        response = requests.get(url, headers=headers, timeout=5)
        if response.status_code == 404:
            ui.console.print("  -> 未找到上一个版本，请手动输入。")
            return None
        response.raise_for_status()
        latest_release = response.json()
        last_tag = latest_release.get("tag_name")
        if last_tag:
            suggested_tag = utils.increment_version_tag(last_tag)
            if suggested_tag:
                ui.console.print(f"  -> [green]建议版本号:[/green] {suggested_tag} (基于上个版本 [cyan]{last_tag}[/cyan])")
                return suggested_tag
            else:
                ui.console.print(f"  -> 上一个版本号 '{last_tag}' 格式无法自动递增。")
        return None
    except requests.exceptions.RequestException as e:
        ui.console.print(f"[yellow]警告: 获取最新版本失败: {e}[/yellow]")
        return None


def create_release(token: str, release_info: dict) -> dict:
    release_data = {
        "tag_name": release_info["version"],
        "name": release_info["title"],
        "body": release_info["notes"],
        "draft": False,
        "prerelease": False,
        "make_latest": "false"  # 先不设为 latest，全部上传完再设置
    }
    headers = {"Authorization": f"token {token}", "Accept": "application/vnd.github.v3+json"}
    url = f"https://api.github.com/repos/{config.REPO_OWNER}/{config.REPO_NAME}/releases"
    response = requests.post(url, headers=headers, json=release_data)
    response.raise_for_status()
    return response.json()


def _upload_worker(url: str, headers: dict, data: UploadProgressReader, result: dict):
    try:
        response = requests.post(url, headers=headers, data=data)
        response.raise_for_status()
        result['response'] = response
        result['exception'] = None
    except requests.exceptions.RequestException as e:
        result['response'] = None
        result['exception'] = e


from rich.progress import Progress, BarColumn, DownloadColumn, TransferSpeedColumn, TimeRemainingColumn


def upload_assets(token: str, release_id: int, upload_url_template: str, assets_to_upload: list[Path]):
    with Progress(BarColumn(), DownloadColumn(), TransferSpeedColumn(), TimeRemainingColumn(), console=ui.console) as progress:
        upload_task = progress.add_task("[bold]上传文件中...", total=len(assets_to_upload))
        for file_path in assets_to_upload:
            file_name, file_size = file_path.name, file_path.stat().st_size
            item_task = progress.add_task(f"[cyan]{file_name}", total=file_size)

            content_type = {
                ".zip": "application/zip", ".exe": "application/vnd.microsoft.portable-executable",
                ".json": "application/json"
            }.get(file_path.suffix, "application/octet-stream")
            headers_upload = {"Authorization": f"token {token}", "Content-Type": content_type}
            upload_url = upload_url_template.split('{')[0] + f"?name={file_name}"

            with open(file_path, 'rb') as f:
                progress_reader = UploadProgressReader(f, progress, item_task)
                result = {}
                thread = threading.Thread(target=_upload_worker, args=(upload_url, headers_upload, progress_reader, result))
                thread.start()

                while thread.is_alive():
                    time.sleep(0.1)

                upload_exception = result.get('exception')
                if upload_exception:
                    progress.stop()
                    e = upload_exception
                    err_text = e.response.text if e.response else 'N/A'
                    raise Exception(f"上传 {file_name} 失败: {e}\n响应内容: {err_text}")

            progress.advance(upload_task)
    ui.console.print(f"✅ [green]所有文件上传成功！[/green]")


def update_release_to_latest(token: str, release_id: int):
    ui.console.print("\n➡️  [bold]正在将 Release 更新为 'latest'...[/bold]")
    headers = {"Authorization": f"token {token}", "Accept": "application/vnd.github.v3+json"}
    update_data = {"make_latest": "true"}
    update_url = f"https://api.github.com/repos/{config.REPO_OWNER}/{config.REPO_NAME}/releases/{release_id}"
    response = requests.patch(update_url, headers=headers, json=update_data)
    response.raise_for_status()
    ui.console.print(f"✅ [green]Release 已成功标记为 'latest'！[/green]")
