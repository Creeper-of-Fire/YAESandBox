# api/routers/websocket.py
# -*- coding: utf-8 -*-
"""
处理 WebSocket 连接、消息路由和工作流触发。
"""
import json
import logging
from typing import Any, Optional # 导入 Optional

from fastapi import APIRouter, WebSocket, WebSocketDisconnect, Depends

# --- 核心和依赖导入 ---
from core.game_state import GameState
from workflows.ai.base import AbstractAIService
from workflows.processing.parser import parse_commands
# --- Debug 命令执行逻辑导入 ---
from .debug.commands import execute_parsed_commands_internal # 只需内部函数
# --- 依赖注入函数和管理器 ---
from ..dependencies import get_ai_service, get_game_state
from ..websocket_manager import manager # 直接导入实例

router = APIRouter()

# --- 消息处理器 ---

async def handle_echo_message(websocket: WebSocket, data: Any):
    """简单的回显处理器"""
    logging.info(f"收到 Echo 请求，数据: {data}")
    await manager.send_personal_message({
        "type": "echo_response",
        "original_data": data
    }, websocket)

async def handle_workflow_trigger(
        websocket: WebSocket,
        data: Any,
        ai_service: AbstractAIService,
        gs: GameState,
):
    """处理工作流触发请求，调用 AI，流式返回，解析并执行指令。"""
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

    # 模拟 AI 调用准备
    dummy_system_prompt = "你是一个RPG世界生成器。" # 可以根据 workflow_name 调整
    user_input = params.get("userInput", "")
    # TODO: 实际应用中需要从 GameState 或历史记录构建更真实的 history
    dummy_history = [{"role": "user", "content": user_input}] if user_input else []

    full_response_text = ""
    try:
        logging.debug(f"[{request_id}] 调用 AI Service ({ai_service.__class__.__name__})...")
        stream = ai_service.get_completion_stream(dummy_system_prompt, dummy_history)

        async for text_chunk in stream:
            if text_chunk is None:
                logging.info(f"[{request_id}] AI 流结束信号收到。")
                break
            await manager.send_personal_message({
                "type": "workflow_update",
                "request_id": request_id,
                "data": {"type": "stream_chunk", "content": text_chunk}
            }, websocket)
            full_response_text += text_chunk

        logging.debug(f"[{request_id}] AI 完整响应文本:\n{full_response_text}")

        # 解析指令
        parsed_commands = []
        parse_error = None
        if full_response_text:
            try:
                parsed_commands = parse_commands(full_response_text)
                logging.info(f"[{request_id}] 解析出 {len(parsed_commands)} 条指令。")
                # (省略详细日志)
            except Exception as e:
                logging.exception(f"[{request_id}] 解析 AI 响应时出错:")
                parse_error = str(e)

        # 执行指令
        execution_result = None
        execution_error_detail: Optional[str] = None # 用于存储内部错误信息

        if parsed_commands:
            try:
                logging.info(f"[{request_id}] 准备执行 {len(parsed_commands)} 条指令...")
                base_url = str(websocket.base_url) # 需要 base_url 调用 API
                # 调用内部执行函数
                execution_result = await execute_parsed_commands_internal(parsed_commands, base_url, gs)
                logging.info(f"[{request_id}] 指令执行内部结果: {execution_result.message}")
                if execution_result.errors:
                    logging.error(f"[{request_id}] 指令执行期间发生错误:\n{execution_result.errors}")
                    execution_error_detail = execution_result.errors # 记录详细错误供后端日志分析
                # 注意：此时不应将 execution_result.errors 直接发给前端

            except Exception as e:
                logging.exception(f"[{request_id}] 调用指令执行逻辑时发生意外错误:")
                execution_error_detail = f"指令执行意外失败: {e}"

        # 判断最终执行状态
        execution_status: str
        if not parsed_commands:
            execution_status = "no_commands"
        elif execution_result and not execution_error_detail:
            execution_status = "success"
        elif execution_result and execution_error_detail: # 部分成功
             execution_status = "partial_failure"
        else: # 完全失败或执行逻辑出错
            execution_status = "failure"

        # --- 向前端发送最终结果 ---
        # execution_error_message 对前端暂时为 None
        final_result_for_frontend = {
            "full_text": full_response_text,
            "execution_status": execution_status,
            "execution_error_message": None # <--- 新增字段，初始为 None
        }
        await manager.send_personal_message({
            "type": "workflow_complete",
            "request_id": request_id,
            "result": final_result_for_frontend
        }, websocket)
        logging.info(f"[{request_id}] 工作流处理完成，已发送最终结果给前端。 Status: {execution_status}")

    except Exception as e:
        logging.exception(f"[{request_id}] 处理工作流时发生严重错误:")
        await manager.send_personal_message({
            "type": "workflow_error", # 使用特定的错误类型
            "request_id": request_id,
            "error": f"处理工作流 '{workflow_name}' 时发生内部错误。" # 避免暴露过多细节
        }, websocket)


# --- 主 WebSocket 端点 ---
@router.websocket("/ws")
async def websocket_endpoint(
        websocket: WebSocket,
        ai_service: AbstractAIService = Depends(get_ai_service),
        gs: GameState = Depends(get_game_state)
):
    """主 WebSocket 端点，处理连接和消息分发。"""
    await manager.connect(websocket)
    try:
        while True:
            raw_data = await websocket.receive_text()
            logging.debug(f"WebSocket 收到原始文本数据: {raw_data}")

            try:
                message = json.loads(raw_data)
                message_type = message.get("type")

                if message_type == "echo":
                    await handle_echo_message(websocket, message.get("data"))
                elif message_type == "trigger_workflow":
                    # 传递注入的服务和状态
                    await handle_workflow_trigger(websocket, message, ai_service, gs)
                else:
                    logging.warning(f"收到未知类型的 WebSocket 消息: {message_type}")
                    await manager.send_personal_message({
                        "type": "error",
                        "message": f"未知消息类型: {message_type}",
                        "request_id": message.get("request_id") # 尝试包含 request_id
                    }, websocket)

            except json.JSONDecodeError:
                logging.error("收到无法解析的非 JSON WebSocket 消息。")
                await manager.send_personal_message({
                    "type": "error", "message": "无效的 JSON 格式。", "request_id": None
                }, websocket)
            except WebSocketDisconnect:
                # 这个异常通常由 receive_text() 抛出，这里捕获以防万一
                logging.info("客户端主动断开连接 (在消息处理循环中捕获)。")
                break # 退出循环，finally 会处理 disconnect
            except Exception as e:
                # 捕获 handle_xxx 函数或 JSON 解析之外的意外错误
                logging.exception("处理 WebSocket 消息时发生意外错误:")
                req_id = None
                try: # 尝试从原始消息中获取 request_id
                    msg_dict = json.loads(raw_data)
                    req_id = msg_dict.get("request_id")
                except: pass
                await manager.send_personal_message({
                    "type": "error",
                    "message": "处理消息时发生服务器内部错误。", # 简化消息
                    "request_id": req_id
                }, websocket)

    except WebSocketDisconnect:
        logging.info("WebSocket 连接已断开 (在主接收循环外捕获)。")
    except Exception as e:
        # 捕获 websocket.accept() 或循环之外的更严重错误
        logging.exception("WebSocket 接收循环或连接建立出错:")
    finally:
        # 确保无论如何都清理连接
        manager.disconnect(websocket)