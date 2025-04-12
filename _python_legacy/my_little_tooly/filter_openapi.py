# my_little_tooly/filter_openapi.py
import json
import logging
import argparse
from pathlib import Path
from typing import List, Dict, Any, Set

import requests  # 需要安装: pip install requests

# --- 配置 ---
# 默认的 FastAPI 应用地址和 OpenAPI 端点
# 确保你的 FastAPI 应用正在运行，并且这个 URL 是可访问的
DEFAULT_OPENAPI_URL = "http://127.0.0.1:6700/openapi.json"
# 默认的输出文件路径
DEFAULT_OUTPUT_FILE = "../docs/openapi_frontend.json"
# 要从 Schema 中移除的 Tags 列表
# 与你在 api/main.py 中为 debug 路由设置的 Tag 一致
TAGS_TO_EXCLUDE = {
    "_Internal/Debug - Commands",
    "_Internal/Debug - Game State",
    # 你可以根据需要添加其他内部 Tag
}

logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')


def fetch_openapi_schema(url: str) -> Dict[str, Any]:
    """从给定的 URL 获取 OpenAPI JSON schema。"""
    logging.info(f"正在从 {url} 获取 OpenAPI schema...")
    try:
        response = requests.get(url, timeout=10)  # 设置超时
        response.raise_for_status()  # 检查 HTTP 错误 (4xx, 5xx)
        schema = response.json()
        logging.info("成功获取并解析 OpenAPI schema。")
        return schema
    except requests.exceptions.ConnectionError as e:
        logging.error(f"无法连接到 FastAPI 应用: {e}")
        raise
    except requests.exceptions.Timeout:
        logging.error("获取 OpenAPI schema 超时。")
        raise
    except requests.exceptions.HTTPError as e:
        logging.error(f"获取 OpenAPI schema 时发生 HTTP 错误: {e}")
        raise
    except json.JSONDecodeError as e:
        logging.error(f"无法解析 OpenAPI 响应为 JSON: {e}")
        raise
    except Exception as e:
        logging.error(f"获取 OpenAPI schema 时发生未知错误: {e}", exc_info=True)
        raise


def filter_schema(schema: Dict[str, Any], tags_to_exclude: Set[str]) -> Dict[str, Any]:
    """根据指定的 Tag 过滤 OpenAPI schema。"""
    logging.info(f"开始过滤 schema，排除 Tags: {tags_to_exclude}")

    # 深拷贝原始 schema 以避免修改原对象
    filtered_schema = json.loads(json.dumps(schema))

    paths = filtered_schema.get("paths", {})
    paths_to_remove: Set[str] = set()

    # 遍历所有路径和方法
    for path, methods in paths.items():
        for method, details in methods.items():
            # 获取当前操作的 tags
            operation_tags = set(details.get("tags", []))
            # 检查是否有任何 tag 在排除列表中
            if not operation_tags.isdisjoint(tags_to_exclude):
                logging.info(f"标记移除路径 '{path}' (方法: {method.upper()})，因为它包含排除的 Tag: {operation_tags.intersection(tags_to_exclude)}")
                paths_to_remove.add(path)
                break  # 找到一个匹配的 tag 就足够移除整个路径了

    # 实际移除标记的路径
    removed_count = 0
    for path in paths_to_remove:
        if path in paths:
            del paths[path]
            removed_count += 1

    logging.info(f"过滤完成，共移除了 {removed_count} 个路径。")

    # --- 可选：清理未被引用的 schemas ---
    # 这是一个更复杂的步骤，需要分析剩余路径中所有 $ref 的引用
    # 对于初期来说，可以省略这一步，即使包含未使用的 schema 通常也无害
    # logging.info("（注意：未执行未引用 Schema 的清理）")

    # --- 可选：清理未被引用的 tags (从顶层 tags 列表) ---
    if "tags" in filtered_schema:
        referenced_tags = set()
        for path, methods in filtered_schema.get("paths", {}).items():
            for method, details in methods.items():
                referenced_tags.update(details.get("tags", []))

        original_tags_list = filtered_schema.get("tags", [])
        filtered_tags_list = [tag_def for tag_def in original_tags_list if tag_def.get("name") in referenced_tags]

        if len(filtered_tags_list) < len(original_tags_list):
            logging.info(f"从顶层移除了 {len(original_tags_list) - len(filtered_tags_list)} 个未使用的 Tag 定义。")
            filtered_schema["tags"] = filtered_tags_list

    return filtered_schema


def save_schema(schema: Dict[str, Any], output_file: Path):
    """将过滤后的 schema 保存到文件。"""
    logging.info(f"正在将过滤后的 schema 保存到: {output_file}")
    try:
        # 确保目录存在
        output_file.parent.mkdir(parents=True, exist_ok=True)
        # 使用 indent=2 美化输出 JSON
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(schema, f, ensure_ascii=False, indent=2)
        logging.info("成功保存过滤后的 schema。")
    except IOError as e:
        logging.error(f"无法写入输出文件 {output_file}: {e}")
        raise
    except Exception as e:
        logging.error(f"保存 schema 时发生未知错误: {e}", exc_info=True)
        raise


def main():
    """主执行函数"""
    parser = argparse.ArgumentParser(description="获取并过滤 FastAPI OpenAPI schema，移除指定 Tag 的路径。")
    parser.add_argument(
        "--url",
        default=DEFAULT_OPENAPI_URL,
        help=f"FastAPI 应用的 OpenAPI JSON 端点 URL (默认: {DEFAULT_OPENAPI_URL})"
    )
    parser.add_argument(
        "-o", "--output",
        default=DEFAULT_OUTPUT_FILE,
        help=f"过滤后的 OpenAPI schema 输出文件路径 (默认: {DEFAULT_OUTPUT_FILE})"
    )
    # 可以添加参数来覆盖要排除的 Tag，但通常在脚本中定义更方便
    # parser.add_argument("--exclude-tags", nargs='+', default=list(TAGS_TO_EXCLUDE), help="要排除的 Tag 列表")

    args = parser.parse_args()

    output_path = Path(args.output)
    tags_to_exclude_set = set(TAGS_TO_EXCLUDE)  # 使用脚本中定义的

    try:
        original_schema = fetch_openapi_schema(args.url)
        filtered_schema = filter_schema(original_schema, tags_to_exclude_set)
        save_schema(filtered_schema, output_path)
        logging.info("脚本执行成功！")
    except Exception as e:
        logging.error(f"脚本执行失败: {e}")
        # 可以考虑在这里 sys.exit(1)


if __name__ == "__main__":
    main()