# user_commands.py
import logging
import re
from typing import List, Dict

from game_state import GameState
from parser import parse_commands # 需要导入 parse_commands 来处理 /@ 指令

# 需要一个方法来执行解析后的指令，可以从 main 导入或在这里重新实现/调用
# 为了简单起见，我们假设有一个 execute_parsed_commands 函数可用
# from main import execute_parsed_commands # 避免循环导入，更好的方式是把 execute_parsed_commands 也移到独立模块

# --- 临时的执行函数占位符 ---
# 在实际应用中，execute_parsed_commands 应该放在 game_state 或独立的 executor 模块
def _placeholder_execute_parsed_commands(parsed_commands_list: List[Dict], game_state: GameState):
     """临时占位符，实际应调用真正的执行逻辑"""
     logging.warning("调用了占位符 _placeholder_execute_parsed_commands。请确保主程序提供了正确的执行函数。")
     # 这里可以简单打印，或者引发一个错误提醒需要连接真正的执行器
     print(f"[调试] 模拟执行指令: {parsed_commands_list}")
     # 在实际集成中，这一行应该被替换为对真正执行函数的调用，例如从 main 传入
     # game_state.execute_command_list(parsed_commands_list) # 假设 GameState 有这样一个方法


def handle_user_command(command_line: str, game_state: GameState, command_executor) -> bool:
    """
    处理用户输入的系统命令 (以 / 开头)。

    Args:
        command_line (str): 用户输入的完整命令字符串 (包括 /)。
        game_state (GameState): 当前游戏状态对象。
        command_executor (callable): 用于执行解析出的 @ 指令的函数。

    Returns:
        bool: 如果输入是已处理的用户命令，则返回 True，否则返回 False。
    """
    command_line = command_line.strip()
    parts = command_line.split(maxsplit=1) # 分割命令和参数
    command = parts[0].lower()
    args_str = parts[1] if len(parts) > 1 else ""

    if command == "/quit":
        print("再见！")
        # 返回 True 通常意味着主循环应该退出，但这由主循环决定
        # 这里我们返回 True 表示这是一个已处理的命令，主循环看到 /quit 会自己处理退出
        return True # 是一个系统命令
    elif command == "/state":
        print("\n--- 当前世界状态摘要 (YAML, 基于焦点) ---")
        yaml_summary = game_state.get_state_summary()
        print(yaml_summary if yaml_summary else "无法生成摘要或无焦点。")
        print("------------------------------------------")
        return True # 是一个系统命令
    elif command == "/showfocus":
        current_focus_ids = game_state.get_current_focus()
        focus_display = ", ".join(current_focus_ids) if current_focus_ids else "无"
        print(f"当前焦点: {focus_display}")
        return True
    elif command == "/clearfocus":
        game_state.clear_focus()
        print("已清除所有焦点。")
        return True
    elif command == "/focus":
        if not args_str:
            print("用法: /focus <entity_id_1> [entity_id_2] ...")
            return True
        ids_to_focus = re.split(r'[,\s]+', args_str)
        added_count = 0
        for entity_id in ids_to_focus:
            if entity_id:
                if game_state.add_focus(entity_id):
                    added_count += 1
        print(f"尝试添加 {len(ids_to_focus)} 个焦点，成功 {added_count} 个。")
        return True
    elif command == "/unfocus":
        if not args_str:
            print("用法: /unfocus <entity_id_1> [entity_id_2] ...")
            return True
        ids_to_unfocus = re.split(r'[,\s]+', args_str)
        removed_count = 0
        for entity_id in ids_to_unfocus:
            if entity_id:
                if game_state.remove_focus(entity_id):
                    removed_count += 1
        print(f"尝试移除 {len(ids_to_unfocus)} 个焦点，成功 {removed_count} 个。")
        return True
    elif command.startswith("/@"): # 处理用户手动输入的 AI 指令
        ai_command_text = command_line[1:] # 移除开头的 /
        print(f"[手动执行 AI 指令: {ai_command_text}]")
        parsed_commands = parse_commands(ai_command_text)
        if parsed_commands:
            # 调用执行器执行这些指令
            command_executor(parsed_commands, game_state)
        else:
            print("[警告] 未能解析手动输入的 AI 指令。")
        return True # 是一个已处理的命令 (即使解析失败)
    else:
        # 如果命令不是以上任何一种，则不是有效的用户命令
        # 可以选择打印未知命令消息，或者静默返回 False
        # print(f"未知命令: {command}")
        return False # 不是一个有效的、需要在此处理的系统命令