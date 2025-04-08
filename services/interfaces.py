# services/interfaces.py
import logging
from typing import Protocol, List, Dict, Any, Optional, Callable, Union, Tuple
from pathlib import Path

# 导入 WorldState 和实体类型定义 (需要访问它们以进行类型提示)
# 假设它们在 core 包中
try:
    from core.world_state import AnyEntity, WorldState
except ImportError:
    # 如果在设计阶段无法直接导入，可以使用 TypeVar 或 Any，但最好能导入
    logging.warning("无法导入 core.world_state，部分类型提示将使用 Any。")
    AnyEntity = Any
    WorldState = Any


# --- 工作流引擎相关类型 ---

# 工作流执行完毕后调用的回调函数签名
# 参数:
#   success (bool): 工作流是否成功执行 (包括 AI 调用和后续处理)。
#   result (Optional[Dict[str, Any]]): 成功时返回的结果字典 (例如 AI 生成的文本、提取的数据等)。
#   error_message (Optional[str]): 失败时的错误信息。
WorkflowCallback = Callable[[bool, Optional[Dict[str, Any]], Optional[str]], None]

# --- 服务接口定义 ---

class IGameStateProvider(Protocol):
    """
    提供对游戏世界状态只读访问的接口。
    实现者需要确保线程安全（如果从不同线程访问）。
    """

    def get_world_state(self) -> WorldState:
        """获取当前完整的 WorldState 对象 (慎用，可能很大)。"""
        ...

    def find_entity(self, entity_id: str, include_destroyed: bool = False) -> Optional[AnyEntity]:
        """通过 ID 查找任何类型的实体。"""
        ...

    def find_entity_by_name(self, name: str, entity_type: Optional[str] = None, include_destroyed: bool = False) -> Optional[AnyEntity]:
        """按名称查找实体 (效率较低)。"""
        ...

    def get_player_character_id(self) -> Optional[str]:
        """获取当前玩家控制的角色 ID (如果定义了的话)。"""
        # 注意: "玩家" 的概念可能需要 GameState 或某个管理器来维护
        # 这里假设 Provider 能以某种方式知道谁是玩家
        ...

    def get_player_character(self) -> Optional[AnyEntity]:
        """获取当前玩家控制的角色实体。"""
        ...

    def get_current_focus(self) -> List[str]:
        """获取当前的用户焦点列表 (由 GameState 管理)。"""
        ...

    # 可以根据需要添加更多具体的查询方法，例如：
    # def get_items_in_location(self, location_id: str) -> List[AnyEntity]: ...
    # def get_characters_in_location(self, location_id: str) -> List[AnyEntity]: ...


class ICommandSubmitter(Protocol):
    """
    接收指令并提交以修改游戏世界状态的接口。
    实现者需要确保指令在正确的线程（通常是主线程）执行，
    并在状态改变后触发适当的通知（例如发出信号）。
    """

    # 指令的格式通常是一个字典列表，每个字典代表一条指令
    # 例如: {"command": "Modify", "entity_type": "Item", "entity_id": "sword-01", "params": {"quality": ("=", "shiny")}}
    # 或者使用更结构化的 Command 对象也可以
    CommandDict = Dict[str, Any]

    def submit_commands(self, commands: List[CommandDict], source: Optional[str] = None) -> None:
        """
        提交一个或多个指令以供执行。

        Args:
            commands: 要执行的指令列表。
            source: (可选) 指令来源的标识符 (例如 "NarrativeWidget", "WorkflowEngine")，用于调试。
        """
        ...

    # 可能需要一个信号，在指令成功执行并应用到 GameState 后发出
    # 这个信号应该在实现类中定义，例如使用 PySide6.QtCore.Signal
    # game_state_updated = Signal() # 示例


class IWorkflowEngine(Protocol):
    """
    执行预定义工作流（通常涉及与 AI 交互）的接口。
    实现者负责处理 AI 调用（可能在后台线程）、解析响应、
    以及通过回调函数返回结果。
    """

    def execute_workflow(self,
                         config_id: str,
                         context: Dict[str, Any],
                         callback: WorkflowCallback) -> None:
        """
        异步执行指定的工作流。

        Args:
            config_id: 要执行的工作流配置的唯一标识符。
            context: 执行工作流所需的上下文信息字典 (例如用户输入、当前实体 ID 等)。
            callback: 工作流执行完毕后调用的回调函数。
                      实现者必须确保回调在合适的线程（通常是请求发起的线程或主线程）被调用。
        """
        ...

    def load_workflow_configs(self, config_source: Union[Path, Dict[str, Any]]) -> None:
        """
        加载工作流配置。配置可以来自文件或字典。
        """
        ...

    def get_workflow_config(self, config_id: str) -> Optional[Dict[str, Any]]:
        """
        获取已加载的工作流配置详情 (用于调试或内省)。
        """
        ...

# --- (可选) 工作流配置接口/结构约定 ---
# 我们可以先用字典来表示配置，后续如果需要更强的类型检查，可以定义 Pydantic 模型

# 示例 Workflow Config 字典结构 (仅为示意):
# {
#     "workflow_id_example": {
#         "description": "一个示例工作流",
#         "prompt_template": "用户说：{user_input}。请回应。",
#         "context_requirements": ["user_input"], # 需要从 context 字典获取的键
#         "ai_model_params": {"temperature": 0.7},
#         "post_processing_script": None, # (可选) AI 返回后执行的 Python 脚本或函数引用
#         "output_mapping": { # 如何构建返回给 callback 的 result 字典
#             "ai_response_text": "{raw_ai_output}"
#         }
#     },
#     "generate_description": {
#         "description": "为实体生成更详细的描述",
#         "prompt_template": "这是一个 {entity_type}，ID 是 {entity_id}，当前属性：\n{entity_attributes}\n请生成一段更生动的描述。",
#         "context_requirements": ["entity_id"],
#         "pre_processing_script": "fetch_entity_attributes", # 需要执行脚本获取 entity_attributes
#         "output_mapping": {
#             "description": "{parsed_description}" # 假设有解析步骤
#         }
#     }
# }