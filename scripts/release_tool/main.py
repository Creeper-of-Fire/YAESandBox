import os
import sys
from pathlib import Path

from dotenv import load_dotenv

# 将此文件的父目录（release_tool）的父目录（项目根目录）添加到 sys.path
# 这使得 Python 可以找到 `release_tool` 这个包
PROJ_ROOT = Path(__file__).resolve().parent.parent
sys.path.append(str(PROJ_ROOT))

from release_tool import config, ui, utils, components, build, github_api


def main():
    # --- 1. 初始化和环境检查 ---
    load_dotenv()
    token = os.getenv("GH_RELEASE_TOKEN")
    if not token:
        ui.console.print("[bold red]错误: 未找到 GH_RELEASE_TOKEN 环境变量。[/bold red]")
        return
    config.DIST_DIR.mkdir(exist_ok=True)

    # --- 2. 扫描与分析组件变更 ---
    ui.console.print("[bold]Step 1: 扫描文件变更...[/bold]")
    all_components, current_hashes, last_state = components.scan_for_changes()
    changed_components = [c for c in all_components if c.changed]
    if not changed_components:
        ui.console.print("[bold green]✅ 所有组件都是最新的，无需发布。[/bold green]")
        return

    if not ui.Confirm.ask("\n[bold]是否基于以上变更创建新版本？[/bold]", default=True):
        return

    # --- 3. 用户输入与确认 ---
    suggested_version = github_api.get_suggested_version(token)
    ui.console.print("\n[bold]Step 2: 输入版本信息...[/bold]")
    release_info = ui.get_release_info(suggested_version)

    ui.console.print("\n[bold]Step 3: 选择组件...[/bold]")
    components_to_release = ui.select_components_to_release(changed_components)
    if not components_to_release:
        return

    # --- 4. 本地构建与生成 ---
    ui.console.print("\n[bold]Step 4: 正在本地构建所有产物...[/bold]")
    packaged_assets = []
    assets_to_upload = []

    # 只打包那些被选中且有变更的组件
    for comp in components_to_release:
        comp.zip_path = build.package_component(comp)
        comp.hash = utils.get_file_hash(comp.zip_path)
        packaged_assets.append(comp)
        assets_to_upload.append(comp.zip_path)

    ui.console.print("\n[bold]扫描需要独立发布的可执行文件...[/bold]")
    for comp in all_components:  # 注意：这里我们遍历的是 `all_components`，而不是 `components_to_release`
        if comp.publish_exe:
            exe_path = utils.find_unique_exe(comp.path)
            if exe_path:
                # 检查一下，防止因为 launcher 本身也有变更而被重复添加
                if exe_path not in assets_to_upload:
                    ui.console.print(f"➕ [green]添加独立可执行文件:[/green] {exe_path.name}")
                    assets_to_upload.append(exe_path)
                else:
                    ui.console.print(f"ℹ️ [blue]可执行文件已存在于上传列表 (作为组件包的一部分):[/blue] {exe_path.name}")
            else:
                ui.console.print(f"[yellow]警告: 组件 '{comp.name}' 被标记为发布 .exe，但在目录 {comp.path} 中未找到唯一的可执行文件。[/yellow]")

    manifest_paths, new_manifests = build.generate_manifests(
        packaged_assets, release_info, last_state.manifests
    )
    assets_to_upload.extend(manifest_paths)

    # --- 5. 最终确认 ---
    ui.console.print("\n[bold]Step 5: 最终确认...[/bold]")
    if not ui.confirm_final_upload(config.REPO_OWNER, config.REPO_NAME, release_info["version"], assets_to_upload):
        return

    # --- 6. 执行发布 ---
    ui.console.print("\n[bold]Step 6: 执行发布...[/bold]")
    try:
        # 6a. 创建 Release
        gh_release = github_api.create_release(token, release_info)
        upload_url_template = gh_release['upload_url']
        ui.console.print(f"✅ [green]GitHub Release 创建成功:[/green] [link={gh_release['html_url']}]{gh_release['html_url']}[/link]")

        # 6b. 上传文件
        github_api.upload_assets(token, gh_release['id'], upload_url_template, assets_to_upload)

        # 6c. 更新 Release 状态
        if release_info["is_latest"]:
            github_api.update_release_to_latest(token, gh_release['id'])

    except Exception as e:
        ui.console.print(f"[bold red]❌ 发布过程中发生严重错误: {e}[/bold red]")
        ui.console.print("[yellow]警告: 操作可能未完成。请检查 GitHub Releases 页面确认状态。[/yellow]")
        return

    # --- 7. 保存状态 ---
    utils.save_current_state(current_hashes, new_manifests)
    ui.console.print(f"\n✅ [green]状态已更新到[/green] [yellow]{config.STATE_FILE}[/yellow]")
    ui.console.print("\n[bold green]🎉 全部完成！发布成功！[/bold green]")
    ui.console.print(f"查看你的新 Release: [link={gh_release['html_url']}]{gh_release['html_url']}[/link]")


if __name__ == "__main__":
    main()
