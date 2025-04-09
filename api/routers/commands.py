# api/routers/commands.py
import logging

import httpx
from fastapi import APIRouter, Depends, HTTPException, status, Request as FastAPIRequest, Body
import json
from pydantic_core import to_jsonable_python # Pydantic v2 推荐

# 导入 core GameState 和 DTOs/依赖项
from core.game_state import GameState
# 导入新的 processing 组件
from processing.parser import parse_commands
from processing.translator import translate_all_commands # <--- 签名已改变
from ..dependencies import get_game_state
from ..dtos import CommandExecutionRequest, CommandExecutionResponse

router = APIRouter()


# 获取 API 基础 URL 的辅助函数 (保持不变)
def get_base_url(request: FastAPIRequest) -> str:
    proto = request.headers.get("x-forwarded-proto", request.url.scheme)
    host = request.headers.get("x-forwarded-host", request.url.netloc)
    return f"{proto}://{host}"


# --- 内部执行逻辑 (修改：接收 GameState, 传递 world 给 translator) ---
async def _execute_translated_commands(
        commands_text: str,
        fastapi_request: FastAPIRequest,
        gs: GameState # <--- 接收 GameState
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

        # 2. 翻译成原子 API 调用描述 (传递 world)
        # <--- 修改：传递 gs.world ---
        api_calls_to_make = translate_all_commands(parsed_commands, gs.world)
        # <--- 结束修改 ---
        total_api_calls = len(api_calls_to_make)
        logging.info(f"翻译为 {total_api_calls} 个原子 API 调用。")

        if not api_calls_to_make:
            return CommandExecutionResponse(
                message="指令无需执行任何操作或翻译失败。", # 修改消息
                executed_commands=0,
                total_commands=total_parsed_commands, # 返回解析出的指令数
                errors=None # 可以考虑收集翻译阶段的警告/错误
            )

        # 3. 执行原子 API 调用 (内部 HTTP 请求)
        base_url = get_base_url(fastapi_request)
        logging.info(f"将向 Base URL: {base_url} 发送内部 API 请求。")

        headers = {"Content-Type": "application/json"}

        async with httpx.AsyncClient(base_url=base_url, timeout=10.0) as client:
            for i, call_desc in enumerate(api_calls_to_make):
                method = call_desc.get("method")
                path = call_desc.get("path")
                json_body = call_desc.get("json_body") # 可能包含 TypedID

                if not method or not path:
                    logging.error(f"无效的 API 调用描述 #{i + 1}: {call_desc}")
                    errors_encountered.append(f"内部错误：无效的 API 调用描述 {i + 1}")
                    continue

                # --- 使用 Pydantic v2 的 to_jsonable_python 配合 json.dumps ---
                try:
                    # 这个函数能处理 Pydantic 模型（如 TypedID）的序列化
                    json_compatible_body = to_jsonable_python(json_body)
                    json_string = json.dumps(json_compatible_body)
                except Exception as dump_error:
                    error_detail = f"序列化 API 调用 #{i + 1} 的 Body 时出错: {dump_error}"
                    logging.error(error_detail, exc_info=True)
                    errors_encountered.append(error_detail)
                    logging.warning("遇到序列化错误，停止执行后续调用。")
                    break
                # ---

                logging.debug(f"执行 API 调用 #{i + 1}/{total_api_calls}: {method} {path} Body: {json_string}") # Log string
                try:
                    response = await client.request(
                        method=method,
                        url=path,
                        content=json_string,
                        headers=headers
                    )
                    response.raise_for_status() # 检查 4xx/5xx 错误
                    logging.debug(f"API 调用 #{i + 1} 成功: Status {response.status_code}")
                    executed_api_calls += 1
                except httpx.HTTPStatusError as e:
                    error_detail = f"API 调用 {method} {path} 失败: Status {e.response.status_code}"
                    try:
                        error_detail += f" - Detail: {e.response.json().get('detail', e.response.text)}"
                    except: # JSON 解析失败或无 detail
                        error_detail += f" - Response: {e.response.text}"
                    logging.error(error_detail, exc_info=False) # 只记录关键信息
                    errors_encountered.append(error_detail)
                    logging.warning("遇到 API 调用错误，停止执行后续调用。")
                    break # 停止执行后续调用
                except httpx.RequestError as e: # 连接错误、超时等
                    error_detail = f"网络错误调用 {method} {path}: {e}"
                    logging.error(error_detail, exc_info=True)
                    errors_encountered.append(error_detail)
                    logging.warning("遇到网络错误，停止执行后续调用。")
                    break # 停止执行
                except Exception as e: # 其他意外错误
                    error_detail = f"执行 API 调用 {method} {path} 时发生意外错误: {e}"
                    logging.exception(error_detail) # 记录完整堆栈
                    errors_encountered.append(error_detail)
                    logging.warning("遇到意外错误，停止执行后续调用。")
                    break # 停止执行

        # 4. 构建最终响应
        final_message = f"尝试执行 {total_api_calls} 个原子操作 (来自 {total_parsed_commands} 条指令)。成功 {executed_api_calls} 个。"
        if errors_encountered: final_message += f" 遇到 {len(errors_encountered)} 个错误。"

        logging.info(f"API 响应 (Commands): {final_message}") # 区分日志来源
        return CommandExecutionResponse(
            message=final_message,
            executed_commands=executed_api_calls, # 返回成功执行的 API 调用数
            total_commands=total_api_calls, # 返回尝试执行的 API 调用总数
            errors="\n".join(errors_encountered) if errors_encountered else None
        )

    except (ValueError, TypeError) as e:  # 解析或翻译期间的预期错误
        logging.error(f"命令解析或翻译失败: {e}", exc_info=True)
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


# --- 标准 JSON 端点 (修改：传递 gs) ---
@router.post(
    "/commands/execute",
    response_model=CommandExecutionResponse,
    summary="执行文本中的命令 (标准 JSON)",
    description="解析 @Command 指令 (从 JSON 请求体的 'text' 字段)，翻译成原子 API 调用，并在内部执行。"
)
async def execute_text_commands_via_atomic_json(
        fastapi_request: FastAPIRequest,
        request_body: CommandExecutionRequest,
        gs: GameState = Depends(get_game_state) # <--- 注入 gs
):
    """处理 JSON 请求，调用内部执行逻辑。"""
    # <--- 修改：传递 gs ---
    return await _execute_translated_commands(request_body.text, fastapi_request, gs)


# --- 纯文本端点 (修改：传递 gs) ---
@router.post(
    "/commands/execute_plain",
    response_model=CommandExecutionResponse,
    summary="执行纯文本中的命令 (开发便捷)",
    description="直接解析请求体中的纯文本 @Command 指令，翻译成原子 API 调用，并在内部执行。方便测试。"
)
async def execute_plain_text_commands_via_atomic(
        fastapi_request: FastAPIRequest,
        commands_text: str = Body(..., media_type="text/plain"),
        gs: GameState = Depends(get_game_state) # <--- 注入 gs
):
    """处理纯文本请求，调用内部执行逻辑。"""
    # <--- 修改：传递 gs ---
    return await _execute_translated_commands(commands_text, fastapi_request, gs)