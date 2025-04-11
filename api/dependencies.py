# api/dependencies.py
# -*- coding: utf-8 -*-
"""
管理 FastAPI 应用程序的依赖项和全局实例。
"""
import logging
from fastapi import HTTPException, status
from typing import Optional

# --- 核心模块导入 ---
from core.game_state import GameState
# --- AI 服务导入 ---
from workflows.ai.base import AbstractAIService
# --- WebSocket 管理器导入 ---
from .websocket_manager import manager as global_connection_manager # 直接导入 manager 实例
# --- Notifier 导入 ---
from .notifier import Notifier # 导入 Notifier 类

# --- 全局实例变量 ---
# 这些变量将在 main.py 的 lifespan 管理器中被赋值和清理
game_state_instance: Optional[GameState] = None
ai_service_instance: Optional[AbstractAIService] = None
notifier_instance: Optional[Notifier] = None
# connection_manager 实例直接从 websocket_manager.py 导入，它本身就是单例

# --- 依赖项提供函数 ---
def get_game_state() -> GameState:
    """FastAPI 依赖项，用于获取全局 GameState 实例。"""
    global game_state_instance
    if game_state_instance is None:
        logging.error("依赖项错误：尝试访问未初始化的 GameState！")
        # 在应用完全启动失败时，这可能会在 lifespan 之外被意外调用
        # 返回 503 Service Unavailable 可能比 500 更合适
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="服务暂时不可用：游戏状态尚未初始化"
        )
    return game_state_instance

def get_ai_service() -> AbstractAIService:
    """FastAPI 依赖项，用于获取全局 AI Service 实例。"""
    global ai_service_instance
    if ai_service_instance is None:
        logging.error("依赖项错误：尝试访问未初始化的 AIService！")
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="服务暂时不可用：AI 服务尚未初始化"
        )
    return ai_service_instance

def get_connection_manager() -> 'ConnectionManager':
    """FastAPI 依赖项，用于获取全局 ConnectionManager 实例。"""
    # 直接返回导入的全局实例
    # 无需检查 None，因为它在模块加载时就被创建了
    return global_connection_manager

def get_notifier() -> Notifier:
    """FastAPI 依赖项，用于获取全局 Notifier 实例。"""
    global notifier_instance
    if notifier_instance is None:
        logging.error("依赖项错误：尝试访问未初始化的 Notifier！")
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="服务暂时不可用：通知服务尚未初始化"
        )
    return notifier_instance