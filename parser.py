# parser.py
import ast
import re
import logging
from typing import List, Dict, Any, Optional, Literal, Tuple

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

# 匹配参数字符串中的 key<op>=value, key=value 等
# key 支持点号 .
# op 支持 +=, -=, +, - (移除了 *=, /=)
# value 支持 "quoted", [list], {dict}, unquoted
PARAM_REGEX = re.compile(
    r'(?P<key>[\w\.\-]+)\s*'                                    # 键 (key) - 支持点号
    # 修改 op 捕获组，添加 + 和 -，移除 * 和 /
    r'(?:(?P<op>\+=|-=|\+|-)\s*)?'                              # 可选的操作符 (+, -, +=, -=)
    r'(?:\s*=\s*)?'                                             # 可选的等号
    r'('                                                       # 开始值捕获组
    r'"(?P<quoted_value>(?:\\.|[^"\\])*)"|'                     # "quoted value" (支持转义)
    # 列表值：匹配平衡的方括号
    r'\[(?P<list_value>(?:[^\[\]"]|"(?:\\.|[^"\\])*")*?)\]|'
    # 字典值：匹配平衡的花括号 (简化版，可能不完美处理复杂嵌套)
    r'\{(?P<dict_value>(?:[^{}"]|"(?:\\.|[^"\\])*")*?)\}|'
    # 未引用的值 (非贪婪，直到逗号、括号或结束)
    r'(?P<unquoted_value>[^,()\[\]{}]+?)'
    r')\s*(?:,|$)'                                             # 逗号或行尾
    , re.DOTALL # 允许 . 匹配换行符
)

# --- 函数 ---
def parse_value(value: str):
    """尝试将字符串值解析为 Python 类型 (int, float, bool, str, 或 (Type, ID) 元组)"""
    value = value.strip()
    # 检查新的 EntityType:entity_id 格式
    entity_type_match = re.fullmatch(r"(Item|Character|Place):([\w\-]+)", value)
    if entity_type_match:
        entity_type = entity_type_match.group(1)
        entity_id = entity_type_match.group(2)
        # 返回一个元组来区分这种特殊格式
        logging.debug(f"  解析为实体引用: ({entity_type}, {entity_id})")
        return (entity_type, entity_id) # type: ignore # 告诉类型检查器我们有意返回元组

    # 如果不是实体引用格式，继续尝试解析为其他类型
    if not value: return None
    if value.lower() == 'true': return True
    if value.lower() == 'false': return False
    try: return int(value)
    except ValueError: pass
    try: return float(value)
    except ValueError: pass
    # 默认返回原始字符串 (去除两端空白)
    return value

# --- 更新后的 parse_params_string (修正乱码问题) ---
def parse_params_string(param_str: Optional[str]) -> Dict[str, Any]:
    """
    解析括号或方括号内的参数字符串。
    (支持列表、字典、点号key, +/- 操作符, Type:ID 引用 - 无论是否带引号)
    (修正中文乱码问题)
    """
    params = {}
    if not param_str: return params

    for match in PARAM_REGEX.finditer(param_str):
        key = match.group('key')
        op = match.group('op') # '+' , '-', '+=', '-=' or None
        quoted_value = match.group('quoted_value') # 字符串内容，不含引号，但可能含转义
        list_value_str = match.group('list_value') # 列表内容，不含方括号
        dict_value_str = match.group('dict_value') # 字典内容，不含花括号
        unquoted_value = match.group('unquoted_value') # 未引用的字符串

        final_value: Any = None
        value_source_for_parse_value: Optional[str] = None # 用于存储需要 parse_value 处理的字符串

        try:
            if quoted_value is not None:
                # 使用 ast.literal_eval 解析带引号的字符串字面量
                # 需要重新加上引号，并确保内部引号被正确转义 (PARAM_REGEX 已处理 \)
                # 直接构造一个带引号的字符串传递给 literal_eval
                # Python 的 f-string 会自动处理 quoted_value 中的特殊字符，如 \
                # 但我们需要确保 quoted_value 中的 " 被转义为 \"
                escaped_quoted_value = quoted_value.replace('"', '\\"') # 基本转义
                eval_str = f'"{escaped_quoted_value}"'
                logging.debug(f"  尝试用 ast.literal_eval 解析带引号值: {eval_str}")
                parsed_quoted_value = ast.literal_eval(eval_str)

                if isinstance(parsed_quoted_value, str):
                    # 现在得到了正确的字符串，需要交给 parse_value 做最后检查 (Type:ID 等)
                    value_source_for_parse_value = parsed_quoted_value
                    logging.debug(f"  ast.literal_eval 返回字符串: '{value_source_for_parse_value}'")
                else:
                    # 如果 literal_eval 返回了非字符串（理论上不应该），直接使用
                    final_value = parsed_quoted_value
                    logging.warning(f"  ast.literal_eval 对带引号值返回了非字符串类型: {type(final_value)}。直接使用。")

            elif list_value_str is not None:
                # 列表/字典处理不变
                eval_str = f'[{list_value_str}]'
                logging.debug(f"  尝试用 ast.literal_eval 解析列表: {eval_str}")
                final_value = ast.literal_eval(eval_str)
                if not isinstance(final_value, list):
                    logging.warning(f"解析列表失败 (ast未返回列表): key='{key}', input='{eval_str}'. 回退为原始字符串。")
                    final_value = list_value_str # 回退
                logging.debug(f"  解析列表结果: {repr(final_value)}")

            elif dict_value_str is not None:
                eval_str = f'{{{dict_value_str}}}'
                logging.debug(f"  尝试用 ast.literal_eval 解析字典: {eval_str}")
                final_value = ast.literal_eval(eval_str)
                if not isinstance(final_value, dict):
                     logging.warning(f"解析字典失败 (ast未返回字典): key='{key}', input='{eval_str}'. 回退为原始字符串。")
                     final_value = dict_value_str # 回退
                logging.debug(f"  解析字典结果: {repr(final_value)}")

            elif unquoted_value is not None:
                # 未引用的值，直接交给 parse_value 处理
                value_source_for_parse_value = unquoted_value.strip()
                logging.debug(f"  提取无引号值，准备交给 parse_value: '{value_source_for_parse_value}'")

            else:
                # 这个分支理论上不应该被命中，因为正则至少会匹配到一个组
                logging.warning(f"解析参数时遇到未知值格式或正则匹配问题: key='{key}' in '{param_str}'")
                continue # 跳过这个无法解析的参数

            # --- 如果从 quoted 或 unquoted 提取了字符串，调用 parse_value ---
            if value_source_for_parse_value is not None and final_value is None:
                logging.debug(f"  调用 parse_value 处理字符串: '{value_source_for_parse_value}'")
                final_value = parse_value(value_source_for_parse_value)
                logging.debug(f"  parse_value 返回: {repr(final_value)}")

        except (ValueError, SyntaxError, TypeError) as e:
             # 捕获 ast.literal_eval 可能抛出的错误
             logging.warning(f"解析参数值时出错 (key='{key}', error: {e})。原始片段: {match.group(0).strip()}")
             # 尝试将无法解析的部分作为字符串处理（取原始捕获组）
             # 第2个捕获组是整个 value 部分 (包含引号、括号等)
             raw_value_part = match.group(2)
             if raw_value_part:
                 # 作为最后的手段，直接使用原始匹配到的值字符串
                 final_value = raw_value_part.strip()
                 logging.warning(f"  解析失败，回退为原始字符串值: '{final_value}'")
             else:
                  logging.error(f"  解析失败且无法获取原始值字符串。")
                  continue # 跳过

        except Exception as e:
             # 捕获其他意外错误
             logging.error(f"解析参数值时发生意外错误 (key='{key}', error: {e})", exc_info=True)
             # 原型阶段，让它崩溃
             raise e


        # --- 存储解析结果 (不变) ---
        if op:
            params[key] = (op, final_value)
            logging.debug(f"  存储参数 (带操作符): {key} = (op='{op}', value={repr(final_value)})")
        else:
            params[key] = final_value
            logging.debug(f"  存储参数 (赋值): {key} = {repr(final_value)}")

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

        params_core = parse_params_string(command_data.get('params'))
        params_status = parse_params_string(command_data.get('status_params')) # 通常为空或不推荐

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
                  else:
                      merged_params[k] = v

             if 'name' not in merged_params:
                 # 在原型阶段，如果缺少 name，让程序崩溃可能更好
                 logging.error(f"Create 指令错误 ({entity_id}): 缺少必需的 'name' 参数。")
                 raise ValueError(f"Create 指令 ({entity_id}) 缺少 'name' 参数")
                 # logging.warning(f"跳过 Create 指令 ({entity_id}): 缺少 'name' 参数。")
                 # continue
             final_params = merged_params

        elif command_name == 'modify':
             final_params = params_core.copy()
             if params_status:
                 logging.warning(f"Modify 指令 ({entity_id}) 使用了不推荐的 [] 括号，已尝试合并。请使用 ()。")
                 final_params.update(params_status)
             if not final_params:
                 # 修改指令无参数是合法的吗？也许只是想触发某种效果？暂时允许。
                 logging.info(f"Modify 指令 ({entity_id}): 无参数。")
                 # logging.warning(f"跳过 Modify 指令 ({entity_id}): 无参数。")
                 # continue
             final_params = final_params # 确保赋值

        elif command_name == 'destroy': pass # 无参数

        elif command_name == 'transfer':
             target_val = params_core.get('target')
             if target_val is None:
                 logging.error(f"Transfer 指令错误 ({entity_id}): 缺少必需的 'target' 参数。")
                 raise ValueError(f"Transfer 指令 ({entity_id}) 缺少 'target' 参数")
                 # logging.warning(f"跳过 Transfer 指令 ({entity_id}): 缺少 'target' 参数。")
                 # continue

             actual_target = target_val
             if isinstance(target_val, tuple) and len(target_val) == 2 and target_val[0] in ('+=', '-=', '+', '-'):
                 op, actual_target = target_val
                 logging.warning(f"Transfer ({entity_id}): 'target' 参数含有无效操作符 '{op}'，已忽略操作符。")

             # 验证 actual_target 类型 (Type, ID) or str
             valid_target = False
             if isinstance(actual_target, str): # 纯 ID
                 valid_target = True
             elif isinstance(actual_target, tuple) and len(actual_target) == 2 and actual_target[0] in ["Item", "Character", "Place"] and isinstance(actual_target[1], str): # (Type, ID)
                 valid_target = True

             if not valid_target:
                 logging.error(f"Transfer 指令错误 ({entity_id}): 'target' 参数格式无法识别 '{repr(actual_target)}'。期望 Type:ID 或 ID。")
                 raise ValueError(f"Transfer 指令 ({entity_id}) 的 target 参数格式无效: {repr(actual_target)}")
                 # logging.warning(f"跳过 Transfer 指令 ({entity_id}): 'target' 参数格式无法识别 '{repr(actual_target)}'。期望 Type:ID 或 ID。")
                 # continue

             final_params = {'target': actual_target}
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

    return parsed_commands
