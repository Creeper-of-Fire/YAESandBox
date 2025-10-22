import hashlib
import json
from pathlib import Path

from . import ui, config
from .models import Manifests, AppState, Hashes


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


def load_last_state() -> AppState:
    """加载状态文件并解析为 AppState 对象"""
    if not config.STATE_FILE.exists():
        return AppState()  # 返回一个默认的、空的 AppState 对象
    with open(config.STATE_FILE, 'r', encoding='utf-8') as f:
        data = json.load(f)
        return AppState.from_dict(data)  # 使用顶层 from_dict 解析


def save_current_state(hashes: Hashes, manifests: Manifests):
    """根据 hashes 和 manifests 创建 AppState 对象并保存"""
    config.STATE_FILE.parent.mkdir(exist_ok=True)
    state = AppState(hashes=hashes, manifests=manifests)
    with open(config.STATE_FILE, 'w', encoding='utf-8') as f:
        json.dump(state.to_dict(), f, indent=4, ensure_ascii=False)  # 使用顶层 to_dict 序列化


def backend_and_slim_filter(path: Path) -> bool:
    """过滤器：只保留 .exe 和 appsettings.json。"""
    name_lower = path.name.lower()
    return name_lower.endswith('.exe') or name_lower == 'appsettings.json'


def plugin_filter(path: Path) -> bool:
    """过滤器：排除 .pdb 文件。"""
    return not path.name.lower().endswith('.pdb')


def find_unique_exe(directory: Path) -> Path | None:
    exes = list(directory.glob('*.exe'))
    if len(exes) == 1: return exes[0]
    if len(exes) > 1: ui.console.print(f"[yellow]警告: 在 {directory} 发现多个.exe, 无法确定独立可执行文件。[/yellow]")
    return None


def increment_version_tag(tag: str) -> str | None:
    """尝试将版本号标签的最后一部分加一 (e.g., v1.2.3 -> v1.2.4)"""
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
