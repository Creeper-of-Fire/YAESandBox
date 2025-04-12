# api/websocket_manager.py
import logging
from typing import List, Dict, Optional, Any
from fastapi import WebSocket
import json  # 用于序列化消息


class ConnectionManager:
    """管理活动的 WebSocket 连接。"""

    def __init__(self):
        # 使用列表存储连接对象
        self.active_connections: List[WebSocket] = []
        # 可以考虑使用字典，如果需要通过 client_id 快速查找
        # self.active_connections_map: Dict[str, WebSocket] = {}
        logging.info("ConnectionManager 初始化完成。")

    async def connect(self, websocket: WebSocket):
        """接受新的 WebSocket 连接并将其添加到活动列表。"""
        await websocket.accept()
        self.active_connections.append(websocket)
        # 获取客户端地址用于日志记录
        client_host = websocket.client.host if websocket.client else "未知地址"
        client_port = websocket.client.port if websocket.client else "未知端口"
        logging.info(f"WebSocket 连接已接受并添加: {client_host}:{client_port}。当前连接数: {len(self.active_connections)}")
        # 可以考虑在这里分配一个唯一的 client_id 并发送给客户端
        # client_id = f"client_{random.randint(1000, 9999)}"
        # self.active_connections_map[client_id] = websocket
        # await self.send_personal_message({"type": "your_id", "id": client_id}, websocket)

    def disconnect(self, websocket: WebSocket):
        """从活动列表中移除 WebSocket 连接。"""
        if websocket in self.active_connections:
            self.active_connections.remove(websocket)
            # 获取客户端地址用于日志记录
            client_host = websocket.client.host if websocket.client else "未知地址"
            client_port = websocket.client.port if websocket.client else "未知端口"
            logging.info(f"WebSocket 连接已断开并移除: {client_host}:{client_port}。当前连接数: {len(self.active_connections)}")
            # 如果使用了 map，也要从中移除
            # for client_id, conn in list(self.active_connections_map.items()):
            #     if conn == websocket:
            #         del self.active_connections_map[client_id]
            #         logging.info(f"从 Map 中移除 Client ID: {client_id}")
            #         break
        else:
            logging.warning("尝试断开一个不在活动列表中的 WebSocket 连接。")

    async def send_personal_message(self, message: Any, websocket: WebSocket):
        """向指定的 WebSocket 连接发送消息 (自动 JSON 序列化)。"""
        try:
            json_message = json.dumps(message)  # 将 Python 对象转为 JSON 字符串
            await websocket.send_text(json_message)
            logging.debug(f"向单个客户端发送消息: {json_message}")
        except Exception as e:
            # 连接可能已经关闭或出现其他问题
            logging.error(f"向单个客户端发送消息时出错: {e}", exc_info=False)
            # 可以考虑在这里调用 disconnect
            # self.disconnect(websocket)

    async def broadcast(self, message: Any):
        """向所有活动的 WebSocket 连接广播消息 (自动 JSON 序列化)。"""
        disconnected_sockets: List[WebSocket] = []
        json_message = json.dumps(message)
        logging.debug(f"广播消息给 {len(self.active_connections)} 个连接: {json_message}")
        for connection in self.active_connections:
            try:
                await connection.send_text(json_message)
            except Exception as e:
                # 标记连接以便稍后移除，避免在迭代时修改列表
                logging.warning(f"广播时连接出错，标记断开: {e}")
                disconnected_sockets.append(connection)

        # 移除广播时出错的连接
        for socket in disconnected_sockets:
            self.disconnect(socket)


# 创建一个全局实例 (单例模式)
# 其他模块可以通过 from api.websocket_manager import manager 来访问
manager = ConnectionManager()