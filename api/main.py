# api/main.py
import logging
from fastapi import FastAPI
from contextlib import asynccontextmanager

from core.game_state import GameState
# --- 添加导入 ---
from .routers import entities, commands, atomic # <--- 添加 atomic
from . import dependencies

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

# --- 包含路由 ---
app.include_router(entities.router, prefix="/api", tags=["Entities (Query)"]) # 旧的查询路由
app.include_router(commands.router, prefix="/api", tags=["Commands (Legacy @Command)"]) # 旧的命令路由
app.include_router(atomic.router, prefix="/api", tags=["Entities (Atomic CRUD)"]) # <--- 包含新的原子路由

@app.get("/", tags=["Root"])
async def read_root():
    return {"message": "欢迎来到文本 RPG API"}