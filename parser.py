# parser.py
import ast
import logging
import random
import re
from typing import List, Dict, Any, Optional, Literal, Tuple, cast, Union # 添加 Union

# --- 正则表达式 (保持不变) ---
COMMAND_REGEX = re.compile(
    r"@(?P<command>\w+)\s+"
    r"(?P<type>Item|Character|Place)\s+"
    r"(?P<id>[\w\-]+)\s*"
    r"(?:\((?P<params>.*?)\))?\s*"
    , re.IGNORECASE | re.DOTALL
)

PARAM_REGEX = re.compile(
    r'(?P<key>[\w\.]+)\s*'
    r'(?P<op>[+\-*/]?=|\+|-|=)?\s*'
    r'('
    r'"(?P<quoted_value>(?:\\.|[^"\\])*)"|'
    r"(?P<unquoted_value>[^,)]*?)"
    r')'
    r'\s*(?:,|$|\))'
)


# --- 掷骰函数 (保持不变) ---
def _calculate_dice_roll(expression: str) -> int:
    """根据骰子表达式字符串计算结果"""
    expression = expression.lower().strip()
    match = re.fullmatch(r"(\d+)d(\d+)\s*(?:([+-])\s*(\d+))?", expression)
    if not match:
        try:
            return int(expression) # 尝试解析为整数
        except ValueError:
            raise ValueError(f"无效的骰子表达式或整数值: '{expression}'")

    num_dice = int(match.group(1))
    dice_type = int(match.group(2))
    modifier_op = match.group(3)
    modifier_val = int(match.group(4) or 0)
    if num_dice <= 0 or dice_type <= 0: raise ValueError(f"骰子数量和面数必须为正: {expression}")
    if num_dice > 100 or dice_type > 100: logging.warning(f"执行较大的掷骰计算: {expression}")
    total = sum(random.randint(1, dice_type) for _ in range(num_dice))
    if modifier_op == '+':
        total += modifier_val
    elif modifier_op == '-':
        total -= modifier_val
    logging.debug(f"计算掷骰: {expression} -> 结果 = {total}")
    return total


# --- 新增：递归解析辅助函数 ---
def _parse_recursive(value: Any) -> Any:
    """递归地解析列表和字典中的字符串值，尝试将其转换为实体引用"""
    if isinstance(value, list):
        # 对列表中的每个元素递归调用自身
        return [_parse_recursive(item) for item in value]
    elif isinstance(value, dict):
        # 对字典中的每个值递归调用自身
        return {k: _parse_recursive(v) for k, v in value.items()}
    elif isinstance(value, str):
        # 尝试将字符串解析为实体引用或其他类型
        # 注意：这里我们只检查引用格式，因为原始解析已由 parse_value 完成
        entity_type_match = re.fullmatch(r"(Item|Character|Place):([\w\-]+)", value.strip(), re.IGNORECASE)
        if entity_type_match:
            entity_type = entity_type_match.group(1).capitalize()
            entity_id = entity_type_match.group(2)
            logging.debug(f"递归解析: 字符串 '{value}' -> 实体引用: ({entity_type}, {entity_id})")
            # 返回元组形式的引用
            return cast(Tuple[Literal["Item", "Character", "Place"], str], (entity_type, entity_id))
        else:
            # 如果不是引用格式，保持字符串原样（因为它已在 parse_value 中被处理过）
            logging.debug(f"递归解析: 保持字符串: '{value}'")
            return value
    else:
        # 对于非列表/字典/字符串类型，直接返回原值
        return value


# --- 修改后的解析函数 ---
def parse_value(value_str: str) -> Any:
    """
    尝试将字符串值解析为 Python 类型 (int, float, bool, str, 实体引用元组, list, dict)。
    列表和字典会被递归解析内部的字符串以查找实体引用。
    会执行掷骰表达式。
    """
    value_str = value_str.strip()
    if not value_str: return None

    # 1. 优先检查是否为实体引用格式 (无引号)
    #    注意：带引号的 "Place:village-well" 会在后面作为普通字符串处理
    if not (value_str.startswith('"') and value_str.endswith('"')):
        entity_type_match = re.fullmatch(r"(Item|Character|Place):([\w\-]+)", value_str, re.IGNORECASE)
        if entity_type_match:
            entity_type = entity_type_match.group(1).capitalize()
            entity_id = entity_type_match.group(2)
            logging.debug(f"解析为实体引用: ({entity_type}, {entity_id})")
            return cast(Tuple[Literal["Item", "Character", "Place"], str], (entity_type, entity_id))

    # 2. 检查是否为掷骰表达式
    try:
        if re.fullmatch(r"\d+d\d+.*", value_str, re.IGNORECASE):
            roll_result = _calculate_dice_roll(value_str)
            logging.debug(f"解析并执行掷骰: '{value_str}' -> {roll_result}")
            return roll_result
    except ValueError:
        # 如果不是有效的掷骰或整数，继续尝试其他类型
        pass
    except Exception as e: # 捕获掷骰计算中其他可能的错误
        logging.error(f"执行掷骰 '{value_str}' 时出错: {e}", exc_info=True)
        # 在原型阶段，让它崩溃可能更好，或者返回原始字符串？暂定返回原始字符串
        logging.warning(f"掷骰计算失败，将 '{value_str}' 作为字符串处理。")
        return value_str # 返回原始字符串

    # 3. 尝试解析为基本 Python 类型 (int, float - 虽然我们没显式处理 float, bool)
    try:
        # 尝试整数
        int_result = int(value_str)
        logging.debug(f"解析为整数: {int_result}")
        return int_result
    except ValueError:
        pass # 不是整数，继续

    # 检查布尔值
    val_lower = value_str.lower()
    if val_lower == 'true':
        logging.debug("解析为布尔值: True")
        return True
    if val_lower == 'false':
        logging.debug("解析为布尔值: False")
        return False

    # 4. 尝试用 ast.literal_eval 解析列表、字典、带引号字符串等
    #    处理带引号的值，包括内部可能存在的转义
    #    例如 '"A string with \\"quotes\\" inside"' 或 '["list", "of", "strings"]'
    try:
        eval_result = ast.literal_eval(value_str)
        logging.debug(f"使用 ast.literal_eval 解析: '{value_str}' -> {repr(eval_result)}")

        # 5. 如果结果是列表或字典，进行递归解析
        if isinstance(eval_result, (list, dict)):
            logging.debug(f"ast 结果是 list/dict，开始递归解析...")
            recursive_result = _parse_recursive(eval_result)
            logging.debug(f"递归解析完成: {repr(recursive_result)}")
            return recursive_result
        elif isinstance(eval_result, (str, int, float, bool, type(None))):
             # ast 解析出的基本类型，直接返回
             return eval_result
        else:
             logging.warning(f"ast.literal_eval 返回了意外类型 ({type(eval_result)})，当作普通字符串处理。")
             # 如果 literal_eval 返回了非预期的类型（理论上不太可能），回退到原始字符串
             return value_str

    except (ValueError, SyntaxError, TypeError) as e:
        # 如果 literal_eval 失败（例如，无引号的字符串），则认为它是一个普通字符串
        logging.debug(f"ast.literal_eval 解析失败 ({e})，将 '{value_str}' 视为普通字符串。")
        return value_str
    except Exception as e:
        # 捕获其他可能的 ast 错误
        logging.error(f"解析值 '{value_str}' 时发生未预料的 ast 错误: {e}", exc_info=True)
        # 在原型阶段崩溃或返回原始字符串
        raise e # 或者 return value_str


# --- parse_params_string (保持不变) ---
def parse_params_string(param_str: Optional[str]) -> Dict[str, Tuple[str, Any]]:
    """解析圆括号内的参数字符串 (统一返回 Dict[str, Tuple[str, Any]])"""
    params = {}
    if not param_str: return params
    last_pos = 0
    for match in PARAM_REGEX.finditer(param_str):
        gap = param_str[last_pos:match.start()].strip()
        if gap and gap != ',': logging.warning(f"解析参数时跳过无法识别的片段: '{gap}'")
        last_pos = match.end()
        key = match.group('key')
        op = match.group('op') # op 可能为 None
        quoted_value = match.group('quoted_value')
        unquoted_value = match.group('unquoted_value')

        # 优先使用带引号的值，否则使用不带引号的值
        raw_value_str = f'"{quoted_value}"' if quoted_value is not None else unquoted_value

        if raw_value_str is None:
            logging.warning(f"解析参数 '{key}' 时未能提取原始值字符串。匹配详情: op='{op}', quoted='{quoted_value}', unquoted='{unquoted_value}'")
            # 如果操作符是 '-' (表示删除)，即使没有值也可能是有效的
            if op == '-':
                final_value = None # 删除操作不需要值
                effective_op = op
            else:
                 logging.error(f"参数 '{key}' 缺少值且操作符不是 '-'。跳过此参数。")
                 continue # 跳过这个无效参数
        else:
            try:
                # 调用增强后的 parse_value
                final_value = parse_value(raw_value_str.strip())
                effective_op = op if op else '=' # 如果没有显式操作符，则默认为赋值操作 '='
            except Exception as e:
                logging.error(f"解析参数值时出错: key='{key}', raw_value='{raw_value_str}', error: {e}", exc_info=True)
                # 让错误传播出去，以便 command executor 知道解析失败
                raise ValueError(f"解析参数 '{key}' 的值 '{raw_value_str}' 时失败: {e}") from e

        params[key] = (effective_op, final_value) # 存储为元组 (op, value)
        logging.debug(f"存储参数 (统一格式): {key} = (op='{effective_op}', value={repr(final_value)})")

    remaining = param_str[last_pos:].strip()
    if remaining and remaining != ')': logging.warning(f"解析参数时忽略了末尾无法识别的片段: '{remaining}'")
    return params


# --- parse_commands (保持不变) ---
def parse_commands(text: str) -> List[Dict[str, Any]]:
    """从文本中解析所有 @Command 指令 (使用 parse_params_string 返回统一格式)"""
    parsed_commands = []
    processed_spans = set()
    for match in COMMAND_REGEX.finditer(text):
        start, end = match.span()
        is_overlapping = False
        for s_start, s_end in processed_spans:
            # (重叠检查逻辑保持不变)
            if start >= s_start and end <= s_end:
                is_overlapping = True
                break
            if max(s_start, start) < min(s_end, end):
                is_overlapping = True
                break
        if is_overlapping:
            logging.debug(f"跳过重叠或内部匹配的指令: '{match.group(0).strip()}'")
            continue

        command_data = match.groupdict()
        command_name = command_data['command'].lower()
        entity_type_str = command_data.get('type')
        entity_id = command_data.get('id')
        params_str = command_data.get('params')
        if not entity_type_str or not entity_id:
            logging.error(f"内部错误：COMMAND_REGEX 匹配但缺少 type 或 id: {match.group(0)}")
            continue
        entity_type = entity_type_str.capitalize()
        if entity_type not in ["Item", "Character", "Place"]:
            logging.warning(f"跳过无效指令: 未知实体类型 '{entity_type}' in '{match.group(0).strip()}'")
            continue
        try:
            # params 现在是 Dict[str, Tuple[str, Any]], Any 可以是基本类型、引用元组、列表或字典（可能嵌套引用元组）
            params = parse_params_string(params_str)
            parsed_command = {
                "command": command_name,
                "entity_type": cast(Literal["Item", "Character", "Place"], entity_type),
                "entity_id": entity_id,
                "params": params, # 直接存储解析结果
            }
            parsed_commands.append(parsed_command)
            processed_spans.add(match.span())
        except Exception as e:
            # 捕获 parse_params_string 中可能抛出的错误
            logging.error(f"解析指令 '{match.group(0).strip()}' 参数时出错: {e}", exc_info=True)
            # 在原型阶段，让解析错误中断整个流程
            raise e # 重新抛出异常
    return parsed_commands