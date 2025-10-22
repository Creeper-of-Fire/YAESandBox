from dataclasses import dataclass
from pathlib import Path
from typing import Callable, List, Optional

from release_tool.models import AppState
from . import config, utils, ui
from .models import Hashes


@dataclass
class Component:
    # 从 Definition 继承基础属性
    id: str
    name: str
    path: Path
    manifest_id: str
    manifest_type: List[str]
    filter_func: Optional[Callable[[Path], bool]]
    publish_exe: bool
    is_plugin: bool

    # 运行时状态属性
    changed: bool = False
    zip_path: Path | None = None
    hash: str | None = None

    @classmethod
    def from_definition(cls, definition: 'config.ComponentDefinition', changed: bool) -> 'Component':
        """从静态定义创建一个运行时组件实例"""
        return cls(
            id=definition.id,
            name=definition.name,
            path=definition.path,
            manifest_id=definition.manifest_id,
            manifest_type=definition.manifest_type,
            filter_func=definition.filter_func,
            publish_exe=definition.publish_exe,
            is_plugin=definition.is_plugin,
            changed=changed
        )


def scan_for_changes() -> tuple[list[Component], Hashes, AppState]:
    """扫描所有已定义的组件和插件，检测变更。"""
    last_state = utils.load_last_state()
    last_hashes = last_state.hashes

    all_components: list[Component] = []
    current_hashes = Hashes()

    # 扫描核心组件
    for comp_def in config.CORE_COMPONENTS_DEF:
        current_hash = utils.get_dir_hash(comp_def.path, comp_def.filter_func)
        if current_hash == "not_found":
            ui.console.print(f"[yellow]警告: 组件 '{comp_def.name}' 的源目录不存在，已跳过: {comp_def.path}[/yellow]")
            continue

        current_hashes.core[comp_def.id] = current_hash

        has_changed = (last_hashes.core.get(comp_def.id) != current_hash)
        comp = Component.from_definition(comp_def, has_changed)
        all_components.append(comp)

    # 扫描插件
    current_hashes.plugins = {}
    if config.PLUGINS_SOURCE_DIR.exists():
        for plugin_dir in config.PLUGINS_SOURCE_DIR.iterdir():
            if plugin_dir.is_dir():
                plugin_id = plugin_dir.name
                current_hash = utils.get_dir_hash(plugin_dir)
                current_hashes.plugins[plugin_id] = current_hash

                has_changed = (last_hashes.plugins.get(plugin_id) != current_hash)
                # 为插件动态创建一个临时的 ComponentDefinition 来创建 Component
                plugin_def = config.ComponentDefinition(
                    id=plugin_id,
                    name=f"插件: {plugin_id}",
                    path=plugin_dir,
                    manifest_id=plugin_id,
                    filter_func=utils.plugin_filter,
                    is_plugin=True,
                )
                all_components.append(Component.from_definition(plugin_def, has_changed))

    ui.display_changes_table(all_components)
    return all_components, current_hashes, last_state
