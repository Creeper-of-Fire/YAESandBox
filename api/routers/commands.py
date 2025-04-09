# api/routers/commands.py
import logging
import httpx # <--- 导入 httpx
from fastapi import APIRouter, Depends, HTTPException, status, Request as FastAPIRequest # 导入 Request

# 导入 core GameState 和 DTOs/依赖项 (保持)
from core.game_state import GameState
from ..dtos import CommandExecutionRequest, CommandExecutionResponse
from ..dependencies import get_game_state

# 导入新的 processing 组件
from processing.parser import parse_commands
from processing.translator import translate_all_commands # <--- 导入翻译器

router = APIRouter()

# 获取 API 基础 URL 的辅助函数 (需要 Request 对象)
def get_base_url(request: FastAPIRequest) -> str:
    # 优先使用 X-Forwarded-Proto 和 X-Forwarded-Host (如果应用部署在反向代理后)
    proto = request.headers.get("x-forwarded-proto", request.url.scheme)
    host = request.headers.get("x-forwarded-host", request.url.netloc)
    return f"{proto}://{host}"

@router.post(
    "/commands/execute",
    response_model=CommandExecutionResponse,
    summary="执行文本中的命令 (通过原子 API)",
    description="解析 @Command 指令，翻译成原子 API 调用，并在内部执行这些调用来修改状态。"
)
async def execute_text_commands_via_atomic( # <-- 函数名改变以示区别
    fastapi_request: FastAPIRequest, # <--- 注入 FastAPI Request 获取 base_url
    request_body: CommandExecutionRequest, # <--- 请求体现在作为单独参数
    gs: GameState = Depends(get_game_state) # 注入 GameState (可能不再直接需要，但保留可能有用)
):
    """解析 @Command，翻译，并调用原子 API 执行。"""
    logging.info(f"API 请求 (Atomic): 执行命令，文本长度={len(request_body.text)}")

    parsed_commands = []
    api_calls_to_make = []
    total_parsed_commands = 0
    executed_api_calls = 0
    errors_encountered = []

    try:
        # 1. 解析 @Command
        parsed_commands = parse_commands(request_body.text)
        total_parsed_commands = len(parsed_commands)
        logging.info(f"从文本中解析出 {total_parsed_commands} 条命令。")

        if not parsed_commands:
            return CommandExecutionResponse(
                message="文本中未找到有效命令。",
                executed_commands=0, # 指令执行数为 0
                total_commands=0,
                errors=None
            )

        # 2. 翻译成原子 API 调用描述
        api_calls_to_make = translate_all_commands(parsed_commands)
        total_api_calls = len(api_calls_to_make)
        logging.info(f"翻译为 {total_api_calls} 个原子 API 调用。")

        if not api_calls_to_make:
             # 翻译后没有 API 调用（可能是空指令或无法翻译）
             return CommandExecutionResponse(
                 message="指令无法翻译或无需执行任何操作。",
                 executed_commands=0, # 指令执行数为 0
                 total_commands=total_parsed_commands,
                 errors=None
             )

        # 3. 执行原子 API 调用 (内部 HTTP 请求)
        base_url = get_base_url(fastapi_request) # 获取 API 的基础 URL
        logging.info(f"将向 Base URL: {base_url} 发送内部 API 请求。")

        async with httpx.AsyncClient(base_url=base_url, timeout=10.0) as client: # 使用基础 URL
             for i, call_desc in enumerate(api_calls_to_make):
                 method = call_desc.get("method")
                 path = call_desc.get("path")
                 json_body = call_desc.get("json_body")

                 if not method or not path:
                     logging.error(f"无效的 API 调用描述 #{i+1}: {call_desc}")
                     errors_encountered.append(f"内部错误：无效的 API 调用描述 {i+1}")
                     continue # 跳过这个错误的调用

                 logging.debug(f"执行 API 调用 #{i+1}/{total_api_calls}: {method} {path} Body: {json_body}")

                 try:
                     response = await client.request(
                         method=method,
                         url=path, # httpx 会自动拼接 base_url 和 path
                         json=json_body # 发送 JSON body
                         # 可以在这里添加 headers，例如认证信息（如果需要）
                     )

                     # 检查响应状态码
                     response.raise_for_status() # 如果状态码是 4xx 或 5xx，会抛出 httpx.HTTPStatusError

                     logging.debug(f"API 调用 #{i+1} 成功: Status {response.status_code}")
                     executed_api_calls += 1

                 except httpx.HTTPStatusError as e:
                     # API 返回了错误状态码
                     error_detail = f"API 调用 {method} {path} 失败: Status {e.response.status_code}"
                     try:
                         # 尝试解析响应体中的错误详情
                         error_detail += f" - Detail: {e.response.json().get('detail', e.response.text)}"
                     except: # 解析失败则使用原始文本
                         error_detail += f" - Response: {e.response.text}"
                     logging.error(error_detail, exc_info=False) # 不需要完整堆栈
                     errors_encountered.append(error_detail)
                     # --- 失败策略：遇到第一个错误就停止？还是继续尝试？ ---
                     # 策略 A: 停止执行后续 API 调用
                     logging.warning("遇到 API 调用错误，停止执行后续调用。")
                     break
                     # 策略 B: 继续执行 (记录错误，让后续调用有机会执行)
                     # logging.warning("遇到 API 调用错误，记录并继续执行后续调用。")
                     # continue

                 except httpx.RequestError as e:
                     # 连接错误、超时等网络问题
                     error_detail = f"网络错误调用 {method} {path}: {e}"
                     logging.error(error_detail, exc_info=True)
                     errors_encountered.append(error_detail)
                     # 网络错误通常应该停止
                     logging.warning("遇到网络错误，停止执行后续调用。")
                     break

                 except Exception as e:
                     # 其他意外错误
                     error_detail = f"执行 API 调用 {method} {path} 时发生意外错误: {e}"
                     logging.exception(error_detail) # 记录完整堆栈
                     errors_encountered.append(error_detail)
                     # 意外错误也应该停止
                     logging.warning("遇到意外错误，停止执行后续调用。")
                     break

        # 4. 构建最终响应
        final_message = f"尝试执行 {total_api_calls} 个原子操作 (来自 {total_parsed_commands} 条指令)。成功 {executed_api_calls} 个。"
        if errors_encountered:
             final_message += f" 遇到 {len(errors_encountered)} 个错误。"

        logging.info(f"API 响应 (Atomic): {final_message}")
        return CommandExecutionResponse(
            message=final_message,
            executed_commands=executed_api_calls, # 返回成功执行的 API 调用数
            total_commands=total_api_calls,       # 返回总共尝试的 API 调用数
            errors="\n".join(errors_encountered) if errors_encountered else None
        )

    except (ValueError, TypeError) as e: # 解析或翻译期间的预期错误
        logging.error(f"命令解析或翻译失败: {e}", exc_info=True)
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"命令解析/翻译失败: {e}"
        )
    except Exception as e: # 意外错误
        logging.exception("处理命令时发生意外错误:")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"内部服务器错误: {e}"
        )