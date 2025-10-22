from typing import List

from rich.console import Console
from rich.prompt import Prompt, Confirm
from rich.table import Table

from .components import Component

console = Console()


def display_changes_table(components: List[Component]):
    table = Table(title="🔍 检测到文件变更")
    table.add_column("组件", style="cyan")
    table.add_column("ID", style="magenta")
    table.add_column("状态", style="green")
    for comp in components:
        table.add_row(comp.name, comp.id, "[bold yellow]有变更[/bold yellow]" if comp.changed else "无变更")
    console.print(table)


def get_release_info(suggested_version: str | None) -> dict:
    console.print("\n[bold]请输入新版本信息...[/bold]")
    version = Prompt.ask("版本号 (e.g., v1.2.3)", default=suggested_version)
    release_title = Prompt.ask("发布标题", default=f"Release {version}")

    console.print("更新日志 (输入 'EOF' 结束):")
    release_notes_lines = []
    while True:
        try:
            line = input()
            if line == 'EOF': break
            release_notes_lines.append(line)
        except EOFError:
            break
    release_notes = "\n".join(release_notes_lines)

    is_latest = Confirm.ask("\n[bold]将此版本标记为 'latest' (最新) 版本吗？[/bold]", default=True)

    return {
        "version": version,
        "title": release_title,
        "notes": release_notes,
        "is_latest": is_latest
    }


def select_components_to_release(changed_components: List[Component]) -> List[Component]:
    console.print("\n[bold]请选择要发布的组件...[/bold]")
    to_release = [c for c in changed_components if Confirm.ask(f"发布 [cyan]{c.name}[/cyan]?", default=True)]
    if not to_release:
        console.print("未选择任何组件，操作取消。")
        return []
    return to_release


def confirm_final_upload(repo_owner, repo_name, version, assets_to_upload):
    console.print(f"将在 [cyan]{repo_owner}/{repo_name}[/cyan] 创建 Release [green]{version}[/green]")
    console.print("[bold]将上传以下文件:[/bold]")
    for asset_path in assets_to_upload:
        console.print(f"  - {asset_path.name}")

    if not Confirm.ask("\n[bold red]确认执行发布？此操作不可逆！[/bold red]", default=False):
        console.print("操作已取消。")
        return False
    return True
