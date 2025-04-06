# core/game_state.py
import logging
import json
import re
import yaml # 仍然可能用于元数据
from typing import List, Optional, Dict, Any, Literal, Union, Set, Tuple # 移除 Deque
from pathlib import Path
from datetime import datetime
# from collections import deque # 移除 deque
from pydantic import BaseModel, Field, ValidationError

# 导入 WorldState 和实体类 (保持不变)
from world_state import WorldState, Item, Character, Place, AnyEntity

# --- 存档数据模型 (移除 world_history) ---
class SaveData(BaseModel):
    """用于保存和加载游戏状态的数据结构 (无历史记录)"""
    current_world: WorldState = Field(...)
    conversation_log: List[Dict[str, Any]] = Field(default_factory=list)
    # 移除 world_history: List[WorldState] = Field(default_factory=list)
    game_state_metadata: Dict[str, Any] = Field(default_factory=dict) # 保留以备将来扩展
    metadata: Dict[str, Any] = Field(default_factory=dict)
    model_config = {'validate_assignment': True}


# --- 游戏状态管理器 (移除历史和回滚) ---
class GameState:
    """
    管理游戏元状态，包括当前世界状态、用户焦点和持久化。
    不包含历史快照或回滚功能。
    """
    def __init__(self): # 移除 max_history 参数
        self.world = WorldState()
        # 移除 self.history: Deque[WorldState] = deque(maxlen=max_history)
        # 移除 self.max_history = max_history
        self.user_focus: List[str] = []
        logging.info("GameState 初始化 (无历史记录功能)")

    # --- 查询方法 (保持不变) ---
    def find_entity(self, entity_id: str, include_destroyed: bool = False) -> Optional[AnyEntity]:
        """通过 ID 查找任何类型的实体"""
        return self.world.find_entity(entity_id, include_destroyed)

    def find_entity_by_name(self, name: str, entity_type: Optional[Literal["Item", "Character", "Place"]] = None,
                            include_destroyed: bool = False) -> Optional[AnyEntity]:
        """按名称查找实体（效率较低）"""
        return self.world.find_entity_by_name(name, entity_type, include_destroyed)

    # --- 焦点管理 (保持不变) ---
    def set_focus(self, entity_ids: List[str]):
        """设置用户焦点，只包含有效的实体ID"""
        valid_ids = []
        requested_ids = set(entity_ids)
        for entity_id in requested_ids:
            if self.find_entity(entity_id):
                valid_ids.append(entity_id)
            else:
                logging.warning(f"设置焦点时忽略无效或已销毁的实体 ID: '{entity_id}'")
        self.user_focus = valid_ids
        logging.info(f"用户焦点设置为: {', '.join(valid_ids) if valid_ids else '无'}")

    def add_focus(self, entity_id: str) -> bool:
        """添加一个实体到焦点列表 (如果有效且不存在)"""
        if entity_id in self.user_focus: return True
        if self.find_entity(entity_id):
            self.user_focus.append(entity_id)
            logging.info(f"添加焦点: {entity_id}")
            return True
        else:
            logging.warning(f"添加焦点失败: 找不到实体 ID '{entity_id}'")
            return False

    def remove_focus(self, entity_id: str) -> bool:
        """从焦点列表中移除一个实体"""
        if entity_id in self.user_focus:
            self.user_focus.remove(entity_id)
            logging.info(f"移除焦点: {entity_id}")
            return True
        else: return False

    def clear_focus(self):
        """清除所有用户焦点"""
        if self.user_focus:
            logging.info("清除所有用户焦点。")
            self.user_focus = []

    def get_current_focus(self) -> List[str]:
        """获取当前的用户焦点列表"""
        return self.user_focus

    # --- 移除 快照与回滚 方法 ---
    # def save_history_point(self): ...
    # def rollback_state(self) -> bool: ...
    # def commit_state(self): ...

    # --- 保存游戏 (更新 SaveData 和元数据) ---
    def save_game(self, filepath: Union[str, Path], conversation_log: List[Dict[str, Any]]):
        """保存当前游戏状态 (GameState 元数据和 WorldState) 和对话记录到文件 (无历史)"""
        filepath = Path(filepath)
        logging.info(f"准备保存游戏到: {filepath}")
        try:
            save_data = SaveData(
                current_world=self.world,
                conversation_log=conversation_log,
                # 不再保存 world_history
                game_state_metadata={
                    # "max_history": self.max_history, # 移除
                    "user_focus": self.user_focus
                 },
                metadata={
                    "save_time": datetime.now().isoformat(),
                    "game_version": "0.5.0-no-rollback" # 更新版本号
                }
            )
            json_data = save_data.model_dump_json(indent=2)
            filepath.parent.mkdir(parents=True, exist_ok=True)
            with open(filepath, 'w', encoding='utf-8') as f: f.write(json_data)
            logging.info(f"游戏成功保存到: {filepath}")
        except Exception as e:
            logging.error(f"保存游戏到 '{filepath}' 失败: {e}", exc_info=True)
            raise RuntimeError(f"保存游戏失败: {e}") from e

# --- 加载游戏 (更新 SaveData 和元数据加载) ---
def load_game(filepath: Union[str, Path]) -> Tuple[GameState, List[Dict[str, Any]]]:
    """
    从文件加载游戏状态，返回新的 GameState 实例和对话记录。
    失败时抛出异常。(无历史)
    """
    filepath = Path(filepath)
    logging.info(f"尝试从文件加载游戏: {filepath}")
    if not filepath.is_file():
        raise FileNotFoundError(f"存档文件不存在: {filepath}")
    try:
        with open(filepath, 'r', encoding='utf-8') as f: json_data = f.read()
        save_data = SaveData.model_validate_json(json_data) # 使用更新后的 SaveData 模型

        gs_metadata = save_data.game_state_metadata
        # max_history = gs_metadata.get("max_history", 10) # 移除
        user_focus = gs_metadata.get("user_focus", [])

        new_game_state = GameState() # 创建新的无历史 GameState
        new_game_state.world = save_data.current_world
        new_game_state.user_focus = user_focus

        # 移除 history_deque 的加载
        # history_deque = deque(maxlen=max_history)
        # for state in save_data.world_history: history_deque.appendleft(state)
        # new_game_state.history = history_deque

        logging.info(f"游戏成功从 '{filepath}' 加载。焦点: {user_focus}")
        return new_game_state, save_data.conversation_log
    except (json.JSONDecodeError, ValidationError) as e:
         # 检查 ValidationError 是否因为缺少 world_history，如果是旧存档，可以给更明确提示
         if isinstance(e, ValidationError) and any('world_history' in err['loc'] for err in e.errors()):
              logging.error(f"加载游戏失败：存档文件 '{filepath}' 格式过时 (包含已移除的 world_history)。错误: {e}")
              raise ValueError(f"加载游戏失败，存档文件格式过时，请使用新版本创建存档。") from e
         logging.error(f"加载游戏失败：文件格式无效或损坏 '{filepath}'. 错误: {e}")
         raise ValueError(f"加载游戏失败，文件格式无效: {e}") from e
    except FileNotFoundError as e:
         logging.error(f"加载游戏失败：文件未找到 '{filepath}'")
         raise e
    except Exception as e:
         logging.error(f"加载游戏时发生意外错误 '{filepath}': {e}", exc_info=True)
         raise RuntimeError(f"加载游戏时发生意外错误: {e}") from e