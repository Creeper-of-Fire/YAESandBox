# api/main.py
# -*- coding: utf-8 -*-
"""
FastAPI 应用的主入口点，负责应用配置、生命周期管理和路由包含。
"""
import logging
import os
from contextlib import asynccontextmanager
from fastapi import FastAPI
from dotenv import load_dotenv

# --- 核心和依赖导入 ---
from core.game_state import GameState
from workflows.ai.base import AbstractAIService
from workflows.ai.deepseek_service import DeepSeekService
from workflows.ai.deepfake_service import DeepFakeService
# --- 导入 dependencies 模块以设置全局实例 ---
from . import dependencies
# --- 导入 Notifier 和 ConnectionManager ---
from .notifier import Notifier
from .websocket_manager import manager as connection_manager # 导入全局管理器实例
# --- 导入路由 ---
from .routers import entities, atomic, websocket
from .routers.debug import commands as debug_commands
from .routers.debug import game_state_debug

# --- AI 服务配置函数 (保持不变) ---
def get_configured_ai_service() -> AbstractAIService:
    ai_service_type = os.getenv("AI_SERVICE_TYPE", "deepfake").lower()
    if ai_service_type == "deepseek":
        logging.info("配置使用: DeepSeekService (真实 AI)")
        return DeepSeekService()
    elif ai_service_type == "deepfake":
        logging.info("配置使用: DeepFakeService (模拟 AI)")
        return DeepFakeService()
    else:
        logging.warning(f"未知的 AI_SERVICE_TYPE: '{ai_service_type}'，默认使用 DeepFakeService。")
        return DeepFakeService()

# --- 应用生命周期管理器 (Lifespan) ---
@asynccontextmanager
async def lifespan(app: FastAPI):
    """管理 FastAPI 应用的启动和关闭过程。"""
    print("--- 应用启动流程开始 ---")

    # 1. 加载 .env 文件
    load_dotenv()
    print(f".env 文件加载完成。AI_SERVICE_TYPE={os.getenv('AI_SERVICE_TYPE', '未设置')}")

    # 2. 配置日志
    log_level_str = os.getenv("LOG_LEVEL", "INFO").upper()
    log_level = getattr(logging, log_level_str, logging.INFO)
    # 确保每次启动时日志配置生效，避免 uvicorn reload 导致配置丢失
    logging.basicConfig(
        level=log_level,
        format='%(asctime)s - %(name)s [%(levelname)s] - %(message)s',
        force=True # 强制重新配置
    )
    logging.info(f"日志级别设置为: {log_level_str}")

    # 3. 初始化并设置全局 AI Service 实例
    logging.info("初始化 AI 服务...")
    dependencies.ai_service_instance = get_configured_ai_service()
    logging.info(f"全局 AI 服务实例已设置为: {dependencies.ai_service_instance.__class__.__name__}")

    # 4. 初始化并设置全局 GameState 实例
    logging.info("初始化全局 GameState...")
    dependencies.game_state_instance = GameState()
    logging.info("全局 GameState 初始化完成。")

    # 5. 初始化并设置全局 Notifier 实例
    #    Notifier 依赖于 ConnectionManager，ConnectionManager 实例已在导入时创建
    logging.info("初始化全局 Notifier...")
    dependencies.notifier_instance = Notifier(connection_manager=connection_manager)
    logging.info("全局 Notifier 初始化完成。")
    # 注意：ConnectionManager 实例 (dependencies.global_connection_manager) 不需要显式设置，
    # 它在 websocket_manager.py 模块加载时就已经存在了。

    print("--- 应用启动流程完成，准备处理请求 ---")
    yield  # 应用在此运行

    # --- 在关闭时执行 ---
    logging.info("API 应用关闭。清理资源...")
    # 按依赖关系反向清理可能是个好习惯
    dependencies.notifier_instance = None
    dependencies.game_state_instance = None
    dependencies.ai_service_instance = None
    # ConnectionManager 通常不需要手动清理，除非它持有需要释放的资源
    logging.info("全局实例引用已清理。")


# --- 创建 FastAPI 应用实例 ---
app = FastAPI(
    title="AI 驱动文本 RPG 引擎 API", # 更新标题
    description="提供查询游戏世界状态、触发 AI 工作流和通过原子操作修改状态的接口。", # 更新描述
    version="0.4.0-notifier",  # <-- 更新版本号
    lifespan=lifespan
)

# --- 包含路由 ---
app.include_router(entities.router, prefix="/api", tags=["Entities (Query)"])
app.include_router(atomic.router, prefix="/api", tags=["Entities (Atomic CRUD)"])
app.include_router(websocket.router, tags=["WebSocket"])
# 内部/调试路由
app.include_router(debug_commands.router, prefix="/api") # Tag 在路由内部定义
app.include_router(game_state_debug.router, prefix="/api") # Tag 在路由内部定义

@app.get("/", tags=["Root"])
async def read_root():
    return {"message": "欢迎来到 AI 驱动文本 RPG 引擎 API"}