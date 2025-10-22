# scripts/list_tree.py

import os
import sys
from pathlib import Path
from typing import TextIO

try:
    import git
except ImportError:
    print("错误: 'GitPython' 库未安装。")
    print("请运行: pip install GitPython")
    sys.exit(1)

# --- 配置 ---
OUTPUT_FILENAME = "project_tree.txt"


def find_git_root(path: Path) -> Path | None:
    """从给定路径向上查找 .git 目录，确定项目根目录。"""
    current_path = path.resolve()
    while current_path != current_path.parent:
        if (current_path / ".git").is_dir():
            return current_path
        current_path = current_path.parent
    return None


def generate_tree(start_path: Path, repo: git.Repo, output_file: TextIO):
    """
    遍历目录并生成文件树，将结果写入提供的文件对象。
    """
    output_file.write(f"项目结构: {start_path.name}\n")

    for root, dirs, files in os.walk(start_path, topdown=True):
        current_dir = Path(root)

        # 1. 强制排除 .git 目录
        if '.git' in dirs:
            dirs.remove('.git')

        # 2. 准备要检查的路径列表 (相对于仓库根目录)
        paths_to_check = [
                             (current_dir / d).relative_to(start_path) for d in dirs
                         ] + [
                             (current_dir / f).relative_to(start_path) for f in files
                         ]

        paths_to_check_str = [str(p) for p in paths_to_check]

        # 3. 使用 gitpython 一次性找出所有被忽略的路径
        if not paths_to_check_str:
            ignored_set = set()
        else:
            ignored_set = set(repo.ignored(*paths_to_check_str))

        # 4. 过滤目录和文件
        dirs[:] = sorted([
            d for d in dirs
            if str((current_dir / d).relative_to(start_path)) not in ignored_set
        ])

        files[:] = sorted([
            f for f in files
            if str((current_dir / f).relative_to(start_path)) not in ignored_set
        ])

        # 5. 计算缩进并写入文件
        level = len(current_dir.relative_to(start_path).parts)
        indent = '│   ' * level

        entries = [d + '/' for d in dirs] + files

        for i, name in enumerate(entries):
            connector = '└── ' if i == len(entries) - 1 else '├── '
            output_file.write(f"{indent}{connector}{name}\n")


if __name__ == "__main__":
    # 确定脚本所在位置并找到项目根目录
    script_path = Path(__file__).parent
    project_root = find_git_root(script_path)

    if not project_root:
        print(f"错误: 未能在 '{script_path}' 或其任何父目录中找到 '.git' 文件夹。")
        print("请确保此脚本在一个 Git 仓库中运行。")
        sys.exit(1)

    # 定义输出文件的完整路径
    output_filepath = project_root / OUTPUT_FILENAME

    try:
        # 初始化 Git 仓库对象
        repository = git.Repo(project_root)

        # 使用 'with' 语句安全地打开和写入文件
        print(f"正在生成文件结构树...")
        with open(output_filepath, 'w', encoding='utf-8') as f:
            generate_tree(project_root, repository, f)

        print(f"✅ 文件结构树已成功写入到: {output_filepath}")

    except git.exc.InvalidGitRepositoryError:
        print(f"错误: '{project_root}' 不是一个有效的 Git 仓库。")
        sys.exit(1)
    except Exception as e:
        print(f"发生了一个意外错误: {e}")
        sys.exit(1)