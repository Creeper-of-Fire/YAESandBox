import json
import zipfile
from dataclasses import asdict
from pathlib import Path
from typing import List, Dict, Any

from . import config, ui
from .components import Component
from .models import Manifests, CoreManifest, ManifestEntry


def package_component(component: Component) -> Path:
    """根据 Component 对象打包其源文件。"""
    zip_path = config.DIST_DIR / f"{component.id}.zip"
    ui.console.print(f"📦 [bold]打包组件:[/] [cyan]{component.name}[/cyan] from [yellow]{component.path}[/yellow]")
    with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
        files_to_zip = [p for p in component.path.rglob('*') if p.is_file() and (not component.filter_func or component.filter_func(p))]
        for file in files_to_zip:
            zipf.write(file, file.relative_to(component.path))
    ui.console.print(f"✅ [green]打包完成:[/green] {zip_path.name}")
    return zip_path


def generate_manifests(
        packaged_assets: List[Component],
        release_info: Dict[str, Any],
        last_manifests: Manifests
) -> tuple[list[Path], Manifests]:
    """生成所有清单文件并返回它们的路径列表。"""
    ui.console.print("\n📝 [bold]生成最终清单文件...[/bold]")
    version = release_info["version"]
    notes = release_info["notes"]
    base_download_url = f"https://github.com/{config.REPO_OWNER}/{config.REPO_NAME}/releases/download/{version}"
    assets_to_upload: list[Path] = []

    # --- 核心组件清单 ---
    full_core_map = last_manifests.full.get_component_map()
    slim_core_map = last_manifests.slim.get_component_map()

    core_assets = [a for a in packaged_assets if not a.is_plugin]
    for asset in core_assets:
        description = config.COMPONENT_DESCRIPTIONS.get(asset.manifest_id, "暂无描述。")
        manifest_entry = ManifestEntry(
            id=asset.manifest_id,
            name=asset.name,
            version=version.lstrip('v'),
            notes=notes,
            description=description,
            hash=asset.hash,
            url=f"{base_download_url}/{asset.zip_path.name}"
        )

        if 'full' in asset.manifest_type:
            full_core_map[asset.manifest_id] = manifest_entry
        if 'slim' in asset.manifest_type:
            slim_core_map[asset.manifest_id] = manifest_entry

    # 遍历最终的 core_map，为所有条目（包括未变更的）刷新描述
    for manifest_id, entry in full_core_map.items():
        entry.description = config.COMPONENT_DESCRIPTIONS.get(manifest_id, "暂无描述。")
    for manifest_id, entry in slim_core_map.items():
        entry.description = config.COMPONENT_DESCRIPTIONS.get(manifest_id, "暂无描述。")

    new_full_manifest = CoreManifest(components=list(full_core_map.values()))
    core_manifest_path = config.DIST_DIR / "core_components_manifest.json"
    with open(core_manifest_path, 'w', encoding='utf-8') as f:
        json.dump(asdict(new_full_manifest), f, ensure_ascii=False, indent=4)
    ui.console.print(f"  -> 已生成 [green]{core_manifest_path.name}[/green]")
    assets_to_upload.append(core_manifest_path)

    new_slim_manifest = CoreManifest(components=list(slim_core_map.values()))
    slim_manifest_path = config.DIST_DIR / "core_components_slim_manifest.json"
    with open(slim_manifest_path, 'w', encoding='utf-8') as f:
        json.dump(asdict(new_slim_manifest), f, ensure_ascii=False, indent=4)
    ui.console.print(f"  -> 已生成 [green]{slim_manifest_path.name}[/green]")
    assets_to_upload.append(slim_manifest_path)

    # --- 插件清单 ---
    plugins_map = {p.id: p for p in last_manifests.plugins}
    plugin_assets = [a for a in packaged_assets if a.is_plugin]
    for asset in plugin_assets:
        description = config.COMPONENT_DESCRIPTIONS.get(asset.id, "暂无描述。")
        plugins_map[asset.id] = ManifestEntry(
            id=asset.id,
            name=asset.name.replace("插件: ", ""),
            version=version.lstrip('v'),
            notes=notes,
            description=description,
            hash=asset.hash,
            url=f"{base_download_url}/{asset.zip_path.name}"
        )

    # 遍历最终的 plugin_map，为所有条目（包括未变更的）刷新描述
    for plugin_id, entry in plugins_map.items():
              entry.description = config.COMPONENT_DESCRIPTIONS.get(plugin_id, "暂无描述。")

    new_plugins_list = list(plugins_map.values())
    plugins_manifest_path = config.DIST_DIR / "plugins_manifest.json"
    with open(plugins_manifest_path, 'w', encoding='utf-8') as f:
        json.dump([asdict(p) for p in new_plugins_list], f, ensure_ascii=False, indent=4)
    ui.console.print(f"  -> 已生成 [green]{plugins_manifest_path.name}[/green]")
    assets_to_upload.append(plugins_manifest_path)

    new_manifests_obj = Manifests(
        full=new_full_manifest,
        slim=new_slim_manifest,
        plugins=new_plugins_list
    )

    return assets_to_upload, new_manifests_obj
