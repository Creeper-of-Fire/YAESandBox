# api/routers/commands.py
import logging

import httpx
# --- 添加 Body ---
from fastapi import APIRouter, Depends, HTTPException, status, Request as FastAPIRequest, Body  # <--- 导入 Body
import json
from pydantic.deprecated.json import pydantic_encoder

# 导入 core GameState 和 DTOs/依赖项 (保持)
from core.game_state import GameState
# 导入新的 processing 组件 (保持)
from processing.parser import parse_commands
from processing.translator import translate_all_commands
from ..dependencies import get_game_state
from ..dtos import CommandExecutionRequest, CommandExecutionResponse  # CommandExecutionRequest 只用于旧端点

router = APIRouter()


# 获取 API 基础 URL 的辅助函数 (保持不变)
def get_base_url(request: FastAPIRequest) -> str:
    proto = request.headers.get("x-forwarded-proto", request.url.scheme)
    host = request.headers.get("x-forwarded-host", request.url.netloc)
    return f"{proto}://{host}"


# --- 内部执行逻辑 (提取出来以便复用) ---
async def _execute_translated_commands(
        commands_text: str,
        fastapi_request: FastAPIRequest,
        gs: GameState  # GameState 可能不需要了，但先留着
) -> CommandExecutionResponse:
    """内部函数：解析、翻译并执行原子 API 调用。"""
    logging.info(f"内部执行: 文本长度={len(commands_text)}")

    parsed_commands = []
    api_calls_to_make = []
    total_parsed_commands = 0
    executed_api_calls = 0
    errors_encountered = []

    try:
        # 1. 解析 @Command
        parsed_commands = parse_commands(commands_text)
        total_parsed_commands = len(parsed_commands)
        logging.info(f"从文本中解析出 {total_parsed_commands} 条命令。")

        if not parsed_commands:
            return CommandExecutionResponse(
                message="文本中未找到有效命令。", executed_commands=0, total_commands=0, errors=None
            )

        # 2. 翻译成原子 API 调用描述
        api_calls_to_make = translate_all_commands(parsed_commands)
        total_api_calls = len(api_calls_to_make)
        logging.info(f"翻译为 {total_api_calls} 个原子 API 调用。")

        if not api_calls_to_make:
            return CommandExecutionResponse(
                message="指令无法翻译或无需执行任何操作。", executed_commands=0, total_commands=total_parsed_commands, errors=None
            )

        # 3. 执行原子 API 调用 (内部 HTTP 请求)
        base_url = get_base_url(fastapi_request)
        logging.info(f"将向 Base URL: {base_url} 发送内部 API 请求。")

        # --- 定义请求头 ---
        headers = {"Content-Type": "application/json"}

        async with httpx.AsyncClient(base_url=base_url, timeout=10.0) as client:
            for i, call_desc in enumerate(api_calls_to_make):
                method = call_desc.get("method")
                path = call_desc.get("path")
                json_body = call_desc.get("json_body")

                if not method or not path:
                    logging.error(f"无效的 API 调用描述 #{i + 1}: {call_desc}")
                    errors_encountered.append(f"内部错误：无效的 API 调用描述 {i + 1}")
                    continue

                # --- 新增：使用 pydantic_encoder 转换 body 为 JSON 字符串 ---
                try:
                    json_string = json.dumps(json_body, default=pydantic_encoder)
                except Exception as dump_error:
                    error_detail = f"序列化 API 调用 #{i + 1} 的 Body 时出错: {dump_error}"
                    logging.error(error_detail, exc_info=True)
                    errors_encountered.append(error_detail)
                    logging.warning("遇到序列化错误，停止执行后续调用。")
                    break
                # ---

                logging.debug(f"执行 API 调用 #{i + 1}/{total_api_calls}: {method} {path} Body: {json_body}")
                try:
                    # --- 使用 content 参数发送 JSON 字符串，并设置 headers ---
                    response = await client.request(
                        method=method,
                        url=path,
                        content=json_string,  # <--- 使用 content
                        headers=headers  # <--- 传递 headers
                    )
                    # ---
                    response.raise_for_status()
                    logging.debug(f"API 调用 #{i + 1} 成功: Status {response.status_code}")
                    executed_api_calls += 1
                except httpx.HTTPStatusError as e:
                    error_detail = f"API 调用 {method} {path} 失败: Status {e.response.status_code}"
                    try:
                        error_detail += f" - Detail: {e.response.json().get('detail', e.response.text)}"
                    except:
                        error_detail += f" - Response: {e.response.text}"
                    logging.error(error_detail, exc_info=False)
                    errors_encountered.append(error_detail)
                    logging.warning("遇到 API 调用错误，停止执行后续调用。")
                    break
                except httpx.RequestError as e:
                    error_detail = f"网络错误调用 {method} {path}: {e}"
                    logging.error(error_detail, exc_info=True)
                    errors_encountered.append(error_detail)
                    logging.warning("遇到网络错误，停止执行后续调用。")
                    break
                except Exception as e:
                    error_detail = f"执行 API 调用 {method} {path} 时发生意外错误: {e}"
                    logging.exception(error_detail)
                    errors_encountered.append(error_detail)
                    logging.warning("遇到意外错误，停止执行后续调用。")
                    break

        # 4. 构建最终响应
        final_message = f"尝试执行 {total_api_calls} 个原子操作 (来自 {total_parsed_commands} 条指令)。成功 {executed_api_calls} 个。"
        if errors_encountered: final_message += f" 遇到 {len(errors_encountered)} 个错误。"

        logging.info(f"API 响应 (Atomic): {final_message}")
        return CommandExecutionResponse(
            message=final_message,
            executed_commands=executed_api_calls,
            total_commands=total_api_calls,
            errors="\n".join(errors_encountered) if errors_encountered else None
        )

    except (ValueError, TypeError) as e:  # 解析或翻译期间的预期错误
        logging.error(f"命令解析或翻译失败: {e}", exc_info=True)
        # 直接抛出 HTTPException，让 FastAPI 处理
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"命令解析/翻译失败: {e}"
        )
    except Exception as e:  # 意外错误
        logging.exception("处理命令时发生意外错误:")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"内部服务器错误: {e}"
        )


# --- 标准 JSON 端点 ---
@router.post(
    "/commands/execute",
    response_model=CommandExecutionResponse,
    summary="执行文本中的命令 (标准 JSON)",
    description="解析 @Command 指令 (从 JSON 请求体的 'text' 字段)，翻译成原子 API 调用，并在内部执行。"
)
async def execute_text_commands_via_atomic_json(  # 重命名以区分
        fastapi_request: FastAPIRequest,
        request_body: CommandExecutionRequest,  # 接收 JSON
        gs: GameState = Depends(get_game_state)
):
    """处理 JSON 请求，调用内部执行逻辑。"""
    # 直接调用内部函数处理
    return await _execute_translated_commands(request_body.text, fastapi_request, gs)


# --- 新增：纯文本端点 ---
@router.post(
    "/commands/execute_plain",
    response_model=CommandExecutionResponse,
    summary="执行纯文本中的命令 (开发便捷)",
    description="直接解析请求体中的纯文本 @Command 指令，翻译成原子 API 调用，并在内部执行。方便测试。"
)
async def execute_plain_text_commands_via_atomic(  # 新函数名
        fastapi_request: FastAPIRequest,
        # --- 使用 Body 接收纯文本 ---
        commands_text: str = Body(..., media_type="text/plain"),  # <--- 关键改动
        gs: GameState = Depends(get_game_state)
):
    """处理纯文本请求，调用内部执行逻辑。"""
    # 直接调用内部函数处理
    return await _execute_translated_commands(commands_text, fastapi_request, gs)
