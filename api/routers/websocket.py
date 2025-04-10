# api/routers/websocket.py
import json  # 用于解析接收到的 JSON 消息
import logging
from typing import Any

from fastapi import APIRouter, WebSocket, WebSocketDisconnect, Depends

from core.game_state import GameState
from workflow.ai.base import AbstractAIService
from workflow.processing.parser import parse_commands
from .debug.commands import execute_parsed_commands_internal, get_base_url
from ..dependencies import get_ai_service, get_game_state
# 导入连接管理器实例
from ..websocket_manager import manager


# --- 导入未来的 WorkflowEngine (现在先用占位符) ---
# from core.workflow_engine import WorkflowEngine # 假设未来有这个
# from ..dependencies import get_workflow_engine # 假设未来有这个依赖

# --- 临时的 Echo 处理函数 ---
async def handle_echo_message(websocket: WebSocket, data: Any):
    """简单的回显处理器"""
    logging.info(f"收到 Echo 请求，数据: {data}")
    await manager.send_personal_message({
        "type": "echo_response",
        "original_data": data
    }, websocket)


# --- 再次修改：处理工作流触发请求，逻辑大幅简化 ---
async def handle_workflow_trigger(
        websocket: WebSocket,
        data: Any,
        ai_service: AbstractAIService,
        gs: GameState,  # <--- 注入 GameState
):
    """处理工作流触发请求，调用 AI 服务，流式返回文本，并解析完整文本。"""
    workflow_name = data.get("workflow_name")
    params = data.get("params", {})
    request_id = data.get("request_id")

    if not request_id:
        logging.error("收到工作流触发请求，但缺少 request_id。")
        await manager.send_personal_message({
            "type": "error",
            "message": "请求缺少 'request_id'。",
            "request_id": None
        }, websocket)
        return

    logging.info(f"[{request_id}] 处理工作流: Name='{workflow_name}', Params='{params}'")

    # --- 准备调用 AI Service ---
    dummy_system_prompt = "你是一个笨蛋 AI 助手 DeepFake。"
    user_input = params.get("userInput", "")
    dummy_history = [{"role": "user", "content": user_input}] if user_input else []

    # --- 用于拼接完整响应 ---
    full_response_text = ""  # 用于拼接

    try:
        logging.debug(f"[{request_id}] 调用 AI Service ({ai_service.__class__.__name__})...")

        # --- 现在 stream 直接 yield Optional[str] ---
        stream = ai_service.get_completion_stream(dummy_system_prompt, dummy_history)

        async for text_chunk in stream:
            # --- 检查是否是结束信号 ---
            if text_chunk is None:
                logging.info(f"[{request_id}] AI 流结束信号 (None) 收到。")
                break  # 跳出循环

            # --- 如果不是 None，那它就是文本块 ---
            # 1. 实时发送给前端
            await manager.send_personal_message({
                "type": "workflow_update",
                "request_id": request_id,
                "data": {
                    "type": "stream_chunk",
                    "content": text_chunk  # 直接发送文本块
                }
            }, websocket)

            # 2. 拼接完整文本
            full_response_text += text_chunk

            # --- 流处理完毕 (无论是 break 还是迭代结束) ---
        logging.debug(f"[{request_id}] AI 完整响应文本:\n{full_response_text}")

        # --- 解析指令 (逻辑不变) ---
        parsed_commands = []
        parse_error = None
        if full_response_text:  # 只在有文本时尝试解析
            try:
                parsed_commands = parse_commands(full_response_text)
                logging.info(f"[{request_id}] 从 AI 响应中解析出 {len(parsed_commands)} 条指令:")
                for i, cmd in enumerate(parsed_commands):
                    # 使用更结构化的日志记录指令详情可能更好，但现在先这样
                    logging.info(
                        f"  指令 #{i + 1}: Type={cmd.get('command')}, Target={cmd.get('entity_type')}:{cmd.get('entity_id')}, Params={cmd.get('params')}")
            except Exception as e:
                logging.exception(f"[{request_id}] 解析 AI 响应文本时出错:")
                parse_error = str(e)

        execution_result = None
        execution_error = None

        # --- 执行指令 ---
        if parsed_commands:
            try:
                logging.info(f"[{request_id}] 准备执行 {len(parsed_commands)} 条指令...")
                # 获取 base_url (需要注入 Request)
                base_url = str(websocket.base_url)
                # 调用内部执行函数
                execution_result = await execute_parsed_commands_internal(parsed_commands, base_url, gs)
                logging.info(f"[{request_id}] 指令执行结果: {execution_result.message}")
                if execution_result.errors:
                    logging.error(f"[{request_id}] 指令执行期间发生错误:\n{execution_result.errors}")
                    execution_error = execution_result.errors  # 记录错误

            except Exception as e:
                logging.exception(f"[{request_id}] 调用指令执行逻辑时发生意外错误:")
                execution_error = f"指令执行意外失败: {e}"

        # --- 向前端发送最终结果 (包含执行状态，但隐藏细节) ---
        final_result_for_frontend = {
            "full_text": full_response_text,
            "execution_status": "success" if execution_result and not execution_error else "partial_failure" if execution_result else "failure" if execution_error else "no_commands",
            # 可以考虑加一个非常通用的错误消息，如果不希望暴露细节
            # "error_message": "执行部分指令时遇到问题。" if execution_error else None
        }
        await manager.send_personal_message({
            "type": "workflow_complete",
            "request_id": request_id,
            "result": final_result_for_frontend
        }, websocket)
        logging.info(f"[{request_id}] 工作流处理完成，已发送最终结果给前端。")

    except Exception as e:
        # 捕获调用 AI Service 或流处理过程中的其他异常
        logging.exception(f"[{request_id}] 处理工作流时发生严重错误:")
        await manager.send_personal_message({
            "type": "workflow_error",
            "request_id": request_id,
            "error": f"处理工作流 '{workflow_name}' 时发生内部错误: {e}"
        }, websocket)


# --- 主 WebSocket 端点 ---
router = APIRouter()


@router.websocket("/ws")
async def websocket_endpoint(
        websocket: WebSocket,
        ai_service: AbstractAIService = Depends(get_ai_service),
        gs: GameState = Depends(get_game_state)  # <--- 注入 GameState
):
    await manager.connect(websocket)
    try:
        while True:
            raw_data = await websocket.receive_text()
            logging.debug(f"收到原始 WebSocket 文本数据: {raw_data}")

            try:
                message = json.loads(raw_data)
                message_type = message.get("type")

                if message_type == "echo":
                    await handle_echo_message(websocket, message.get("data"))
                elif message_type == "trigger_workflow":
                    # 将注入的 ai_service 传递给处理函数
                    await handle_workflow_trigger(websocket, message, ai_service, gs)
                else:
                    logging.warning(f"收到未知类型的 WebSocket 消息: {message_type}")
                    await manager.send_personal_message({
                        "type": "error",
                        "message": f"未知消息类型: {message_type}"
                    }, websocket)

            except json.JSONDecodeError:
                logging.error("收到无法解析的非 JSON WebSocket 消息。")
                await manager.send_personal_message({
                    "type": "error",
                    "message": "无效的 JSON 格式。"
                }, websocket)
            except WebSocketDisconnect:
                logging.info("客户端主动断开连接 (在消息处理中捕获)。")
                manager.disconnect(websocket)
                break
            except Exception as e:
                logging.exception("处理 WebSocket 消息时发生意外错误:")
                await manager.send_personal_message({
                    "type": "error",
                    "message": f"处理消息时发生服务器内部错误: {e}"
                }, websocket)

    except WebSocketDisconnect:
        logging.info("WebSocket 连接已断开。")
    except Exception as e:
        logging.exception("WebSocket 接收循环出错:")
    finally:
        manager.disconnect(websocket)
