# parser.py
import re
import logging
from typing import List, Dict, Any, Optional, Literal

# --- 正则表达式 ---

# 匹配整个指令
COMMAND_REGEX = re.compile(
    r"@(?P<command>\w+)\s*"
    r"(?P<type>Item|Character|Place)\s+"
    r"(?P<id>[\w\-]+)\s*"
    r"(?:\((?P<params>.*?)\))?\s*" # 参数现在都在 () 里
    # 保留 status_params 的解析能力，虽然 Modify 现在统一用 ()，但 Create 还需要 []
    r"(?:\[(?P<status_params>.*?)\])?"
)

# 匹配参数字符串中的 key=value, key="value", key<op>value
# 修改 op 捕获组，不再错误匹配单独的 '='
PARAM_REGEX = re.compile(
    r'(?P<key>\w+)\s*'                                       # 键 (key)
    # 修改 op 组: 明确列出所有有效的操作符，不再使用可能匹配单独'='的 [+\-*/]?=
    r'(?:(?P<op>\+=|-=|\*=|/=|[-+])\s*)?'                    # 可选的操作符 (+=, -=, *=, /=, +, -)
    r'(?:\s*=\s*)?'                                          # 可选的等号 (现在 op 不会匹配它了)
    r'(?:"(?P<quoted_value>.*?)"|(?P<unquoted_value>[^,()\[\]]+))' # 值 (value)
)

# --- 函数 ---

def parse_value(value: str):
    """尝试将字符串值解析为 Python 类型 (int, float, bool, str)"""
    value = value.strip()
    if not value: return None
    if value.lower() == 'true': return True
    if value.lower() == 'false': return False
    try: return int(value)
    except ValueError: pass
    try: return float(value)
    except ValueError: pass
    return value

def parse_params_string(param_str: Optional[str]) -> Dict[str, Any]:
    """解析括号或方括号内的参数字符串"""
    params = {}
    if not param_str: return params

    for match in PARAM_REGEX.finditer(param_str):
        key = match.group('key')
        op = match.group('op')
        quoted_value = match.group('quoted_value')
        unquoted_value = match.group('unquoted_value')

        value_str = quoted_value if quoted_value is not None else unquoted_value
        if value_str is None:
             logging.warning(f"解析参数时发现无效格式，跳过: key='{key}' in '{param_str}'")
             continue

        # 如果有操作符，直接将 op + value 作为值存储，让 GameState 处理
        if op:
             # 确保 '+' 和 '-' 后面跟的是数字或有效字符串
             # '+=' 等已经包含了 '='，直接拼接
             if op in ['+', '-'] and not value_str.strip(): # 避免 "key=+" 或 "key=-"
                 logging.warning(f"解析参数 '{key}': 操作符 '{op}' 后面缺少值，跳过。")
                 continue
             final_value = f"{op}{value_str.strip()}"
        else:
            # 没有操作符，解析值的类型 (替换模式)
            final_value = parse_value(value_str)

        params[key] = final_value
        logging.debug(f"  解析参数: {key} = {repr(final_value)} (原始: '{value_str}', op: {op})")

    return params

def parse_commands(text: str) -> List[Dict[str, Any]]:
    """从文本中解析所有 @Command 指令"""
    parsed_commands = []
    for match in COMMAND_REGEX.finditer(text):
        command_data = match.groupdict()
        command_name = command_data['command'].lower()
        entity_type = command_data['type']
        entity_id = command_data['id']

        if entity_type not in ["Item", "Character", "Place"]:
             logging.warning(f"跳过无效指令: 未知实体类型 '{entity_type}'")
             continue

        # 解析核心参数 () 和 status 参数 []
        params = parse_params_string(command_data.get('params'))
        status_params = parse_params_string(command_data.get('status_params'))

        # 合并参数：对于 Modify，所有参数都在 () 内；对于 Create，() 是核心，[] 是 status
        final_params = {}
        if command_name == 'create':
             if status_params:
                 params['status'] = status_params # 将解析出的 status 字典放入 params
             if 'name' not in params:
                 logging.warning(f"跳过 Create 指令: 缺少必要的 'name' 参数。ID: {entity_id}")
                 continue
             final_params = params
        elif command_name == 'modify':
             # Modify 指令应该只用 ()
             if status_params:
                 logging.warning(f"Modify 指令 (ID: {entity_id}) 使用了不推荐的 [] 括号，已尝试合并。请使用 ()。")
                 params.update(status_params) # 尝试合并，但提示 AI 规范用法
             if not params:
                 logging.warning(f"跳过 Modify 指令: 没有提供任何要修改的参数。ID: {entity_id}")
                 continue
             final_params = params
        elif command_name == 'destroy':
             pass # Destroy 不需要参数
        elif command_name == 'transfer':
             if 'target' not in params:
                 logging.warning(f"跳过 Transfer 指令: 缺少必要的 'target' 参数。ID: {entity_id}")
                 continue
             final_params = {'target': params['target']} # 只取 target
        else:
             logging.warning(f"跳过未知指令: '@{command_data['command']}'")
             continue

        parsed_command = {
            "command": command_name,
            "entity_type": entity_type,
            "entity_id": entity_id,
            "params": final_params,
        }

        parsed_commands.append(parsed_command)
        logging.debug(f"解析结果: {parsed_command}")

    return parsed_commands