# parser.py
import ast
import re
import logging
from typing import List, Dict, Any, Optional, Literal, Tuple, Union, cast
from dataclasses import dataclass

# --- 数据类定义 (保持不变) ---
@dataclass(frozen=True)
class DiceRollRequest:
    expression: str

# --- 正则表达式 ---
COMMAND_REGEX = re.compile(
    r"@(?P<command>\w+)\s*"
    r"(?P<type>Item|Character|Place)\s+"
    r"(?P<id>[\w\-]+)\s*"
    r"(?:\((?P<params>.*?)\))?\s*"
    r"(?:\[(?P<status_params>.*?)\])?"
    , re.IGNORECASE
)

# --- 更新 PARAM_REGEX，移除 (?R) ---
PARAM_REGEX = re.compile(
    r'(?P<key>[\w\.\-]+)\s*'                                    # 键 (key) - 支持点号
    r'(?:(?P<op>\+=|-=|\+|-)\s*)?'                              # 可选的操作符 (+, -, +=, -=)
    r'(?:\s*=\s*)?'                                             # 可选的等号
    r'('                                                       # 开始值捕获组
    # 列表值：尝试匹配平衡的方括号 (非递归，可能不完美处理复杂嵌套)
    # 匹配非 [ ] " 的字符，或者匹配完整的 "quoted string"
    r'\[(?P<list_value>(?:[^\[\]"]|"(?:\\.|[^"\\])*")*?)\]|'
    # 字典值：尝试匹配平衡的花括号 (非递归，同上)
    r'\{(?P<dict_value>(?:[^{}"]|"(?:\\.|[^"\\])*")*?)\}|'
    # 带引号的值
    r'"(?P<quoted_value>(?:\\.|[^"\\])*)"|'                     # "quoted value" (支持转义)
    # 未引用的值 (直到逗号、括号或结束，不允许引号)
    r'(?P<unquoted_value>[^,()\[\]{}"]+?)'
    r')\s*(?:,|$)'                                             # 逗号或行尾
    , re.DOTALL # 允许 . 匹配换行
)

# --- 解析函数 (parse_value, parse_params_string, parse_commands 保持不变) ---
# ... (后面的函数代码与上一版本相同) ...

def parse_value(value: str) -> Any:
    """尝试将字符串值解析为 Python 类型 (int, float, bool, str, (Type, ID), 或 DiceRollRequest)"""
    value = value.strip()
    # 1. 检查 EntityType:ID 格式
    entity_type_match = re.fullmatch(r"(Item|Character|Place):([\w\-]+)", value, re.IGNORECASE)
    if entity_type_match:
        entity_type = entity_type_match.group(1).capitalize() # 标准化首字母大写
        entity_id = entity_type_match.group(2)
        logging.debug(f"  解析为实体引用: ({entity_type}, {entity_id})")
        return cast(Tuple[Literal["Item", "Character", "Place"], str], (entity_type, entity_id))
    # 2. 检查骰子表达式格式 "XdY[+Z]" 或 "XdY-Z"
    dice_match = re.fullmatch(r"(\d+)d(\d+)(([+-])(\d+))?", value, re.IGNORECASE)
    if dice_match:
        logging.debug(f"  解析为骰子掷骰请求: {value}")
        return DiceRollRequest(expression=value) # 返回特殊对象
    # 3. 检查其他类型 (bool, int, float)
    if not value: return None
    val_lower = value.lower()
    if val_lower == 'true': return True
    if val_lower == 'false': return False
    try: return int(value)
    except ValueError: pass
    try: return float(value)
    except ValueError: pass
    # 4. 默认返回原始字符串
    logging.debug(f"  解析为普通字符串: {value}")
    return value

def parse_params_string(param_str: Optional[str]) -> Dict[str, Any]:
    """解析括号或方括号内的参数字符串 (支持多种格式)"""
    params = {}
    if not param_str: return params
    last_pos = 0
    for match in PARAM_REGEX.finditer(param_str):
        if match.start() > last_pos:
             unmatched_text = param_str[last_pos:match.start()].strip()
             if unmatched_text and unmatched_text != ',':
                 logging.warning(f"解析参数时跳过无法识别的片段: '{unmatched_text}'")
        last_pos = match.end()

        key = match.group('key')
        op = match.group('op')
        quoted_value = match.group('quoted_value')
        list_value_str = match.group('list_value')
        dict_value_str = match.group('dict_value')
        unquoted_value = match.group('unquoted_value')

        final_value: Any = None
        value_source_for_parse_value: Optional[str] = None

        try:
            if list_value_str is not None: # 优先处理列表/字典
                eval_str = f'[{list_value_str}]'
                logging.debug(f"  尝试用 ast.literal_eval 解析列表: {eval_str}")
                final_value = ast.literal_eval(eval_str)
                if not isinstance(final_value, list):
                    logging.warning(f"解析列表失败 (ast未返回列表): key='{key}', input='{eval_str}'. 回退为原始字符串。")
                    final_value = f'[{list_value_str}]'
                logging.debug(f"  解析列表结果: {repr(final_value)}")
            elif dict_value_str is not None:
                eval_str = f'{{{dict_value_str}}}'
                logging.debug(f"  尝试用 ast.literal_eval 解析字典: {eval_str}")
                final_value = ast.literal_eval(eval_str)
                if not isinstance(final_value, dict):
                     logging.warning(f"解析字典失败 (ast未返回字典): key='{key}', input='{eval_str}'. 回退为原始字符串。")
                     final_value = f'{{{dict_value_str}}}'
                logging.debug(f"  解析字典结果: {repr(final_value)}")
            elif quoted_value is not None:
                eval_str = f'"{quoted_value.replace("\"", "\\\"")}"'
                logging.debug(f"  尝试用 ast.literal_eval 解析带引号值: {eval_str}")
                parsed_quoted_value = ast.literal_eval(eval_str)
                if isinstance(parsed_quoted_value, str):
                    value_source_for_parse_value = parsed_quoted_value
                    logging.debug(f"  ast.literal_eval 返回字符串，交由 parse_value 处理: '{value_source_for_parse_value}'")
                else:
                    final_value = parsed_quoted_value
                    logging.warning(f"  ast.literal_eval 对带引号值返回了非字符串类型: {type(final_value)}。直接使用。")
            elif unquoted_value is not None:
                value_source_for_parse_value = unquoted_value
                logging.debug(f"  提取无引号值，准备交给 parse_value: '{value_source_for_parse_value}'")
            else:
                logging.warning(f"解析参数时匹配到未知情况: key='{key}' in '{param_str}'")
                continue

            if value_source_for_parse_value is not None and final_value is None:
                logging.debug(f"  调用 parse_value 处理字符串: '{value_source_for_parse_value}'")
                final_value = parse_value(value_source_for_parse_value)
                logging.debug(f"  parse_value 返回: {repr(final_value)}")

        except (ValueError, SyntaxError, TypeError) as e:
             logging.warning(f"解析参数值时出错 (key='{key}', error: {e})。原始片段: {match.group(0).strip()}")
             value_part_match = re.search(r'(?:=|\+=|-=|\+|-)?\s*(.*)', match.group(0)[len(key):])
             raw_value_part = value_part_match.group(1).strip() if value_part_match else match.group(0)[len(key):].strip()
             if raw_value_part:
                 final_value = raw_value_part
                 logging.warning(f"  解析失败，回退为原始字符串值: '{final_value}'")
             else:
                  logging.error(f"  解析失败且无法获取原始值字符串。")
                  continue
        except Exception as e:
             logging.error(f"解析参数值时发生意外错误 (key='{key}', error: {e})", exc_info=True)
             raise e

        if op:
            params[key] = (op, final_value)
            logging.debug(f"  存储参数 (带操作符): {key} = (op='{op}', value={repr(final_value)})")
        else:
            params[key] = final_value
            logging.debug(f"  存储参数 (赋值): {key} = {repr(final_value)}")

    if last_pos < len(param_str.strip()):
         unmatched_text = param_str[last_pos:].strip()
         if unmatched_text:
             logging.warning(f"解析参数时忽略了末尾无法识别的片段: '{unmatched_text}'")
    return params

def parse_commands(text: str) -> List[Dict[str, Any]]:
    """从文本中解析所有 @Command 指令"""
    parsed_commands = []
    processed_spans = set()

    for match in COMMAND_REGEX.finditer(text):
        if any(start <= match.start() < end or start < match.end() <= end or (match.start() <= start and match.end() >= end)
               for start, end in processed_spans):
            continue

        command_data = match.groupdict()
        command_name = command_data['command'].lower()

        entity_type = command_data.get('type')
        entity_id = command_data.get('id')

        if not entity_type or not entity_id:
             logging.warning(f"跳过格式错误的指令 (缺少 type 或 id): {match.group(0)}")
             continue

        entity_type = entity_type.capitalize()
        if entity_type not in ["Item", "Character", "Place"]:
             logging.warning(f"跳过无效指令: 未知实体类型 '{entity_type}'")
             continue

        try:
            params_core = parse_params_string(command_data.get('params'))
            params_status = parse_params_string(command_data.get('status_params'))
            final_params: Dict[str, Any] = {}

            if command_name == 'create':
                 merged_params = {}
                 temp_merged = params_core.copy()
                 if params_status:
                     logging.warning(f"Create ({entity_id}): 不推荐使用 [] 定义属性，但已尝试合并。请使用 ()。")
                     temp_merged.update(params_status)
                 for k, v in temp_merged.items():
                      if isinstance(v, tuple) and len(v) == 2 and v[0] in ('+=', '-=', '+', '-'):
                          op, actual_value = v
                          logging.warning(f"Create ({entity_id}): 参数 '{k}' 含有无效操作符 '{op}'，将尝试直接使用其值 '{repr(actual_value)}'。")
                          merged_params[k] = actual_value
                      else: merged_params[k] = v
                 if 'name' not in merged_params:
                     raise ValueError(f"Create 指令 ({entity_id}) 缺少 'name' 参数")
                 final_params = merged_params
            elif command_name == 'modify':
                 final_params = params_core.copy()
                 if params_status:
                     logging.warning(f"Modify 指令 ({entity_id}) 使用了不推荐的 [] 括号，已尝试合并。请使用 ()。")
                     final_params.update(params_status)
            elif command_name == 'destroy': pass
            elif command_name == 'transfer':
                 target_val = params_core.get('target')
                 if target_val is None:
                     raise ValueError(f"Transfer 指令 ({entity_id}) 缺少 'target' 参数")
                 actual_target = target_val
                 if isinstance(target_val, tuple) and len(target_val) == 2 and target_val[0] in ('+=', '-=', '+', '-'):
                     op, actual_target = target_val
                     logging.warning(f"Transfer ({entity_id}): 'target' 参数含有无效操作符 '{op}'，已忽略操作符。")
                 valid_target = False
                 if isinstance(actual_target, str): valid_target = True
                 elif isinstance(actual_target, tuple) and len(actual_target) == 2 \
                      and isinstance(actual_target[0], str) and actual_target[0].capitalize() in ["Item", "Character", "Place"] \
                      and isinstance(actual_target[1], str): valid_target = True
                 if not valid_target:
                     raise ValueError(f"Transfer 指令 ({entity_id}) 的 target 参数格式无效: {repr(actual_target)}")
                 final_params = {'target': actual_target}
            else:
                 logging.warning(f"跳过未知指令: '@{command_data['command']}'")
                 continue

            parsed_command = {
                "command": command_name, "entity_type": entity_type,
                "entity_id": entity_id, "params": final_params,
            }
            parsed_commands.append(parsed_command)
            processed_spans.add(match.span())
        except Exception as e:
            logging.error(f"解析指令 '{match.group(0)}' 参数时出错: {e}", exc_info=True)
            # 选择跳过此指令或让错误冒泡

    return parsed_commands

# --- 添加必要的导入 ---
from typing import cast, Literal