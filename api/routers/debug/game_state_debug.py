# api/routers/debug/game_state_debug.py
import logging
from fastapi import APIRouter, Depends, status, Response, HTTPException

from core.game_state import GameState
from api.dependencies import get_game_state

router = APIRouter()

@router.post(
    "/game_state/reset", # 路径保持不变，因为前缀会在 main.py 中添加
    status_code=status.HTTP_204_NO_CONTENT,
    tags=["_Internal/Debug - Game State"], # 使用 Debug Tag
    summary="重置游戏状态",
    description="清空当前的游戏世界和用户焦点，恢复到初始状态（用于开发和测试）。"
)
async def reset_game_state(
    gs: GameState = Depends(get_game_state)
):
    try:
        gs.reset()
        logging.info("API 请求：游戏状态已重置。")
        return Response(status_code=status.HTTP_204_NO_CONTENT)
    except Exception as e:
        logging.exception("重置游戏状态时发生错误:")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"重置游戏状态时发生内部错误: {e}"
        )