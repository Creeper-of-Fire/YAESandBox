# parser.py
import ast
import logging
import random  # 导入 random 用于掷骰
import re
from typing import List, Dict, Any, Optional, Literal, Tuple, cast

# --- 正则表达式 ---
# COMMAND_REGEX (保持不变)
COMMAND_REGEX = re.compile(
    r"@(?P<command>\w+)\s+"
    r"(?P<type>Item|Character|Place)\s+"
    r"(?P<id>[\w\-]+)\s*"
    r"(?:\((?P<params>.*?)\))?\s*"
    , re.IGNORECASE | re.DOTALL
)

# PARAM_REGEX: 修改 op 部分，确保 = 也被捕获
PARAM_REGEX = re.compile(
    r'(?P<key>[\w\.]+)\s*'  # 键 (字母数字下划线点)
    # 修改 op 捕获组，明确包含 =
    r'(?P<op>[+\-*/]?=|\+|-|=)?\s*'  # 可选操作符 (包括 +/- 和 =)
    r'('
    r'"(?P<quoted_value>(?:\\.|[^"\\])*)"|'  # 带引号的值优先
    r"(?P<unquoted_value>[^,)]*?)"  # 不带引号的值 (非贪婪)
    r')'
    r'\s*(?:,|$|\))'  # 分隔符: 逗号, 结束符, 或右括号
)


# --- 掷骰函数 ---
def _calculate_dice_roll(expression: str) -> int:
    """根据骰子表达式字符串计算结果"""
    expression = expression.lower().strip()
    match = re.fullmatch(r"(\d+)d(\d+)\s*(?:([+-])\s*(\d+))?", expression)
    if not match:
        try:
            return int(expression)  # 尝试解析为整数
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


# --- 解析函数 ---
def parse_value(value_str: str) -> Any:
    """尝试将字符串值解析为 Python 类型 (int, float, bool, str, (Type, ID), 或 **执行掷骰**)"""
    value_str = value_str.strip()
    if not value_str: return None
    entity_type_match = re.fullmatch(r"(Item|Character|Place):([\w\-]+)", value_str, re.IGNORECASE)
    if entity_type_match:
        entity_type = entity_type_match.group(1).capitalize()
        entity_id = entity_type_match.group(2)
        logging.debug(f"解析为实体引用: ({entity_type}, {entity_id})")
        return cast(Tuple[Literal["Item", "Character", "Place"], str], (entity_type, entity_id))
    try:
        if re.fullmatch(r"\d+d\d+.*", value_str, re.IGNORECASE):
            roll_result = _calculate_dice_roll(value_str)
            logging.debug(f"解析并执行掷骰: '{value_str}' -> {roll_result}")
            return roll_result
        int_result = int(value_str)
        logging.debug(f"解析为整数: {int_result}")
        return int_result
    except ValueError:
        pass
    val_lower = value_str.lower()
    if val_lower == 'true': return True
    if val_lower == 'false': return False
    if (value_str.startswith('[') and value_str.endswith(']')) or (
            value_str.startswith('{') and value_str.endswith('}')):
        try:
            eval_result = ast.literal_eval(value_str)
            if isinstance(eval_result, (list, dict)):
                logging.debug(f"使用 ast.literal_eval 解析: '{value_str}' -> {repr(eval_result)}")
                return eval_result
            else:
                logging.warning(f"ast.literal_eval 返回了意外类型 ({type(eval_result)})，当作字符串处理。")
        except (ValueError, SyntaxError, TypeError) as e:
            logging.warning(f"ast.literal_eval 解析列表/字典失败: {e}。当作字符串处理。")
    if len(value_str) >= 2 and value_str.startswith('"') and value_str.endswith('"'):
        try:
            unescaped_str = ast.literal_eval(value_str)
            if isinstance(unescaped_str, str):
                logging.debug(f"解析为带引号字符串 (已处理转义): {unescaped_str}")
                return unescaped_str
        except Exception as e:
            logging.warning(f"解析带引号字符串 '{value_str}' 失败 ({e})，返回原始带引号字符串。")
    logging.debug(f"解析为普通字符串: {value_str}")
    return value_str


def parse_params_string(param_str: Optional[str]) -> Dict[str, Any]:
    """解析圆括号内的参数字符串 (统一返回 Dict[str, Tuple[str, Any]])"""
    params = {}
    if not param_str: return params
    last_pos = 0
    for match in PARAM_REGEX.finditer(param_str):
        gap = param_str[last_pos:match.start()].strip()
        if gap and gap != ',': logging.warning(f"解析参数时跳过无法识别的片段: '{gap}'")
        last_pos = match.end()
        key = match.group('key')
        op = match.group('op')
        quoted_value = match.group('quoted_value')
        unquoted_value = match.group('unquoted_value')
        raw_value_str = f'"{quoted_value}"' if quoted_value is not None else unquoted_value
        if raw_value_str is None:
            logging.warning(f"解析参数 '{key}' 时未能提取原始值字符串。")
            continue
        try:
            final_value = parse_value(raw_value_str.strip())
            # --- 核心改动：统一处理操作符 ---
            effective_op = op if op else '='  # 如果没有显式操作符，则默认为赋值操作 '='
            params[key] = (effective_op, final_value)  # 存储为元组 (op, value)
            logging.debug(f"存储参数 (统一格式): {key} = (op='{effective_op}', value={repr(final_value)})")
        except Exception as e:
            logging.error(f"解析参数值时出错: key='{key}', value='{raw_value_str}', error: {e}", exc_info=True)
            raise ValueError(f"解析参数 '{key}' 的值 '{raw_value_str}' 时失败: {e}") from e
    remaining = param_str[last_pos:].strip()
    if remaining and remaining != ')': logging.warning(f"解析参数时忽略了末尾无法识别的片段: '{remaining}'")
    return params


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
            # params 现在是 Dict[str, Tuple[str, Any]]
            params = parse_params_string(params_str)
            parsed_command = {
                "command": command_name,
                "entity_type": cast(Literal["Item", "Character", "Place"], entity_type),
                "entity_id": entity_id,
                "params": params,  # 直接存储解析结果
            }
            parsed_commands.append(parsed_command)
            processed_spans.add(match.span())
        except Exception as e:
            logging.error(f"解析指令 '{match.group(0).strip()}' 参数时出错: {e}", exc_info=True)
            raise e
    return parsed_commands
