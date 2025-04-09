# core/game_state.py
import json
import logging
from datetime import datetime
from pathlib import Path
from typing import List, Optional, Dict, Any, Literal, Union, Tuple, cast

from pydantic import BaseModel, Field, ValidationError

# 导入 WorldState 和实体类
from core.world_state import WorldState, AnyEntity

# 导入 TypedID
from core.types import TypedID, EntityType # 使用 EntityType

# --- 存档数据模型 (修改：user_focus 类型提示用 Any，加载时处理) ---
class SaveData(BaseModel):
    current_world: WorldState = Field(...)
    conversation_log: List[Dict[str, Any]] = Field(default_factory=list)
    # 由于类型嵌套在字典中，Pydantic 可能无法自动转换，加载时需要手动处理
    game_state_metadata: Dict[str, Any] = Field(default_factory=dict)  # 包含 user_focus (List[Dict] 或 List[TypedID])
    metadata: Dict[str, Any] = Field(default_factory=dict)
    model_config = {'validate_assignment': True}


# --- 游戏状态管理器 (修改：使用 TypedID) ---
class GameState:
    """
    管理游戏元状态，包括当前世界状态、用户焦点和持久化。
    用户焦点存储为 TypedID 实例列表。
    """

    def __init__(self):
        self.world = WorldState()
        self.user_focus: List[TypedID] = [] # <--- 修改：存储 TypedID 列表
        logging.info("GameState 初始化 (ID 类型: 类型内唯一)")

    # --- 新增 reset 方法 (保持不变) ---
    def reset(self):
        """重置游戏状态到初始状态（清空世界和焦点）。"""
        logging.warning("执行 GameState.reset()，清空当前世界状态和用户焦点！")
        self.world = WorldState()  # 创建一个新的空 WorldState
        self.user_focus = []  # 清空焦点列表
        logging.info("GameState 已重置。")

    # --- 查询方法 (调用 world 的方法) ---
    def find_entity(self, entity_id: str, entity_type: EntityType, include_destroyed: bool = False) -> Optional[AnyEntity]:
        """通过 ID 和类型查找实体"""
        return self.world.find_entity(entity_id, entity_type, include_destroyed)

    # --- 修改：移除 find_entity_by_ref，使用 find_entity_by_typed_id ---
    # def find_entity_by_ref(self, ref: Optional[Union[Tuple[str, str], List[str]]], include_destroyed: bool = False) -> Optional[AnyEntity]:
    #     """通过 (Type, ID) 元组查找实体"""
    #     # 调用 world 的同名方法
    #     return self.world.find_entity_by_ref(ref, include_destroyed) # WorldState 已移除此方法

    def find_entity_by_typed_id(self, ref: Optional[TypedID], include_destroyed: bool = False) -> Optional[AnyEntity]:
        """通过 TypedID 引用查找实体"""
        return self.world.find_entity_by_typed_id(ref, include_destroyed) # 调用 world 的新方法

    def find_entity_by_name(self, name: str, entity_type: Optional[EntityType] = None,
                            include_destroyed: bool = False) -> Optional[AnyEntity]:
        """按名称查找实体（效率较低）"""
        return self.world.find_entity_by_name(name, entity_type, include_destroyed)

    # --- 焦点管理 (修改：使用 TypedID) ---
    def set_focus(self, entity_refs: List[TypedID]): # <--- 修改：接受 TypedID 列表
        """设置用户焦点，只包含有效的 TypedID 实例"""
        valid_refs: List[TypedID] = []
        # 使用集合去重 TypedID 对象 (TypedID 需要实现 __hash__ 和 __eq__)
        requested_refs_set = set(entity_refs)
        for ref in requested_refs_set:
            if not isinstance(ref, TypedID):
                logging.warning(f"设置焦点时忽略无效类型: {type(ref)}，需要 TypedID。")
                continue
            # 使用 find_entity 进行验证
            if self.find_entity(ref.id, ref.type, include_destroyed=False):
                valid_refs.append(ref)
            else:
                logging.warning(f"设置焦点时忽略无效或已销毁的实体引用: {ref}") # TypedID 会自动转为 str
        # 对 TypedID 列表排序 (需要 TypedID 实现 __lt__ 或提供 key 函数)
        # 暂时按字符串排序
        self.user_focus = sorted(valid_refs, key=lambda tid: str(tid))
        logging.info(f"用户焦点设置为: {', '.join(map(str, self.user_focus)) if self.user_focus else '无'}")

    def add_focus(self, entity_ref: TypedID) -> bool: # <--- 修改：接受 TypedID
        """添加一个 TypedID 到焦点列表 (如果有效且不存在)"""
        if not isinstance(entity_ref, TypedID):
            logging.error(f"添加焦点失败：无效的引用格式 {repr(entity_ref)}，需要 TypedID 对象。")
            return False

        if entity_ref in self.user_focus: return True # 依赖 TypedID 的 __eq__

        # 使用 find_entity 进行验证
        if self.find_entity(entity_ref.id, entity_ref.type, include_destroyed=False):
            self.user_focus.append(entity_ref)
            self.user_focus.sort(key=lambda tid: str(tid)) # 按字符串排序
            logging.info(f"添加焦点: {entity_ref}")
            return True
        else:
            logging.warning(f"添加焦点失败: 找不到实体引用 {entity_ref}")
            return False

    def remove_focus(self, entity_ref: TypedID) -> bool: # <--- 修改：接受 TypedID
        """从焦点列表中移除一个 TypedID"""
        if not isinstance(entity_ref, TypedID):
            logging.error(f"移除焦点失败：无效的引用格式 {repr(entity_ref)}，需要 TypedID 对象。")
            return False

        try:
            self.user_focus.remove(entity_ref) # 依赖 TypedID 的 __eq__
            logging.info(f"移除焦点: {entity_ref}")
            return True
        except ValueError:
            return False # 不存在于列表中

    def clear_focus(self):
        """清除所有用户焦点"""
        if self.user_focus:
            logging.info("清除所有用户焦点。")
            self.user_focus = []

    def get_current_focus(self) -> List[TypedID]: # <--- 修改：返回 TypedID 列表
        """获取当前的用户焦点列表 (TypedID 列表)"""
        return self.user_focus

    # --- 保存游戏 (修改：保存 TypedID 列表) ---
    def save_game(self, filepath: Union[str, Path], conversation_log: List[Dict[str, Any]]):
        filepath = Path(filepath)
        logging.info(f"准备保存游戏到: {filepath}")
        try:
            save_data = SaveData(
                current_world=self.world,
                conversation_log=conversation_log,
                game_state_metadata={
                    # user_focus 现在是 TypedID 列表
                    # Pydantic 会自动将 TypedID 序列化为字典 {"type": ..., "id": ...}
                    "user_focus": self.user_focus
                },
                metadata={
                    "save_time": datetime.now().isoformat(),
                    "game_version": "0.8.0-typed-id"  # 更新版本号
                }
            )
            json_data = save_data.model_dump_json(indent=2)
            filepath.parent.mkdir(parents=True, exist_ok=True)
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(json_data)
            logging.info(f"游戏成功保存到: {filepath}")
        except Exception as e:
            logging.error(f"保存游戏到 '{filepath}' 失败: {e}", exc_info=True)
            raise RuntimeError(f"保存游戏失败: {e}") from e


# --- 加载游戏 (修改：处理 TypedID 反序列化) ---
def load_game(filepath: Union[str, Path]) -> Tuple[GameState, List[Dict[str, Any]]]:
    filepath = Path(filepath)
    logging.info(f"尝试从文件加载游戏: {filepath}")
    if not filepath.is_file():
        raise FileNotFoundError(f"存档文件不存在: {filepath}")
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            json_data = f.read()
        # SaveData.model_validate_json 会尝试解析
        # 但 game_state_metadata["user_focus"] 可能仍是 Dict 列表
        save_data = SaveData.model_validate_json(json_data)

        gs_metadata = save_data.game_state_metadata
        loaded_focus_raw = gs_metadata.get("user_focus", [])
        user_focus: List[TypedID] = []

        if isinstance(loaded_focus_raw, list):
            for item_raw in loaded_focus_raw:
                if isinstance(item_raw, dict) and "type" in item_raw and "id" in item_raw:
                    try:
                        # 显式地从字典创建 TypedID 对象
                        typed_id = TypedID(type=item_raw["type"], id=item_raw["id"])
                        user_focus.append(typed_id)
                    except ValidationError as ve:
                        logging.warning(f"加载存档时，在 user_focus 中发现无效 TypedID 字典: {item_raw}，错误: {ve}，已忽略。")
                    except Exception as e:
                        logging.warning(f"加载存档时，在 user_focus 中处理字典 {item_raw} 时出错: {e}，已忽略。")
                elif isinstance(item_raw, TypedID): # 如果 Pydantic 自动转换成功
                     user_focus.append(item_raw)
                else:
                    logging.warning(f"加载存档时，在 user_focus 中发现未知格式项: {repr(item_raw)} ({type(item_raw)})，已忽略。")
        else:
            logging.warning(f"加载存档时，user_focus 格式无效 (非列表): {type(loaded_focus_raw)}，已忽略。")

        new_game_state = GameState()
        new_game_state.world = save_data.current_world
        # 排序处理后的 TypedID 列表
        new_game_state.user_focus = sorted(user_focus, key=lambda tid: str(tid))

        logging.info(f"游戏成功从 '{filepath}' 加载。焦点: {', '.join(map(str, new_game_state.user_focus)) if new_game_state.user_focus else '无'}")
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