# api/dependencies.py
import logging
from fastapi import HTTPException, status
from typing import Optional

from workflow.ai.base import AbstractAIService
# 导入 core GameState
from core.game_state import GameState

# --- 全局 GameState 实例 ---
# 这个实例将由 main.py 中的 lifespan 管理器进行初始化和清理
game_state_instance: Optional[GameState] = None
ai_service_instance: Optional[AbstractAIService] = None

# --- 依赖项函数 ---
def get_game_state() -> GameState:
    """FastAPI 依赖项，用于获取全局 GameState 实例。"""
    # 引用本模块的全局变量
    global game_state_instance
    if game_state_instance is None:
        logging.error("依赖项错误：尝试访问未初始化的 GameState！")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="内部服务器错误：游戏状态未初始化"
        )
    return game_state_instance

# --- 新增：获取 AI 服务的依赖项函数 ---
def get_ai_service() -> AbstractAIService:
    """FastAPI 依赖项，用于获取全局 AI Service 实例。"""
    global ai_service_instance
    if ai_service_instance is None:
        # 这个错误理论上不应该发生，因为它应该在应用启动时被初始化
        logging.error("依赖项错误：尝试访问未初始化的 AIService！")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="内部服务器错误：AI 服务未初始化"
        )
    return ai_service_instance