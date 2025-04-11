# core/game_state.py
import logging
from typing import List, Optional, Dict, Any, Set
from pydantic import BaseModel, Field

# 导入 TypedID (假设它在 core.types 中)
from core.types import TypedID, EntityType

# --- 新的 GameState 类 ---
class GameState(BaseModel):
    """
    存储与叙事块关联的非实体状态。
    例如：玩家焦点、故事基调、工作流参数等。
    这个类的实例将在叙事块之间传递和修改。
    不再管理 WorldState 或负责存档/读档。
    """
    # 核心状态字段 (示例)
    user_focus: List[TypedID] = Field(default_factory=list, description="当前用户关注的实体列表 (有序)")
    story_tone: str = Field(default="neutral", description="当前故事或场景的基调")
    workflow_params: Dict[str, Any] = Field(default_factory=dict, description="传递给工作流的额外参数")
    active_quests: Set[str] = Field(default_factory=set, description="当前激活的任务ID集合")
    # 可以根据需要添加更多字段...

    # --- 焦点管理方法 (现在只操作自身属性) ---

    def set_focus(self, entity_refs: List[TypedID]):
        """
        设置用户焦点 (仅操作本实例的 user_focus 列表)。
        注意：不再进行实体有效性验证，验证应在更高层进行。
        """
        # 使用集合去重并保持顺序（如果 Python >= 3.7）
        unique_refs = list(dict.fromkeys(entity_refs))
        # 简单排序 (按字符串表示)
        self.user_focus = sorted(unique_refs, key=str)
        logging.debug(f"GameState 实例焦点设置为: {self.user_focus}")

    def add_focus(self, entity_ref: TypedID):
        """添加一个 TypedID 到焦点列表 (如果不存在)。"""
        if entity_ref not in self.user_focus:
            self.user_focus.append(entity_ref)
            self.user_focus.sort(key=str)
            logging.debug(f"GameState 实例添加焦点: {entity_ref}")

    def remove_focus(self, entity_ref: TypedID):
        """从焦点列表中移除一个 TypedID。"""
        try:
            self.user_focus.remove(entity_ref)
            logging.debug(f"GameState 实例移除焦点: {entity_ref}")
        except ValueError:
            pass # 不存在则忽略

    def clear_focus(self):
        """清除所有用户焦点。"""
        if self.user_focus:
            logging.debug("GameState 实例清除所有焦点。")
            self.user_focus = []

    def get_current_focus(self) -> List[TypedID]:
        """获取当前的用户焦点列表。"""
        return self.user_focus

    # 注意：旧的 find_entity, save_game, load_game, reset 方法已移除。
    # reset 的逻辑应由 GameManager 在创建初始块时处理。
    # 查找逻辑需要 BlockManager 提供，基于特定块的 WorldState 快照。
    # 存档/读档逻辑属于 GameManager，需处理整个 Block 结构。

    # Pydantic 模型配置
    model_config = {
        'validate_assignment': True, # 赋值时验证类型
        'frozen': False # 允许修改实例属性
    }

# --- 不再需要 SaveData 类 ---
# class SaveData(BaseModel): ...

# --- 不再需要 load_game 函数 ---
# def load_game(...) -> ...: