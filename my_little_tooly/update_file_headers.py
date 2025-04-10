# my_little_tooly/update_file_headers.py
import os
import sys
import subprocess
import argparse
import logging
import re
from pathlib import Path
from typing import List, Tuple, Optional, Set  # <--- Added Set

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    stream=sys.stdout
)

HEADER_COMMENT_REGEX = re.compile(r"^#\s*([\w/\\.-]+\.py)\s*$")


# --- 新增：查找 Git 根目录函数 ---
def find_git_root() -> Optional[Path]:
    """使用 'git rev-parse --show-toplevel' 查找 Git 仓库根目录。"""
    try:
        logging.debug("尝试使用 'git rev-parse --show-toplevel' 查找 Git 根目录...")
        # 运行命令时不需要指定 cwd，它会从当前目录开始查找
        result = subprocess.run(
            ["git", "rev-parse", "--show-toplevel"],
            capture_output=True,
            text=True,
            check=True,
            encoding='utf-8'
        )
        root_path_str = result.stdout.strip()
        if not root_path_str:
            logging.error("'git rev-parse --show-toplevel' 没有输出路径。")
            return None

        root_path = Path(root_path_str).resolve()  # 获取绝对路径
        logging.info(f"找到 Git 根目录: {root_path}")
        return root_path
    except FileNotFoundError:
        logging.error("错误：未找到 'git' 命令。请确保 Git 已安装并位于系统 PATH 中。")
        return None
    except subprocess.CalledProcessError:
        # 这通常意味着当前目录或其任何父目录都不是 Git 仓库
        logging.error("错误：'git rev-parse --show-toplevel' 执行失败。请确保你在 Git 仓库内部运行此脚本。")
        return None
    except Exception as e:
        logging.error(f"查找 Git 根目录时发生未知错误: {e}", exc_info=True)
        return None


# --- 修改：Git 相关函数，接收 root_dir 参数 ---
def get_git_tracked_files(root_dir: Path) -> Optional[Set[Path]]:
    """
    使用 git ls-files 获取所有被 Git 跟踪的文件路径 (相对于 root_dir)。
    尊重 .gitignore 规则。
    在指定的 root_dir 中运行。
    返回一个包含 Path 对象的集合，如果 Git 命令失败则返回 None。
    """
    logging.info(f"在 '{root_dir}' 中使用 'git ls-files' 获取 Git 跟踪的文件列表...")
    try:
        result = subprocess.run(
            ["git", "ls-files", "--cached", "--others", "--exclude-standard"],
            cwd=root_dir,  # <--- 明确在找到的根目录运行
            capture_output=True,
            text=True,
            check=True,
            encoding='utf-8'
        )
        tracked_files = set()
        for line in result.stdout.strip().splitlines():
            try:
                # 路径已经是相对于 root_dir 的了
                file_path = root_dir / Path(line.strip())
                # 存储相对于 root_dir 的 Path 对象
                tracked_files.add(file_path.relative_to(root_dir))
            except ValueError:
                logging.warning(f"无法处理 git ls-files 输出的路径: '{line.strip()}'")

        logging.info(f"找到 {len(tracked_files)} 个 Git 跟踪的文件。")
        return tracked_files
    except FileNotFoundError:
        logging.error("错误：未找到 'git' 命令。")
        return None
    except subprocess.CalledProcessError as e:
        logging.error(f"运行 'git ls-files' 时出错: {e}")
        logging.error(f"Git stderr: {e.stderr}")
        return None
    except Exception as e:
        logging.error(f"获取 Git 跟踪文件时发生未知错误: {e}", exc_info=True)
        return None


# --- 文件处理函数 (基本不变，接收 root_dir 参数) ---
def process_python_file(
        file_path: Path,
        root_dir: Path,
        dry_run: bool = False
) -> Tuple[str, str]:
    # ... (函数内部逻辑基本不变，确保使用传入的 root_dir 计算 relative_path) ...
    try:
        relative_path = file_path.relative_to(root_dir)  # 使用传入的 root_dir
        expected_path_str = relative_path.as_posix()
        expected_comment = f"# {expected_path_str}"

        logging.debug(f"处理文件: {relative_path}")

        # --- 读取文件内容 ---
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                lines = f.readlines()
        except FileNotFoundError:
            return "Error (Not Found)", expected_path_str
        except Exception as e:
            logging.error(f"读取文件时出错 {relative_path}: {e}")
            return f"Error (Read failed: {e})", expected_path_str

        if not lines:
            return "Skipped (Empty)", expected_path_str

        # --- 检查 Shebang 和确定目标行 ---
        first_line = lines[0].strip()
        target_line_index = 0
        has_shebang = False
        if first_line.startswith("#!"):
            has_shebang = True
            target_line_index = 1
            if len(lines) == 1:
                lines.append("\n" + expected_comment + "\n")
                action = "Added"
            # else: 继续检查第二行

        # --- 检查/修改目标行 (如果 action 未定) ---
        action = "Unknown" if 'action' not in locals() else action  # 初始化
        if action == "Unknown":  # 只有在上面 shebang 逻辑未确定 action 时才执行
            if len(lines) > target_line_index:
                current_target_line = lines[target_line_index].strip()
                match = HEADER_COMMENT_REGEX.match(current_target_line)

                if match:
                    current_path_str_in_comment = match.group(1)
                    current_path_obj = Path(current_path_str_in_comment.replace('\\', '/'))
                    expected_path_obj = Path(expected_path_str)

                    if current_path_obj == expected_path_obj:
                        action = "Correct"
                    else:
                        logging.info(f"路径不匹配: 期望 '{expected_path_str}', 找到 '{current_path_str_in_comment}' in {relative_path}")
                        lines[target_line_index] = expected_comment + "\n"
                        action = "Updated"
                else:
                    logging.info(f"在 {relative_path} 的目标行 {target_line_index + 1} 未找到有效路径注释，将插入。")
                    lines.insert(target_line_index, expected_comment + "\n")
                    action = "Added"
            else:  # 文件行数不足
                logging.info(f"文件 {relative_path} 行数不足，在末尾添加注释。")
                lines.append(expected_comment + "\n")
                action = "Added"

        # --- 写回文件 (如果不是 dry run 且有更改) ---
        if action in ["Added", "Updated"] and not dry_run:
            try:
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.writelines(lines)
                logging.info(f"{action}: {expected_path_str}")
            except Exception as e:
                logging.error(f"写入文件时出错 {relative_path}: {e}")
                return f"Error (Write failed: {e})", expected_path_str
        elif action in ["Added", "Updated"] and dry_run:
            logging.info(f"[Dry Run] Would be {action}: {expected_path_str}")
        elif action == "Correct":
            logging.debug(f"{action}: {expected_path_str}")

        return action, expected_path_str

    except Exception as e:
        logging.error(f"处理文件 {file_path} 时发生意外错误: {e}", exc_info=True)
        return f"Error (Unexpected: {e})", str(file_path)


# --- 主函数 ---
def main():
    parser = argparse.ArgumentParser(
        description="检查并更新 Python 文件第一行的路径注释，仅处理 Git 跟踪的文件。自动查找 Git 根目录。"
    )
    # 移除了 root_dir 参数，因为我们自动查找
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="执行空运行，只显示将要进行的更改，不实际修改文件。"
    )
    parser.add_argument(
        "-v", "--verbose",
        action="store_true",
        help="显示更详细的日志 (包括状态为 'Correct' 的文件)。"
    )

    args = parser.parse_args()

    if args.verbose:
        logging.getLogger().setLevel(logging.DEBUG)

    # --- 自动查找 Git 根目录 ---
    root_path = find_git_root()
    if root_path is None:
        logging.error("未能找到 Git 仓库根目录，脚本终止。")
        sys.exit(1)

    logging.info(f"将在 Git 根目录 '{root_path}' 中处理 Python 文件...")
    if args.dry_run:
        logging.warning("*** Dry Run模式开启，不会修改任何文件！ ***")

    # 1. 获取 Git 跟踪的文件列表 (传入找到的 root_path)
    tracked_files = get_git_tracked_files(root_path)
    if tracked_files is None:
        logging.error("无法获取 Git 跟踪文件列表，脚本终止。")
        sys.exit(1)

    # --- 统计信息 ---
    processed_count = 0
    added_count = 0
    updated_count = 0
    correct_count = 0
    skipped_count = 0
    error_count = 0

    # 2. 遍历项目目录 (从找到的 root_path 开始)
    for dirpath, dirnames, filenames in os.walk(root_path, topdown=True):
        current_dir_path = Path(dirpath)

        # --- 排除 .git 目录 ---
        if '.git' in dirnames:
            dirnames.remove('.git')
        if '.git' in current_dir_path.relative_to(root_path).parts:  # 检查相对路径
            continue

        for filename in filenames:
            if filename.endswith(".py"):
                file_path = current_dir_path / filename
                try:
                    # 计算相对于根目录的路径用于比较
                    relative_file_path = file_path.relative_to(root_path)
                except ValueError:
                    logging.warning(f"跳过文件，无法计算相对路径: {file_path}")
                    continue

                # 3. 检查文件是否在 tracked_files 集合中
                if relative_file_path in tracked_files:
                    processed_count += 1
                    # 传入找到的 root_path
                    status, _ = process_python_file(file_path, root_path, args.dry_run)
                    # 更新统计
                    if status == "Added":
                        added_count += 1
                    elif status == "Updated":
                        updated_count += 1
                    elif status == "Correct":
                        correct_count += 1
                    elif status.startswith("Skipped"):
                        skipped_count += 1
                    elif status.startswith("Error"):
                        error_count += 1
                else:
                    logging.debug(f"Skipped (Not tracked by Git): {relative_file_path.as_posix()}")
                    skipped_count += 1  # 未跟踪也算跳过

    # --- 打印最终统计 ---
    logging.info("--- 处理完成 ---")
    # logging.info(f"总共检查文件数: {processed_count + skipped_count} (处理了 {processed_count} 个 .py 文件)") # 这个总数可能不准，因为 os.walk 包含未跟踪的
    logging.info(f"Git 跟踪并处理的 .py 文件: {processed_count}")
    logging.info(f"  - 注释已正确: {correct_count}")
    logging.info(f"  - 注释已添加: {added_count}")
    logging.info(f"  - 注释已更新: {updated_count}")
    logging.info(f"处理时跳过 (空文件): {skipped_count - (processed_count + added_count + updated_count + correct_count + error_count)}")  # 估算空文件跳过数
    logging.info(f"处理时发生错误: {error_count}")

    # ... (结束日志和可能的退出码) ...
    if not args.dry_run and (added_count > 0 or updated_count > 0):
        logging.warning("文件已被修改。建议使用 'git status' 或 'git diff' 检查更改。")
    elif args.dry_run and (added_count > 0 or updated_count > 0):
        logging.warning("Dry run 结束，未修改文件。重新运行时移除 --dry-run 以应用更改。")

    if error_count > 0:
        logging.error("脚本执行期间遇到错误，请检查上面的日志。")
        sys.exit(1)


if __name__ == "__main__":
    main()