# api/main.py
import logging
from fastapi import FastAPI, Depends, status, Response, HTTPException  # <--- 添加 Depends, status, Response
from contextlib import asynccontextmanager

from core.game_state import GameState
# --- 添加导入 ---
from .routers import entities, commands, atomic # <--- 添加 atomic
from . import dependencies

# --- 导入 get_game_state 依赖 ---
from .dependencies import get_game_state # <--- 导入依赖项

logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

@asynccontextmanager
async def lifespan(app: FastAPI):
    logging.info("API 应用启动，初始化全局 GameState...")
    dependencies.game_state_instance = GameState()
    logging.info("全局 GameState 初始化完成。")
    yield
    logging.info("API 应用关闭。")
    dependencies.game_state_instance = None

app = FastAPI(
    title="文本 RPG 游戏 API",
    description="提供查询游戏世界状态和执行命令的接口。",
    version="0.2.0-atomic", # <-- 更新版本号
    lifespan=lifespan
)

# --- 新增：重置游戏状态端点 ---
@app.post(
    "/api/game_state/reset",
    status_code=status.HTTP_204_NO_CONTENT, # 重置成功返回无内容
    tags=["Game State"], # 可以创建一个新标签
    summary="重置游戏状态",
    description="清空当前的游戏世界和用户焦点，恢复到初始状态（用于开发和测试）。"
)
async def reset_game_state(
    gs: GameState = Depends(get_game_state) # 注入当前的 GameState
):
    """
    处理重置游戏状态的请求。
    """
    try:
        gs.reset() # 调用 GameState 的 reset 方法
        logging.info("API 请求：游戏状态已重置。")
        # 返回 204 No Content
        return Response(status_code=status.HTTP_204_NO_CONTENT)
    except Exception as e:
        logging.exception("重置游戏状态时发生错误:")
        # 如果重置失败，返回 500 错误
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"重置游戏状态时发生内部错误: {e}"
        )


# --- 包含路由 ---
app.include_router(entities.router, prefix="/api", tags=["Entities (Query)"])
app.include_router(commands.router, prefix="/api", tags=["Commands (@Command)"])
app.include_router(atomic.router, prefix="/api", tags=["Entities (Atomic CRUD)"])

@app.get("/", tags=["Root"])
async def read_root():
    return {"message": "欢迎来到文本 RPG API"}