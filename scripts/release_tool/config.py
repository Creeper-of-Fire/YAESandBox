# release_tool/config.py
from pathlib import Path

# --- 配置区 ---
REPO_OWNER = "Creeper-of-Fire"
REPO_NAME = "YAESandBox"

# 路径配置
SOLUTION_ROOT = Path(__file__).parent.resolve().parent.parent
BUILD_DIR = SOLUTION_ROOT / 'build'
DIST_DIR = BUILD_DIR / 'dist'

# 源目录
LAUNCHER_SOURCE_DIR = BUILD_DIR / 'launcher'
FRONTEND_SOURCE_DIR = BUILD_DIR / 'frontend'
BACKEND_SOURCE_DIR = BUILD_DIR / 'backend'
BACKEND_SLIM_SOURCE_DIR = BUILD_DIR / 'backend-slim'
PLUGINS_SOURCE_DIR = BUILD_DIR / 'Plugins'

# 状态文件
STATE_FILE = DIST_DIR / '.release_state.json'
# --- 配置区结束 ---

from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, List, Optional

from . import utils


@dataclass(frozen=True)
class ComponentDefinition:
    """组件的静态配置定义"""
    id: str
    name: str
    path: Path
    manifest_id: str
    manifest_type: List[str] = field(default_factory=list)
    filter_func: Optional[Callable[[Path], bool]] = None
    publish_exe: bool = False
    is_plugin: bool = False  # 插件可以动态扫描，但定义一个模板也无妨


# 组件定义
CORE_COMPONENTS_DEF: list[ComponentDefinition] = [
    ComponentDefinition(
        id="launcher", name="启动器", path=LAUNCHER_SOURCE_DIR,
        publish_exe=True, manifest_type=["full", "slim"],
        manifest_id="launcher"
    ),
    ComponentDefinition(
        id="app", name="前端应用", path=FRONTEND_SOURCE_DIR,
        manifest_type=["full", "slim"],
        manifest_id="app"
    ),
    ComponentDefinition(
        id="backend", name=".NET 后端 (完整版)", path=BACKEND_SOURCE_DIR,
        filter_func=utils.backend_and_slim_filter,
        manifest_type=["full"],
        manifest_id="backend"
    ),
    ComponentDefinition(
        id="backend-slim", name=".NET 后端 (精简版)", path=BACKEND_SLIM_SOURCE_DIR,
        filter_func=utils.backend_and_slim_filter,
        manifest_type=["slim"],
        manifest_id="backend"
    ),
]

# 组件描述映射表
COMPONENT_DESCRIPTIONS = {
    # 核心组件 (使用 manifest_id)
    "launcher": "应用程序启动器，负责检查更新和启动核心应用。",
    "app": "YAESandBox 的前端用户界面。",
    "backend": ".NET 后端。",

    # 插件 (使用插件目录名，即 id)
    "YAESandBox.Plugin.FileSystemConfigProvider": "获得内置/官方的配置插件。",
    "YAESandBox.Plugin.LuaScript": "Lua 脚本插件。",
    "YAESandBox.Plugin.TextParser": "文本解析器插件。",
}
