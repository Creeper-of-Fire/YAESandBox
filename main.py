# main.py
import logging
import re
from typing import List, Dict, Optional, Any

# --- 导入自定义模块 ---
from game_state import GameState, AnyEntity
from parser import parse_commands
from ai_service import AIService
import prompts
import user_commands # 导入新的用户命令处理模块

# --- 配置 (保持不变) ---
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

# --- 辅助函数 (display_stream 保持不变) ---
def display_stream(stream_iterator):
    full_response = ""
    print("\nAI: ", end="")
    if stream_iterator:
        try:
            for chunk in stream_iterator:
                delta_content = chunk.choices[0].delta.content
                if delta_content:
                    print(delta_content, end="", flush=True)
                    full_response += delta_content
            print()
            return full_response
        except Exception as e:
            logging.error(f"处理 AI 响应流时出错: {e}")
            print(f"\n[错误：处理 AI 响应流时出错: {e}]")
            return None
    else:
        print("[错误：未能获取 AI 响应流]")
        return None

# --- 新增：指令排序函数 ---
def sort_commands_for_execution(commands: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    """
    对解析出的指令列表进行排序，以优化执行顺序。
    优先级: Create Place > Create Character/Item > Modify/Transfer > Destroy
    相同优先级的指令保持原始相对顺序。
    """
    def get_priority(command_data: Dict[str, Any]) -> int:
        command = command_data.get("command", "").lower()
        entity_type = command_data.get("entity_type", "")

        if command == "create":
            if entity_type == "Place":
                return 0  # 最高优先级
            elif entity_type  == "Character":
                return 1  # 次高优先级
            elif entity_type  == "Item":
                return 2  # 第三高优先级
        elif command in ["modify", "transfer"]:
            return 3  # 操作指令
        elif command == "destroy":
            return 4  # 最后执行销毁
        return 99 # 未知或其他指令放最后

    # 使用稳定排序 sorted，保留同优先级指令的原始相对顺序
    sorted_commands = sorted(commands, key=get_priority)

    # 仅在顺序确实发生改变时记录日志，避免干扰
    original_order_summary = [(c.get('command'), c.get('entity_type')) for c in commands]
    sorted_order_summary = [(c.get('command'), c.get('entity_type')) for c in sorted_commands]

    if original_order_summary != sorted_order_summary:
        logging.info("指令已重新排序以确保创建优先:")
        for i, cmd in enumerate(sorted_commands):
            logging.info(f"  排序后 #{i+1}: {cmd.get('command')} {cmd.get('entity_type','')} {cmd.get('entity_id','')}")
            # logging.debug(f"    Raw: {cmd}") # 更详细的调试信息

    return sorted_commands


# --- 指令执行函数 (保持在 main.py 或移到 executor.py) ---
def execute_parsed_commands(parsed_commands_list: List[Dict], game_state: GameState):
    """执行从文本中解析出的指令列表"""
    if not parsed_commands_list: return
    logging.info(f"准备执行 {len(parsed_commands_list)} 条指令...")
    for cmd_data in parsed_commands_list:
        command = cmd_data["command"]; entity_type = cmd_data["entity_type"]; entity_id = cmd_data["entity_id"]
        params = cmd_data.get("params", {}); mode = cmd_data.get("mode")
        try:
            if command == "create": game_state.execute_create(entity_type, entity_id, params)
            elif command == "modify": game_state.execute_modify(entity_type, entity_id, params) # type: ignore
            elif command == "destroy": game_state.execute_destroy(entity_type, entity_id)
            elif command == "transfer":
                target_id = params.get("target")
                if target_id: game_state.execute_transfer(entity_type, entity_id, target_id) # type: ignore
                else: logging.warning(f"跳过 Transfer 指令（缺少 target）: {cmd_data}")
        except (ValueError, TypeError, KeyError) as e:
            logging.error(f"执行指令 {cmd_data} 时出错: {e}")
            print(f"[系统警告：执行指令 '{command} {entity_id}' 时出错: {e}]")
        except Exception as e:
            logging.exception(f"执行指令 {cmd_data} 时发生意外错误:")
            print(f"[系统严重错误：执行指令 '{command} {entity_id}' 时崩溃。请检查日志。]")
            raise e
    logging.info("所有指令执行完毕。")


game_state = GameState()
ai_service = AIService()

# --- 主游戏循环 (更新) ---
def game_loop():
    logging.info("开始初始化游戏...")


    if not ai_service.client:
        logging.error("AI 服务未能初始化，无法启动游戏。请检查 API Key。")
        return

    conversation_history: List[Dict[str, str]] = []

    print("欢迎来到 AI 驱动的 RPG 世界！")
    print("输入你的行动或对话。")
    print("命令: /quit, /state, /focus <id>, /unfocus <id>, /clearfocus, /showfocus")
    print("       /@<AI指令> (例如: /@Create Item apple (name=\"苹果\"))")

    while True:
        try:
            current_focus_ids = game_state.get_current_focus()
            focus_display = ", ".join(current_focus_ids) if current_focus_ids else "无"
            prompt_prefix = f"[焦点: {focus_display}] 你: "

            user_input = input(f"\n{prompt_prefix}")
            user_input_strip = user_input.strip()

            if not user_input_strip:
                continue

            # --- 处理用户命令 ---
            is_user_command = False
            if user_input_strip.startswith('/'):
                # 将指令执行函数传递给处理器
                is_user_command = user_commands.handle_user_command(
                    user_input_strip,
                    game_state,
                    execute_parsed_commands # 传递执行函数
                )
                if user_input_strip.lower() == '/quit': # 特殊处理退出命令
                    break

            if is_user_command:
                continue # 如果是已处理的用户命令，则跳过 AI 交互

            # --- 处理与 AI 的交互 ---
            conversation_history.append({"role": "user", "content": user_input_strip})

            system_prompt = prompts.get_system_prompt(game_state)
            cleaned_history = prompts.clean_history_for_ai(conversation_history)

            ai_response_stream = ai_service.get_completion_stream(system_prompt, cleaned_history)
            ai_full_response = display_stream(ai_response_stream)

            if ai_full_response is None:
                print("[系统提示：与 AI 的通信出现问题。]")
                conversation_history.pop()
                continue

            conversation_history.append({"role": "assistant", "content": ai_full_response})

            # --- 解析并排序 AI 指令（只有AI会搞不清楚顺序，人类不会） ---
            parsed_ai_commands = parse_commands(ai_full_response)
            if parsed_ai_commands:
                # 在执行前对 AI 的指令进行排序
                sorted_ai_commands = sort_commands_for_execution(parsed_ai_commands)
                # 使用排序后的指令列表执行
                execute_parsed_commands(sorted_ai_commands, game_state)
            # else: 无指令则不执行

        except KeyboardInterrupt: print("\n检测到中断，正在退出..."); break
        except EOFError: print("\n输入流结束，正在退出..."); break
        except Exception as e:
            logging.exception("主游戏循环发生意外错误:")
            print(f"\n[系统严重错误：发生意外崩溃！错误信息: {e}]")
            raise

# --- 程序入口 (保持不变) ---
if __name__ == "__main__":
    game_loop()