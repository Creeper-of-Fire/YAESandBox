# api/notifier.py
"""
定义 Notifier 服务，用于向 WebSocket 客户端广播状态变更信号。
"""
import logging
from .websocket_manager import ConnectionManager

class Notifier:
    """
    负责向所有连接的 WebSocket 客户端广播简单的状态更新信号。
    采用单例模式，由依赖注入管理。
    """
    def __init__(self, connection_manager: ConnectionManager):
        """
        初始化 Notifier。

        Args:
            connection_manager: 全局的 ConnectionManager 实例。
        """
        self._connection_manager = connection_manager
        logging.info("Notifier 初始化完成。")

    async def notify_state_update(self):
        """
        向所有连接的 WebSocket 客户端广播 state_update_signal 消息。
        """
        message = {
            "type": "state_update_signal",
            "payload": {}  # 简单的信号，不包含具体数据
        }
        logging.info("Notifier: 广播 state_update_signal 给所有客户端...")
        await self._connection_manager.broadcast(message)
        logging.debug("Notifier: state_update_signal 广播完成。")

# 注意：Notifier 的实例将在 main.py 的 lifespan 中创建，并通过 dependencies.py 提供。