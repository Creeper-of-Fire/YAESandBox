# core/game_state.py
import logging
import json
import re
import yaml
from typing import List, Optional, Dict, Any, Literal, Union, Set, Tuple, cast
from pathlib import Path
from datetime import datetime
from pydantic import BaseModel, Field, ValidationError

# 导入 WorldState 和实体类
from core.world_state import WorldState, Item, Character, Place, AnyEntity, BaseEntity

# --- 存档数据模型 (保持不变，user_focus 已是元组) ---
class SaveData(BaseModel):
    current_world: WorldState = Field(...)
    conversation_log: List[Dict[str, Any]] = Field(default_factory=list)
    game_state_metadata: Dict[str, Any] = Field(default_factory=dict) # 包含 user_focus
    metadata: Dict[str, Any] = Field(default_factory=dict)
    model_config = {'validate_assignment': True}


# --- 游戏状态管理器 (修改：查找方法调用更新) ---
class GameState:
    """
    管理游戏元状态，包括当前世界状态、用户焦点和持久化。
    用户焦点存储为 (EntityType, EntityID) 元组。
    """
    def __init__(self):
        self.world = WorldState()
        self.user_focus: List[Tuple[Literal["Item", "Character", "Place"], str]] = []
        logging.info("GameState 初始化 (ID 类型: 类型内唯一)")

    # --- 查询方法 (调用 world 的新方法) ---
    def find_entity(self, entity_id: str, entity_type: Literal["Item", "Character", "Place"], include_destroyed: bool = False) -> Optional[AnyEntity]:
        """通过 ID 和类型查找实体"""
        # 调用 world 的同名方法
        return self.world.find_entity(entity_id, entity_type, include_destroyed)

    def find_entity_by_ref(self, ref: Optional[Union[Tuple[str, str], List[str]]], include_destroyed: bool = False) -> Optional[AnyEntity]:
         """通过 (Type, ID) 元组查找实体"""
         # 调用 world 的同名方法
         return self.world.find_entity_by_ref(ref, include_destroyed)

    def find_entity_by_name(self, name: str, entity_type: Optional[Literal["Item", "Character", "Place"]] = None,
                            include_destroyed: bool = False) -> Optional[AnyEntity]:
        """按名称查找实体（效率较低）"""
        # 调用 world 的同名方法
        return self.world.find_entity_by_name(name, entity_type, include_destroyed)

    # --- 焦点管理 (修改：验证时调用新的 find_entity) ---
    def set_focus(self, entity_refs: List[Tuple[Literal["Item", "Character", "Place"], str]]):
        """设置用户焦点，只包含有效的实体引用元组"""
        valid_refs = []
        requested_refs = set(entity_refs)
        for ref_type, ref_id in requested_refs:
            # 使用新的 find_entity 进行验证
            if self.find_entity(ref_id, ref_type, include_destroyed=False): # 需要传入类型
                valid_refs.append((ref_type, ref_id))
            else:
                logging.warning(f"设置焦点时忽略无效或已销毁的实体引用: {ref_type}:{ref_id}")
        self.user_focus = sorted(valid_refs)
        logging.info(f"用户焦点设置为: {', '.join(f'{t}:{i}' for t, i in self.user_focus) if self.user_focus else '无'}")

    def add_focus(self, entity_ref: Tuple[Literal["Item", "Character", "Place"], str]) -> bool:
        """添加一个实体引用到焦点列表 (如果有效且不存在)"""
        if not isinstance(entity_ref, tuple) or len(entity_ref) != 2:
             logging.error(f"添加焦点失败：无效的引用格式 {repr(entity_ref)}，需要 (Type, ID) 元组。")
             return False
        ref_type, ref_id = entity_ref

        if entity_ref in self.user_focus: return True

        # 使用新的 find_entity 进行验证
        if self.find_entity(ref_id, ref_type, include_destroyed=False): # 需要传入类型
            self.user_focus.append(entity_ref)
            self.user_focus.sort()
            logging.info(f"添加焦点: {ref_type}:{ref_id}")
            return True
        else:
            logging.warning(f"添加焦点失败: 找不到实体引用 {ref_type}:{ref_id}")
            return False

    # remove_focus, clear_focus, get_current_focus 保持不变

    def remove_focus(self, entity_ref: Tuple[Literal["Item", "Character", "Place"], str]) -> bool:
        """从焦点列表中移除一个实体引用"""
        if not isinstance(entity_ref, tuple) or len(entity_ref) != 2:
             logging.error(f"移除焦点失败：无效的引用格式 {repr(entity_ref)}，需要 (Type, ID) 元组。")
             return False
        ref_type, ref_id = entity_ref

        if entity_ref in self.user_focus:
            self.user_focus.remove(entity_ref)
            logging.info(f"移除焦点: {ref_type}:{ref_id}")
            return True
        else: return False

    def clear_focus(self):
        """清除所有用户焦点"""
        if self.user_focus:
            logging.info("清除所有用户焦点。")
            self.user_focus = []

    def get_current_focus(self) -> List[Tuple[Literal["Item", "Character", "Place"], str]]:
        """获取当前的用户焦点列表 (元组列表)"""
        return self.user_focus

    # --- 保存游戏 (保持不变，格式已兼容) ---
    def save_game(self, filepath: Union[str, Path], conversation_log: List[Dict[str, Any]]):
        # (保存逻辑保持不变)
        filepath = Path(filepath)
        logging.info(f"准备保存游戏到: {filepath}")
        try:
            save_data = SaveData(
                current_world=self.world,
                conversation_log=conversation_log,
                game_state_metadata={
                    "user_focus": self.user_focus # 已是元组列表
                 },
                metadata={
                    "save_time": datetime.now().isoformat(),
                    "game_version": "0.7.0-type-unique-id" # 更新版本号
                }
            )
            json_data = save_data.model_dump_json(indent=2)
            filepath.parent.mkdir(parents=True, exist_ok=True)
            with open(filepath, 'w', encoding='utf-8') as f: f.write(json_data)
            logging.info(f"游戏成功保存到: {filepath}")
        except Exception as e:
            logging.error(f"保存游戏到 '{filepath}' 失败: {e}", exc_info=True)
            raise RuntimeError(f"保存游戏失败: {e}") from e

# --- 加载游戏 (保持不变，格式已兼容) ---
def load_game(filepath: Union[str, Path]) -> Tuple[GameState, List[Dict[str, Any]]]:
    # (加载逻辑保持不变，user_focus 解析已兼容元组)
    filepath = Path(filepath)
    logging.info(f"尝试从文件加载游戏: {filepath}")
    if not filepath.is_file():
        raise FileNotFoundError(f"存档文件不存在: {filepath}")
    try:
        with open(filepath, 'r', encoding='utf-8') as f: json_data = f.read()
        save_data = SaveData.model_validate_json(json_data)

        gs_metadata = save_data.game_state_metadata
        loaded_focus = gs_metadata.get("user_focus", [])
        user_focus: List[Tuple[Literal["Item", "Character", "Place"], str]] = []
        if isinstance(loaded_focus, list):
             for item in loaded_focus:
                 if isinstance(item, list) and len(item) == 2: item = tuple(item)
                 if isinstance(item, tuple) and len(item) == 2 and \
                    isinstance(item[0], str) and item[0].capitalize() in ["Item", "Character", "Place"] and \
                    isinstance(item[1], str):
                      user_focus.append((cast(Literal["Item", "Character", "Place"], item[0].capitalize()), item[1]))
                 else:
                      logging.warning(f"加载存档时，在 user_focus 中发现无效项: {repr(item)}，已忽略。")
        else:
            logging.warning(f"加载存档时，user_focus 格式无效 (非列表): {type(loaded_focus)}，已忽略。")

        new_game_state = GameState()
        new_game_state.world = save_data.current_world
        new_game_state.user_focus = sorted(user_focus)

        logging.info(f"游戏成功从 '{filepath}' 加载。焦点: {', '.join(f'{t}:{i}' for t, i in new_game_state.user_focus) if new_game_state.user_focus else '无'}")
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