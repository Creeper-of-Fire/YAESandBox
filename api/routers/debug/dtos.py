# api/routers/debug/dtos.py
# -*- coding: utf-8 -*-
"""
数据传输对象 (DTOs) 专门用于内部调试接口。
这些 DTO 不应暴露给前端。
"""
from pydantic import BaseModel, Field
from typing import Optional

# --- 从 api/dtos.py 移动过来的调试用 DTOs ---

class CommandExecutionRequest(BaseModel):
    """用于 POST /api/commands/execute 的请求体 (调试用)。"""
    text: str = Field(..., description="包含 @Command 指令的文本")

class CommandExecutionResponse(BaseModel):
    """用于 /api/commands/execute* 端点的响应体 (调试用)。"""
    message: str = Field(..., description="执行结果的总结信息")
    executed_commands: int = Field(..., description="成功执行的原子 API 调用数量")
    total_commands: int = Field(..., description="尝试执行的原子 API 调用总数 (来自翻译后的指令)")
    errors: Optional[str] = Field(None, description="执行过程中遇到的错误详情 (多条错误以换行符分隔)，如果无错误则为 null")