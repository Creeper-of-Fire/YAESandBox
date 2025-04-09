# api/dependencies.py
import logging
from fastapi import HTTPException, status
from typing import Optional

# 导入 core GameState
from core.game_state import GameState

# --- 全局 GameState 实例 ---
# 这个实例将由 main.py 中的 lifespan 管理器进行初始化和清理
game_state_instance: Optional[GameState] = None

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