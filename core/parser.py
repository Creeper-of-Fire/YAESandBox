# core/parser.py
import ast
import logging
import random
import re
from typing import List, Dict, Any, Optional, Literal, Tuple, cast, Union

# --- 正则表达式 (只用于指令头和基本结构) ---
COMMAND_REGEX = re.compile(
    r"@(?P<command>\w+)\s+"
    r"(?P<type>Item|Character|Place)\s+"
    r"(?P<id>[\w\-]+)\s*"
    r"(?:\((?P<params>.*?)\))?\s*" # params 部分仍然用 .*? 捕获，后续手动解析
    , re.IGNORECASE | re.DOTALL
)

# 不再需要 PARAM_REGEX

ENTITY_REF_REGEX = re.compile(r"^(Item|Character|Place):([\w\-]+)$", re.IGNORECASE)
DICE_REGEX = re.compile(r"^\d+d\d+.*", re.IGNORECASE) # 用于检查掷骰格式

# --- 掷骰函数 (保持不变) ---
def _calculate_dice_roll(expression: str) -> int:
    expression = expression.lower().strip()
    match = re.fullmatch(r"(\d+)d(\d+)\s*(?:([+-])\s*(\d+))?", expression)
    if not match:
        try: return int(expression)
        except ValueError: raise ValueError(f"无效的骰子表达式或整数值: '{expression}'")
    num_dice, dice_type = int(match.group(1)), int(match.group(2))
    modifier_op, modifier_val = match.group(3), int(match.group(4) or 0)
    if num_dice <= 0 or dice_type <= 0: raise ValueError(f"骰子数量和面数必须为正: {expression}")
    if num_dice > 100 or dice_type > 100: logging.warning(f"执行较大的掷骰计算: {expression}")
    total = sum(random.randint(1, dice_type) for _ in range(num_dice))
    if modifier_op == '+': total += modifier_val
    elif modifier_op == '-': total -= modifier_val
    logging.debug(f"计算掷骰: {expression} -> 结果 = {total}")
    return total

# --- 递归解析辅助函数 (保持不变) ---
def _parse_recursive(value: Any) -> Any:
    if isinstance(value, list): return [_parse_recursive(item) for item in value]
    elif isinstance(value, dict): return {k: _parse_recursive(v) for k, v in value.items()}
    elif isinstance(value, str):
        entity_type_match = ENTITY_REF_REGEX.fullmatch(value.strip())
        if entity_type_match:
            entity_type = entity_type_match.group(1).capitalize()
            entity_id = entity_type_match.group(2)
            logging.debug(f"递归解析: 字符串 '{value}' -> 实体引用: ({entity_type}, {entity_id})")
            return cast(Tuple[Literal["Item", "Character", "Place"], str], (entity_type, entity_id))
        else:
            logging.debug(f"递归解析: 保持字符串: '{value}'")
            return value
    else: return value

# --- parse_value 函数 (保持不变) ---
def parse_value(value_str: str) -> Any:
    """解析单个参数值字符串，处理引号、掷骰、实体引用、基本类型和字面量。"""
    value_str = value_str.strip()
    if not value_str: return None
    content_str = value_str
    is_quoted = False
    # 尝试安全地去除引号并处理转义
    if (value_str.startswith('"') and value_str.endswith('"')) or \
       (value_str.startswith("'") and value_str.endswith("'")):
        try:
            # 使用 literal_eval 处理带引号的字符串，可以正确处理转义
            decoded_content = ast.literal_eval(value_str)
            # 只有当 literal_eval 返回字符串时，才认为是有效的带引号字符串
            if isinstance(decoded_content, str):
                content_str = decoded_content
                is_quoted = True
            else:
                # 如果 literal_eval 返回非字符串（如数字、列表），则 value_str 不是简单的带引号字符串
                pass # content_str 保持原始 value_str，is_quoted 保持 False
        except:
            # literal_eval 失败（引号不匹配或包含非法内容），按原始字符串处理
            content_str = value_str
            is_quoted = False

    # 优先检查内容是否为掷骰
    if isinstance(content_str, str) and DICE_REGEX.fullmatch(content_str):
        try:
            roll_result = _calculate_dice_roll(content_str)
            logging.debug(f"解析并执行掷骰: '{content_str}' (来自 '{value_str}') -> {roll_result}")
            return roll_result
        except ValueError: pass # 非有效掷骰，继续
        except Exception as e: logging.error(f"执行掷骰 '{content_str}' 时出错: {e}", exc_info=True); raise e

    # 检查内容是否为实体引用
    if isinstance(content_str, str):
        entity_type_match = ENTITY_REF_REGEX.fullmatch(content_str)
        if entity_type_match:
            entity_type = entity_type_match.group(1).capitalize()
            entity_id = entity_type_match.group(2)
            logging.debug(f"解析为实体引用: ({entity_type}, {entity_id}) (来自 '{value_str}')")
            return cast(Tuple[Literal["Item", "Character", "Place"], str], (entity_type, entity_id))

    # 如果原始字符串带引号，并且内容不是特殊类型，返回解码后的内容
    if is_quoted:
        logging.debug(f"解析为带引号的普通字符串内容: '{content_str}'")
        return content_str

    # 尝试解析基本类型 (作用于原始字符串 value_str)
    try: return int(value_str)
    except ValueError: pass
    try: return float(value_str)
    except ValueError: pass
    val_lower = value_str.lower()
    if val_lower == 'true': return True
    if val_lower == 'false': return False
    if val_lower == 'none': return None

    # 最后尝试用 literal_eval 解析列表/字典等 (作用于原始字符串 value_str)
    try:
        eval_result = ast.literal_eval(value_str)
        # 如果是列表/字典，递归解析内部字符串
        if isinstance(eval_result, (list, dict)):
            logging.debug(f"使用 ast.literal_eval 解析复杂结构，开始递归解析内部...")
            return _parse_recursive(eval_result)
        else: # 其他 literal_eval 能解析的类型 (如数字，理论上已被上面覆盖)
            return eval_result
    except: # 所有解析都失败，视为普通无引号字符串
        logging.debug(f"所有解析失败，将 '{value_str}' 视为无引号普通字符串。")
        return value_str

# --- 手动解析参数字符串 ---
def parse_params_string_manual(param_str: Optional[str]) -> Dict[str, Tuple[str, Any]]:
    """手动解析参数字符串，处理嵌套括号和引号。"""
    params = {}
    if not param_str: return params

    param_str = param_str.strip()
    current_pos = 0
    n = len(param_str)

    while current_pos < n:
        # 1. 查找 key (字母数字下划线点)
        key_match = re.match(r'([\w\.]+)\s*', param_str[current_pos:])
        if not key_match:
            logging.warning(f"无法在 '{param_str[current_pos:]}' 中找到参数键，停止解析剩余部分。")
            break
        key = key_match.group(1)
        current_pos += key_match.end()

        # 2. 查找 op (包括独立 +/-) 和 '='
        op = '=' # 默认赋值
        # 优先匹配多字符操作符
        op_match = re.match(r'([+\-*/]=)\s*', param_str[current_pos:])
        if op_match:
            op = op_match.group(1)
            current_pos += op_match.end()
        # 再匹配单字符操作符
        elif param_str[current_pos:].startswith(('+', '-', '=')):
            op = param_str[current_pos]
            current_pos += 1
            # 跳过操作符后的空格
            current_pos += len(param_str[current_pos:]) - len(param_str[current_pos:].lstrip())
        else:
             # 如果没有找到操作符，则假定为赋值 '='，并且当前位置就是值的开始
             op = '='


        # 3. 查找 value (处理嵌套和引号)
        value_start_pos = current_pos
        in_quotes = None        # None, '"', "'"
        bracket_level = 0       # []
        brace_level = 0         # {}
        paren_level = 0         # ()
        found_value_end = False

        while current_pos < n:
            char = param_str[current_pos]
            prev_char = param_str[current_pos-1] if current_pos > value_start_pos else None # 检查前一个字符

            if in_quotes:
                if char == in_quotes and prev_char != '\\':
                    in_quotes = None
            elif char == '"' or char == "'":
                in_quotes = char
            elif not in_quotes: # 只在引号外处理括号
                if char == '[': bracket_level += 1
                elif char == ']': bracket_level -= 1
                elif char == '{': brace_level += 1
                elif char == '}': brace_level -= 1
                elif char == '(': paren_level += 1
                elif char == ')': paren_level -= 1
                elif char == ',' and bracket_level == 0 and brace_level == 0 and paren_level == 0:
                    # 遇到顶级逗号，当前参数的值结束
                    found_value_end = True
                    break # 退出内层 while

            current_pos += 1
            # 如果循环到字符串末尾，值也结束了

        # 提取原始值字符串
        raw_value_str = param_str[value_start_pos:current_pos].strip()

        # 4. 处理值和操作符
        if op == '-' and not raw_value_str: # 删除操作
            final_value = None
        elif not raw_value_str and op != '-':
            logging.error(f"参数 '{key}' 操作符为 '{op}' 但缺少值。")
            # 尝试跳到下一个参数
            if found_value_end: current_pos += 1 # 跳过逗号
            current_pos += len(param_str[current_pos:]) - len(param_str[current_pos:].lstrip()) # 跳空格
            continue
        else:
            try:
                final_value = parse_value(raw_value_str) # 使用现有 parse_value 解析单个值
            except Exception as e:
                logging.error(f"解析参数值时出错: key='{key}', raw_value='{raw_value_str}', error: {e}", exc_info=True)
                raise ValueError(f"解析参数 '{key}' 的值 '{raw_value_str}' 时失败: {e}") from e

        params[key] = (op, final_value)
        logging.debug(f"手动解析存储参数: {key} = (op='{op}', value={repr(final_value)})")

        # 5. 移动到下一个参数 (跳过逗号和空格)
        if found_value_end: # 如果是因为逗号结束的
            current_pos += 1 # 跳过逗号
            # 跳过逗号后的空格
            current_pos += len(param_str[current_pos:]) - len(param_str[current_pos:].lstrip())
        elif current_pos == n: # 如果是因为字符串结尾结束的
            break
        else: # 解析在中间停止，但不是因为逗号？可能格式错误
             logging.warning(f"解析参数后在中间停止，遇到意外情况: '{param_str[current_pos:]}'")
             break # 停止解析

    return params


# --- parse_commands (现在调用手动解析器) ---
def parse_commands(text: str) -> List[Dict[str, Any]]:
    """从文本中解析所有 @Command 指令，使用手动参数解析器。"""
    parsed_commands = []
    processed_spans = set()
    for match in COMMAND_REGEX.finditer(text):
        start, end = match.span()
        # 重叠检查 (保持不变)
        is_overlapping = False
        for s_start, s_end in processed_spans:
            if start >= s_start and end <= s_end: is_overlapping = True; break
            if max(s_start, start) < min(s_end, end): is_overlapping = True; break
        if is_overlapping: logging.debug(f"跳过重叠匹配: '{match.group(0).strip()}'"); continue

        command_data = match.groupdict()
        command_name = command_data['command'].lower()
        entity_type_str = command_data.get('type')
        entity_id = command_data.get('id')
        params_str = command_data.get('params') # 获取括号内的原始字符串

        if not entity_type_str or not entity_id: logging.error(f"Regex 匹配但缺 type/id: {match.group(0)}"); continue
        entity_type = entity_type_str.capitalize()
        if entity_type not in ["Item", "Character", "Place"]: logging.warning(f"跳过无效类型 '{entity_type}': {match.group(0).strip()}"); continue

        try:
            # 调用手动参数解析器
            params = parse_params_string_manual(params_str)
            parsed_command = {
                "command": command_name,
                "entity_type": cast(Literal["Item", "Character", "Place"], entity_type),
                "entity_id": entity_id,
                "params": params,
            }
            parsed_commands.append(parsed_command)
            processed_spans.add(match.span())
        except Exception as e:
            logging.error(f"解析指令参数时出错 '{match.group(0).strip()}': {e}", exc_info=True)
            raise e # 原型阶段崩溃
    return parsed_commands