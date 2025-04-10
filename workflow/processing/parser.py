# workflow/processing/parser.py
import ast
import logging
import random
import re
from typing import List, Dict, Any, Optional, Literal, Tuple, cast, Union

# 从新的类型模块导入 TypedID
from core.types import TypedID, EntityType

# --- 正则表达式 (只用于指令头和基本结构) ---
COMMAND_REGEX = re.compile(
    r"@(?P<command>\w+)\s+"
    r"(?P<type>Item|Character|Place)\s+"
    r"(?P<id>[\w\-]+)\s*"
    r"(?P<has_paren>\()?" # 可选地捕获左括号
    , re.IGNORECASE # 移除 DOTALL，因为我们逐行或按匹配处理
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

# --- 递归解析辅助函数 (修改：返回 TypedID) ---
def _parse_recursive(value: Any) -> Any:
    if isinstance(value, list): return [_parse_recursive(item) for item in value]
    elif isinstance(value, dict): return {k: _parse_recursive(v) for k, v in value.items()}
    elif isinstance(value, str):
        value_stripped = value.strip()
        # 优先检查是否为实体引用字符串 'Type:ID'
        try:
            # 尝试使用 TypedID.from_string 解析
            typed_id = TypedID.from_string(value_stripped)
            logging.debug(f"递归解析: 字符串 '{value}' -> 实体引用 TypedID: {typed_id}")
            return typed_id
        except ValueError:
            # 如果不是有效的 TypedID 字符串，则保持为字符串
            logging.debug(f"递归解析: 保持字符串: '{value}'")
            return value # 返回原始字符串，而不是 stripped，以防空格有意义
    else:
        # 其他类型保持不变
        return value

# --- parse_value 函数 (修改：返回 TypedID) ---
def parse_value(value_str: str) -> Any:
    """解析单个参数值字符串，处理引号、掷骰、实体引用(TypedID)、基本类型和字面量。"""
    value_str = value_str.strip()
    if not value_str: return None
    content_str_or_obj: Any = value_str # 初始化为原始字符串
    is_quoted = False

    # 尝试安全地去除引号并处理转义
    if (value_str.startswith('"') and value_str.endswith('"')) or \
       (value_str.startswith("'") and value_str.endswith("'")):
        try:
            decoded_content = ast.literal_eval(value_str)
            if isinstance(decoded_content, str):
                content_str_or_obj = decoded_content
                is_quoted = True
            # else: content_str_or_obj 保持原始 value_str
        except:
            content_str_or_obj = value_str # 按原始字符串处理
            is_quoted = False

    # --- 优先检查特殊格式 (作用于 content_str_or_obj) ---
    if isinstance(content_str_or_obj, str):
        # 1. 检查掷骰
        if DICE_REGEX.fullmatch(content_str_or_obj):
            try:
                roll_result = _calculate_dice_roll(content_str_or_obj)
                logging.debug(f"解析并执行掷骰: '{content_str_or_obj}' (来自 '{value_str}') -> {roll_result}")
                return roll_result
            except ValueError: pass # 非有效掷骰
            except Exception as e: logging.error(f"执行掷骰 '{content_str_or_obj}' 时出错: {e}", exc_info=True); raise e

        # 2. 检查实体引用 (Type:ID)
        try:
            typed_id = TypedID.from_string(content_str_or_obj)
            logging.debug(f"解析为实体引用 TypedID: {typed_id} (来自 '{value_str}')")
            return typed_id
        except ValueError:
            # 不是有效的 Type:ID 字符串，继续
            pass

    # --- 如果带引号且内容不是特殊格式，返回解码后的字符串 ---
    if is_quoted:
        logging.debug(f"解析为带引号的普通字符串内容: '{content_str_or_obj}'")
        return content_str_or_obj

    # --- 尝试解析基本类型 (作用于原始 value_str) ---
    try: return int(value_str)
    except ValueError: pass
    try: return float(value_str)
    except ValueError: pass
    val_lower = value_str.lower()
    if val_lower == 'true': return True
    if val_lower == 'false': return False
    if val_lower == 'none': return None

    # --- 最后尝试用 literal_eval 解析列表/字典等 (作用于原始 value_str) ---
    try:
        eval_result = ast.literal_eval(value_str)
        # 如果是列表/字典，递归解析内部字符串
        if isinstance(eval_result, (list, dict)):
            logging.debug(f"使用 ast.literal_eval 解析复杂结构，开始递归解析内部...")
            # _parse_recursive 现在会返回包含 TypedID 的结构
            return _parse_recursive(eval_result)
        else:
            # 其他 literal_eval 能解析的类型
            return eval_result
    except:
        # 所有解析都失败，视为普通无引号字符串
        logging.debug(f"所有解析失败，将 '{value_str}' 视为无引号普通字符串。")
        return value_str # 返回原始 value_str

# --- 手动解析参数字符串 (无需修改，依赖 parse_value) ---
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
        if op == '-' and not raw_value_str: # 删除操作 (在 modify_attribute 中已移除，但解析器仍需识别)
            final_value = None
            logging.warning(f"解析到删除操作符 '-' (键 '{key}'), 但该操作在 modify_attribute 中已被移除。将值设为 None。")
        elif not raw_value_str and op != '-':
            logging.error(f"参数 '{key}' 操作符为 '{op}' 但缺少值。")
            # 尝试跳到下一个参数
            if found_value_end: current_pos += 1 # 跳过逗号
            current_pos += len(param_str[current_pos:]) - len(param_str[current_pos:].lstrip()) # 跳空格
            continue
        else:
            try:
                # parse_value 现在返回 TypedID
                final_value = parse_value(raw_value_str)
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


def parse_commands(text: str) -> List[Dict[str, Any]]:
    """从文本中解析所有 @Command 指令。手动查找匹配的右括号。"""
    parsed_commands = []
    # 使用 finditer 在整个文本中查找指令头
    for match in COMMAND_REGEX.finditer(text):
        command_data = match.groupdict()
        command_name = command_data['command'].lower()
        entity_type_str = command_data.get('type')
        entity_id = command_data.get('id')
        has_paren = command_data.get('has_paren') # 是否捕获到了左括号

        # 基本验证 (不变)
        if not entity_type_str or not entity_id: continue
        entity_type = entity_type_str.capitalize()
        if entity_type not in ["Item", "Character", "Place"]: continue

        params_str: Optional[str] = None
        command_end_pos = match.end() # 指令头结束的位置

        # --- 手动查找匹配的右括号 ---
        if has_paren:
            param_start_pos = match.end() # 左括号之后是参数开始的位置
            paren_level = 1 # 从 1 开始，因为已经匹配了一个 (
            in_quotes = None
            current_pos = param_start_pos

            while current_pos < len(text):
                char = text[current_pos]
                prev_char = text[current_pos - 1] if current_pos > param_start_pos else None

                if in_quotes:
                    if char == in_quotes and prev_char != '\\':
                        in_quotes = None
                elif char == '"' or char == "'":
                    in_quotes = char
                elif not in_quotes:
                    if char == '(': paren_level += 1
                    elif char == ')':
                        paren_level -= 1
                        if paren_level == 0: # 找到了匹配的右括号
                            params_str = text[param_start_pos:current_pos] # 提取参数内容
                            command_end_pos = current_pos + 1 # 更新指令结束位置
                            break # 找到，退出循环

                current_pos += 1

            if paren_level != 0: # 如果循环结束还没找到匹配的右括号
                logging.warning(f"指令 '{match.group(0)}...' 括号不匹配，无法解析参数。")
                # 这里可以选择跳过这个指令或按无参数处理
                # 我们按无参数处理
                params_str = None
                command_end_pos = match.end() # 回退到只匹配指令头
        # --- 结束手动查找 ---

        try:
            # 调用手动参数解析器 (不变)
            params = parse_params_string_manual(params_str)
            parsed_command = {
                "command": command_name,
                "entity_type": cast(Literal["Item", "Character", "Place"], entity_type),
                "entity_id": entity_id,
                "params": params,
            }
            parsed_commands.append(parsed_command)
            # 注意：我们不再需要处理重叠，因为 finditer 本身会处理
            # processed_spans.add((match.start(), command_end_pos))
        except Exception as e:
            logging.error(f"解析指令参数时出错 '{match.group(0)}{params_str or ''}...': {e}", exc_info=True)
            raise e
    return parsed_commands