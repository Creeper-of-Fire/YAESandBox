# core/block.py
import uuid
import logging
from typing import Optional, List, Dict, Any
from pydantic import BaseModel, Field
from datetime import datetime
import copy # 用于深拷贝创建快照

# 导入 WorldState 和 GameState (假设它们在相应模块中)
from core.world_state import WorldState
from core.game_state import GameState

# 导入可能的指令类型 (如果需要存储解析后的指令)
# from core.commands import ParsedCommand # 假设有这样的类型

class Block(BaseModel):
    """
    叙事块，构成游戏历史和状态的核心单元。
    包含不同阶段的 WorldState 快照和对应的 GameState。
    """

    # --- 核心标识符 ---
    block_id: uuid.UUID = Field(default_factory=uuid.uuid4, description="块的唯一标识符")
    parent_block_id: Optional[uuid.UUID] = Field(None, description="父块的 ID，根节点为 None")

    # --- 叙事内容 ---
    full_text: str = Field(..., description="AI 生成的完整叙事文本")
    # rich_text_config: Dict[str, Any] = Field(default_factory=dict, description="用于前端渲染富文本的配置") # 可选

    # --- 关联的指令 (可能只在内存中处理，不一定持久化在 Block 对象中) ---
    # parsed_commands: List[ParsedCommand] = Field(default_factory=list, description="由此块文本解析出的 AI 指令")

    # --- 状态快照 ---
    # 注意：原型阶段直接存储对象实例 (深拷贝创建)
    ws_input: WorldState = Field(..., description="块的输入 WorldState (父块的 ws_post_user 深拷贝)")
    ws_post_ai: WorldState = Field(..., description="应用 AI 指令后的 WorldState (基于 ws_input 深拷贝 + AI 指令)")
    ws_post_user: WorldState = Field(..., description="用户交互/修改后的最终 WorldState (基于 ws_post_ai 深拷贝 + 用户修改)")

    game_state_input: GameState = Field(..., description="块的输入 GameState (父块的 game_state_post_user 深拷贝)")
    game_state_post_user: GameState = Field(..., description="用户交互/修改后的最终 GameState (基于 game_state_input 深拷贝 + 用户/流程修改)")

    # --- 元数据 ---
    created_at: datetime = Field(default_factory=datetime.now, description="块创建时间")
    # workflow_id: Optional[str] = Field(None, description="触发创建此块的工作流 ID") # 可选

    # --- 模型配置 ---
    model_config = {
        'validate_assignment': True,  # 赋值时验证类型
        'frozen': False, # 允许块被修改（例如，固化父块状态时） - 或者设计成只读，通过新块更新？需要斟酌
        # 如果 WorldState/GameState 包含复杂类型，可能需要配置 arbitrary_types_allowed=True
        # 但最好是让 WorldState/GameState 也是 Pydantic 模型
        'arbitrary_types_allowed': True # 允许非 Pydantic 类型如 WorldState, GameState
    }

    # --- 辅助方法 (示例) ---
    @classmethod
    def create_initial_block(cls) -> 'Block':
        """创建一个初始的、空的根块。"""
        logging.info("创建初始根 Block。")
        initial_ws = WorldState()
        initial_gs = GameState()
        # 初始块的所有状态都相同
        return cls(
            full_text="游戏开始。",
            parent_block_id=None,
            ws_input=copy.deepcopy(initial_ws),
            ws_post_ai=copy.deepcopy(initial_ws),
            ws_post_user=copy.deepcopy(initial_ws),
            game_state_input=copy.deepcopy(initial_gs),
            game_state_post_user=copy.deepcopy(initial_gs)
        )

    # 可能还需要其他方法，例如用于更新状态或获取特定快照的方法
    # 但核心逻辑可能放在 BlockManager 中更好

# --- 示例用法 (概念性) ---
if __name__ == '__main__':
    # 创建初始块
    root_block = Block.create_initial_block()
    print(f"根块 ID: {root_block.block_id}")
    print(f"根块输入 WorldState (示例实体数量): {len(root_block.ws_input.get_all_entities())}")
    print(f"根块输入 GameState (示例焦点): {root_block.game_state_input.get_current_focus()}")

    # 模拟创建子块 (实际逻辑会在 GameManager/BlockManager 中)
    # 假设 parent_block 是 root_block
    parent_block = root_block

    # 1. 获取父块最终状态作为新块输入 (深拷贝)
    new_ws_input = copy.deepcopy(parent_block.ws_post_user)
    new_gs_input = copy.deepcopy(parent_block.game_state_post_user)

    # 2. 模拟 AI 处理和用户修改
    # (这里省略调用 AI、解析指令、应用指令、处理冲突等复杂步骤)
    # 假设 AI 指令应用后得到 ws_post_ai
    new_ws_post_ai = copy.deepcopy(new_ws_input)
    # 假设流程修改 GameState
    new_gs_post_user = copy.deepcopy(new_gs_input)
    new_gs_post_user.story_tone = "紧张"
    # 假设用户修改 WorldState
    new_ws_post_user = copy.deepcopy(new_ws_post_ai)
    # (应用用户修改到 new_ws_post_user)

    # 3. 创建新块
    child_block = Block(
        parent_block_id=parent_block.block_id,
        full_text="你走进了一个黑暗的洞穴...",
        ws_input=new_ws_input,
        ws_post_ai=new_ws_post_ai,
        ws_post_user=new_ws_post_user,
        game_state_input=new_gs_input,
        game_state_post_user=new_gs_post_user
    )
    print(f"\n子块 ID: {child_block.block_id}")
    print(f"子块父 ID: {child_block.parent_block_id}")
    print(f"子块最终 GameState 基调: {child_block.game_state_post_user.story_tone}")