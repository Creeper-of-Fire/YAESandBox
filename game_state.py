# game_state.py
import logging
import json
import re # 仍然需要 re 用于加载时清理摘要文本（可选）
import yaml # 仍然需要 yaml 用于加载/保存时的元数据处理（如果使用）
from typing import List, Optional, Dict, Any, Literal, Union, Set, Tuple, Deque
from pathlib import Path
from datetime import datetime
from collections import deque
from pydantic import BaseModel, Field, ValidationError

# 导入重构后的 WorldState 和实体类
from world_state import WorldState, Item, Character, Place, AnyEntity

# --- 存档数据模型 (保持不变) ---
class SaveData(BaseModel):
    """用于保存和加载游戏状态的数据结构"""
    current_world: WorldState = Field(...)
    conversation_log: List[Dict[str, Any]] = Field(default_factory=list)
    world_history: List[WorldState] = Field(default_factory=list)
    game_state_metadata: Dict[str, Any] = Field(default_factory=dict)
    metadata: Dict[str, Any] = Field(default_factory=dict)
    model_config = {'validate_assignment': True}


# --- 游戏状态管理器 ---
class GameState:
    """
    管理游戏元状态，包括当前世界状态、历史快照、用户焦点和持久化。
    不包含生成摘要或报告的逻辑。
    """
    def __init__(self, max_history: int = 10):
        self.world = WorldState()
        self.history: Deque[WorldState] = deque(maxlen=max_history)
        self.max_history = max_history
        self.user_focus: List[str] = []
        logging.info(f"GameState 初始化，最大历史记录: {max_history}")

    # --- 查询方法 (委托给 self.world) ---
    def find_entity(self, entity_id: str, include_destroyed: bool = False) -> Optional[AnyEntity]:
        """通过 ID 查找任何类型的实体"""
        return self.world.find_entity(entity_id, include_destroyed)

    def find_entity_by_name(self, name: str, entity_type: Optional[Literal["Item", "Character", "Place"]] = None,
                            include_destroyed: bool = False) -> Optional[AnyEntity]:
        """按名称查找实体（效率较低）"""
        return self.world.find_entity_by_name(name, entity_type, include_destroyed)

    # --- 焦点管理 ---
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

    # 移除 get_problematic_entities 和 get_state_summary 方法
    # def get_problematic_entities(self) -> List[Dict[str, Any]]: ...
    # def get_state_summary(self) -> str: ...

    # --- 快照与回滚 ---
    def save_history_point(self):
        """保存当前世界状态 (WorldState) 的快照"""
        max_hist = self.history.maxlen if self.history.maxlen is not None else float('inf')
        current_len = len(self.history)
        if current_len >= max_hist:
             logging.warning(f"历史记录已满 ({max_hist}/{max_hist})，最旧的状态点将被覆盖。")
        snapshot = self.world.model_copy(deep=True)
        self.history.appendleft(snapshot)
        logging.info(f"保存历史状态点 (当前历史数: {len(self.history)} / {max_hist})")

    def rollback_state(self) -> bool:
        """回滚到上一个保存的 WorldState"""
        if not self.history:
            logging.warning("无法回滚：没有可用的历史状态。")
            return False
        logging.info("执行状态回滚...")
        previous_world_state = self.history.popleft()
        self.world = previous_world_state
        logging.info(f"WorldState 已成功回滚到上一个保存点 (剩余历史: {len(self.history)})。焦点状态未改变。")
        return True

    def commit_state(self):
        """清除所有 WorldState 历史记录，使当前状态成为新的基线"""
        if not self.history:
            logging.info("无需固化状态：历史记录已为空。")
            return
        logging.info(f"固化当前状态，清除 {len(self.history)} 条历史记录...")
        self.history.clear()
        logging.info("历史记录已清除。")

    # --- 保存游戏 ---
    def save_game(self, filepath: Union[str, Path], conversation_log: List[Dict[str, Any]]):
        """保存当前游戏状态 (GameState 元数据和 WorldState) 和对话记录到文件"""
        filepath = Path(filepath)
        logging.info(f"准备保存游戏到: {filepath}")
        try:
            save_data = SaveData(
                current_world=self.world,
                conversation_log=conversation_log,
                world_history=list(self.history),
                game_state_metadata={
                    "max_history": self.max_history,
                    "user_focus": self.user_focus
                 },
                metadata={
                    "save_time": datetime.now().isoformat(),
                    "game_version": "0.4.1-prompt-refactor" # 更新版本号
                }
            )
            json_data = save_data.model_dump_json(indent=2)
            filepath.parent.mkdir(parents=True, exist_ok=True)
            with open(filepath, 'w', encoding='utf-8') as f: f.write(json_data)
            logging.info(f"游戏成功保存到: {filepath}")
        except Exception as e:
            logging.error(f"保存游戏到 '{filepath}' 失败: {e}", exc_info=True)
            raise RuntimeError(f"保存游戏失败: {e}") from e

# --- 加载游戏 (独立函数，保持不变) ---
def load_game(filepath: Union[str, Path]) -> Tuple[GameState, List[Dict[str, Any]]]:
    """
    从文件加载游戏状态，返回新的 GameState 实例和对话记录。
    失败时抛出异常。
    """
    filepath = Path(filepath)
    logging.info(f"尝试从文件加载游戏: {filepath}")
    if not filepath.is_file():
        raise FileNotFoundError(f"存档文件不存在: {filepath}")
    try:
        with open(filepath, 'r', encoding='utf-8') as f: json_data = f.read()
        save_data = SaveData.model_validate_json(json_data)

        gs_metadata = save_data.game_state_metadata
        max_history = gs_metadata.get("max_history", 10)
        user_focus = gs_metadata.get("user_focus", [])

        new_game_state = GameState(max_history=max_history)
        new_game_state.world = save_data.current_world
        new_game_state.user_focus = user_focus

        history_deque = deque(maxlen=max_history)
        for state in save_data.world_history: history_deque.appendleft(state)
        new_game_state.history = history_deque

        logging.info(f"游戏成功从 '{filepath}' 加载。历史记录 {len(history_deque)} 条，焦点: {user_focus}")
        return new_game_state, save_data.conversation_log
    except (json.JSONDecodeError, ValidationError) as e:
         logging.error(f"加载游戏失败：文件格式无效或损坏 '{filepath}'. 错误: {e}")
         raise ValueError(f"加载游戏失败，文件格式无效: {e}") from e
    except FileNotFoundError as e:
         logging.error(f"加载游戏失败：文件未找到 '{filepath}'")
         raise e
    except Exception as e:
         logging.error(f"加载游戏时发生意外错误 '{filepath}': {e}", exc_info=True)
         raise RuntimeError(f"加载游戏时发生意外错误: {e}") from e