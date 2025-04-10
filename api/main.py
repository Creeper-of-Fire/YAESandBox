# api/main.py
import logging
import os  # <--- 添加 os
from contextlib import asynccontextmanager
from fastapi import FastAPI
from dotenv import load_dotenv  # <--- 添加 load_dotenv

# --- 导入核心和依赖 ---
from core.game_state import GameState
# --- 添加 AI Service 相关导入 ---
from workflow.ai.base import AbstractAIService
from workflow.ai.deepseek_service import DeepSeekService
from workflow.ai.deepfake_service import DeepFakeService
# --- 导入 dependencies 模块以设置全局实例 ---
from . import dependencies
# --- 导入路由 ---
from .routers import entities, atomic, websocket
from .routers.debug import commands as debug_commands
from .routers.debug import game_state_debug
# --- 导入依赖项函数 (虽然这里不直接用，但保持导入清晰) ---


# --- 将 get_configured_ai_service 函数放在这里或导入它 ---
def get_configured_ai_service() -> AbstractAIService:
    """根据环境变量配置实例化并返回 AI 服务。"""
    ai_service_type = os.getenv("AI_SERVICE_TYPE", "deepfake").lower()  # 默认 fake

    if ai_service_type == "deepseek":
        logging.info("配置使用: DeepSeekService (真实 AI)")
        # API Key 会在 DeepSeekService 内部从 env 获取
        return DeepSeekService()
    elif ai_service_type == "deepfake":
        logging.info("配置使用: DeepFakeService (模拟 AI)")
        return DeepFakeService()
    else:
        logging.warning(f"未知的 AI_SERVICE_TYPE: '{ai_service_type}'，将默认使用 DeepFakeService。")
        return DeepFakeService()


# --- 配置日志和 Lifespan ---
# logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s') # 建议移到 lifespan 或外部配置

@asynccontextmanager
async def lifespan(app: FastAPI):
    # --- 在启动时执行 ---
    print("--- 应用启动流程开始 ---")  # 使用 print 因为 logging 可能还没完全配置好

    # 1. 加载 .env 文件 (确保在任何配置读取之前完成)
    load_dotenv()
    print(f".env 文件加载完成。AI_SERVICE_TYPE={os.getenv('AI_SERVICE_TYPE', '未设置')}")

    # 2. 配置日志 (可以移到这里)
    log_level_str = os.getenv("LOG_LEVEL", "INFO").upper()
    log_level = getattr(logging, log_level_str, logging.INFO)
    logging.basicConfig(level=log_level, format='%(asctime)s - %(name)s - %(levelname)s - %(message)s')
    logging.info(f"日志级别设置为: {log_level_str}")

    # 3. 初始化并设置全局 AI Service 实例
    logging.info("初始化 AI 服务...")
    dependencies.ai_service_instance = get_configured_ai_service()  # <--- **关键赋值**
    logging.info(f"全局 AI 服务实例已设置为: {dependencies.ai_service_instance.__class__.__name__}")

    # 4. 初始化并设置全局 GameState 实例
    logging.info("初始化全局 GameState...")
    dependencies.game_state_instance = GameState()  # <--- **关键赋值**
    logging.info("全局 GameState 初始化完成。")

    print("--- 应用启动流程完成，准备处理请求 ---")
    yield  # <--- 应用在这里运行

    # --- 在关闭时执行 ---
    logging.info("API 应用关闭。清理资源...")
    dependencies.game_state_instance = None
    dependencies.ai_service_instance = None  # 清理 AI 服务实例引用
    logging.info("全局实例已清理。")


# --- 创建 FastAPI 应用实例 ---
app = FastAPI(
    title="文本 RPG 游戏 API",
    description="提供查询游戏世界状态和执行命令的接口。",
    version="0.3.0-websocket",  # <-- 更新版本号
    lifespan=lifespan  # <--- 应用 lifespan 管理器
)




# --- 包含路由 ---
app.include_router(entities.router, prefix="/api", tags=["Entities (Query)"])
app.include_router(atomic.router, prefix="/api", tags=["Entities (Atomic CRUD)"])
app.include_router(websocket.router, tags=["WebSocket"])  # 包含 WebSocket 路由


@app.get("/", tags=["Root"])
async def read_root():
    return {"message": "欢迎来到文本 RPG API"}

# --- 包含新的 Debug 路由 ---
# 注意：我们仍然可以将它们挂载在 /api 下，但 Tag 会区分它们
app.include_router(
    debug_commands.router,
    prefix="/api",
    # Tag 在 router 内部定义了
    # description="内部调试 @Command 接口" # 可以在这里加描述
)
app.include_router(
    game_state_debug.router,
    prefix="/api",
    # Tag 在 router 内部定义了
    # description="内部调试 GameState 操作" # 可以在这里加描述
)

# --- 确保 run_api.py 足够简单 ---
# run_api.py 主要负责加载必要的环境变量（如果有端口、主机配置）
# 和运行 uvicorn，不应再包含 AI 服务或 GameState 的初始化逻辑。