# core/command_processor.py
import logging
from abc import ABC, abstractmethod
from typing import List, Dict, Any, Literal, Tuple, Optional, Union, Type, cast

from world_state import WorldState, Item, Character, Place, AnyEntity, BaseEntity


# --- 辅助函数 ---

def _create_entity_instance(entity_type: Literal["Item", "Character", "Place"], entity_id: str, is_placeholder: bool, context: str) -> AnyEntity:
    """根据类型创建新的实体实例，可能是空白或占位符。"""
    model_class: Type[BaseEntity]
    if entity_type == "Item":
        model_class = Item
    elif entity_type == "Character":
        model_class = Character
    elif entity_type == "Place":
        model_class = Place
    else:
        raise ValueError(f"[{context}] 无效的实体类型 '{entity_type}'")

    new_entity = model_class(entity_id=entity_id, entity_type=entity_type)

    if is_placeholder:
        warning_prefix = "Warning: Missing"
        placeholder_name = f"{warning_prefix} {entity_type} [{entity_id}] (Auto-created by: {context})"
        # 使用 set_attribute 设置占位符名称，world=None 避免不必要的副作用
        new_entity.set_attribute('name', placeholder_name, world=None)
        logging.info(f"[{context}] 创建了占位符实体 '{entity_id}' ({entity_type})，名称: '{placeholder_name}'")
    else:  # 空白实体
        logging.info(f"[{context}] 创建了新的空白实体 '{entity_id}' ({entity_type})")
        # 空白实体不需要特殊名称，会由后续的 _apply_modifications 设置

    return new_entity


def _ensure_entity(world: WorldState,
                   entity_id: str,
                   expected_type: Literal["Item", "Character", "Place"],  # Create/Modify/Transfer 都需要提供期望类型
                   create_mode: Optional[Literal['placeholder', 'blank']] = None,  # 'blank' 用于 Create, 'placeholder' 用于 Modify/Transfer 目标
                   context: str = "未知操作") -> BaseEntity:
    """
    查找实体，如果找不到则根据 create_mode 创建。
    返回找到或创建的实体实例。如果无法查找或创建，则抛出错误。
    现在 expected_type 是必须的。
    """
    logging.debug(f"[{context}] 确保实体存在: ID='{entity_id}', ExpectedType='{expected_type}', CreateMode='{create_mode}'")

    entity = world.find_entity(entity_id, include_destroyed=False)

    if entity:
        # 实体存在且未销毁
        logging.debug(f"[{context}] 找到现有实体 '{entity_id}' ({entity.entity_type})")
        # 强制类型检查：如果找到的实体类型与指令期望的不符，则报错
        if entity.entity_type != expected_type:
            logging.error(f"[{context}] 实体类型不匹配！找到的实体 '{entity_id}' 是 {entity.entity_type}，但指令期望的是 {expected_type}。")
            raise TypeError(f"实体类型不匹配：'{entity_id}' 是 {entity.entity_type} 而非 {expected_type}")
        return entity
    else:
        # 实体不存在或已销毁
        logging.debug(f"[{context}] 实体 '{entity_id}' 不存在或已销毁。CreateMode='{create_mode}'")
        if create_mode:
            # 需要创建
            entity_type_to_create = expected_type  # 使用指令指定的类型

            # 检查是否是从已销毁状态恢复
            destroyed_entity = world.find_entity(entity_id, include_destroyed=True)
            if destroyed_entity:
                logging.warning(f"[{context}] 恢复已销毁的实体 '{entity_id}' 为 '{create_mode}' 模式 (类型: {entity_type_to_create})。")
                # 恢复核心状态
                destroyed_entity._dynamic_attributes.clear()  # 清空旧属性
                destroyed_entity.set_attribute('is_destroyed', False, world=None)
                # 确保类型正确
                if destroyed_entity.entity_type != entity_type_to_create:
                    logging.warning(f"[{context}] 恢复实体 '{entity_id}' 时，类型从 {destroyed_entity.entity_type} 强制改为 {entity_type_to_create}。")
                    destroyed_entity.set_attribute('entity_type', entity_type_to_create, world=None)

                # 设置名称（如果是占位符模式）
                if create_mode == 'placeholder':
                    warning_prefix = "Warning: Missing"
                    placeholder_name = f"{warning_prefix} {entity_type_to_create} [{entity_id}] (Auto-created by: {context})"
                    destroyed_entity.set_attribute('name', placeholder_name, world=None)
                else:  # blank 模式，不需要设置名称，后续 apply 会处理
                    pass
                return destroyed_entity
            else:
                # 完全新建
                new_entity = _create_entity_instance(entity_type_to_create, entity_id, is_placeholder=(create_mode == 'placeholder'), context=context)
                world.add_entity(new_entity)
                return new_entity
        else:
            # 不需要创建，但实体找不到
            logging.error(f"[{context}] 操作失败：找不到目标实体 '{entity_id}' 且不允许创建。")
            raise ValueError(f"[{context}] 失败: 找不到实体 '{entity_id}' 或已销毁。")


def _ensure_target_id(world: WorldState,
                      target_spec: Union[str, Tuple[str, str]],
                      expected_container_type: Union[Type[Character], Type[Place], Tuple[Type[Character], Type[Place]]],
                      context: str) -> str:
    """
    解析目标规范，确保目标容器存在（找不到时创建占位符），返回其 ID。
    """
    # (此函数逻辑不变)
    logging.debug(f"[{context}] 解析并确保目标容器存在: Spec='{target_spec}'")
    target_id: str = ""
    provided_type_str: Optional[Literal["Character", "Place"]] = None  # 容器只能是 Character 或 Place
    if isinstance(target_spec, tuple) and len(target_spec) == 2:
        raw_type, target_id = target_spec
        if isinstance(raw_type, str) and raw_type.capitalize() in ["Character", "Place"]:
            provided_type_str = cast(Literal["Character", "Place"], raw_type.capitalize())
        else:
            raise ValueError(f"[{context}] 无效的目标容器类型 '{raw_type}' 在规范 '{target_spec}' 中。")
    elif isinstance(target_spec, str):
        target_id = target_spec
    else:
        raise TypeError(f"[{context}] 无效的目标规范格式: {repr(target_spec)}")
    if not target_id: raise ValueError(f"[{context}] 无法从规范 '{repr(target_spec)}' 提取有效目标 ID")

    # 使用 _ensure_entity 查找或创建占位符
    try:
        # 确定期望的 Pydantic 类
        expected_pydantic_type: Optional[Literal["Character", "Place"]] = None
        if isinstance(expected_container_type, tuple):  # (Character, Place)
            # 如果 AI 提供了类型，用 AI 的；否则无法确定是 C 还是 P，让 ensure_entity 报错（除非找到）
            expected_pydantic_type = provided_type_str
        elif expected_container_type == Character:
            expected_pydantic_type = "Character"
        elif expected_container_type == Place:
            expected_pydantic_type = "Place"

        if not expected_pydantic_type and not world.find_entity(target_id):
            raise ValueError(f"[{context}] 无法确定目标容器 '{target_id}' 的类型 (期望: {expected_container_type}) 且实体不存在。")

        # 调用 _ensure_entity，强制创建为占位符
        target_entity = _ensure_entity(world, target_id, expected_pydantic_type, create_mode='placeholder', context=context + " 容器")  # type: ignore
        return target_entity.entity_id
    except Exception as e:
        logging.error(f"[{context}] 确保目标容器 '{target_id}' 存在时失败: {e}", exc_info=True)
        raise e


def _apply_modifications_to_entity(entity: BaseEntity, updates: Dict[str, Tuple[str, Any]], world: WorldState, command_context: str):
    """将一系列修改应用到 *给定的* 实体实例上。"""
    entity_id = entity.entity_id
    logging.debug(f"[{command_context}] '{entity_id}': 开始应用修改: {updates}")
    for key, op_value_tuple in updates.items():
        try:
            logging.debug(f"[{command_context}] '{entity_id}': 应用 -> {key} {op_value_tuple}")
            entity.modify_attribute(key, op_value_tuple, world)
        except (ValueError, TypeError) as e:
            logging.error(f"[{command_context}] '{entity_id}': 应用 '{key}={op_value_tuple}' 时失败: {e}", exc_info=True)
            raise e
        except Exception as e:
            logging.exception(f"[{command_context}] '{entity_id}': 应用 '{key}={op_value_tuple}' 时发生意外错误:")
            raise e
    logging.info(f"[{command_context}] '{entity_id}' 应用修改完毕。")


# --- 指令接口和实现 ---
class ICommand(ABC):
    def __init__(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str, params: Dict[str, Any]):
        self.entity_type = entity_type
        self.entity_id = entity_id
        self.params = cast(Dict[str, Tuple[str, Any]], params)
        logging.debug(f"初始化指令: {self.__class__.__name__} (Type={entity_type}, ID={entity_id}, Params={params})")

    @abstractmethod
    def execute(self, world: WorldState) -> None: pass


class ModifyCommand(ICommand):
    """处理 @Modify 指令。查找实体（找不到时报错），然后应用修改。"""

    def execute(self, world: WorldState) -> None:
        context = "Modify"
        # 查找实体，不允许创建 (create_mode=None)
        entity = _ensure_entity(world, self.entity_id, expected_type=self.entity_type, create_mode=None, context=context)
        _apply_modifications_to_entity(entity, self.params, world, context)


class CreateCommand(ICommand):
    """处理 @Create 指令。查找或创建空白实体，然后应用修改，覆盖现有属性。"""

    def execute(self, world: WorldState) -> None:
        context = "Create"
        # 查找或创建空白实体 (create_mode='blank')
        entity = _ensure_entity(world, self.entity_id, expected_type=self.entity_type, create_mode='blank', context=context)
        # 不再需要重置逻辑，直接应用参数覆盖
        _apply_modifications_to_entity(entity, self.params, world, context)


class TransferCommand(ICommand):
    """处理 @Transfer 指令：查找源实体，解析目标ID，然后对源实体应用位置修改。"""

    def __init__(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str, params: Dict[str, Any]):
        if entity_type not in ["Item", "Character"]: raise TypeError(f"Transfer 指令只适用于 Item 或 Character，而非 {entity_type}")
        super().__init__(cast(Literal["Item", "Character"], entity_type), entity_id, params)
        target_spec_tuple = self.params.get('target')
        if not target_spec_tuple or target_spec_tuple[0] != '=': raise ValueError(f"Transfer 命令 ({self.entity_id}) 缺少必需的 'target' 赋值参数。")
        self.target_spec = target_spec_tuple[1]  # 值可能是 Tuple 或 ID
        if self.target_spec is None: raise ValueError("Transfer 指令的目标不能为空。")

    def execute(self, world: WorldState) -> None:
        context = "Transfer"

        # 1. 查找源实体 (找不到会报错, create_mode=None)
        source_entity = _ensure_entity(world, self.entity_id, expected_type=self.entity_type, create_mode=None, context=context + " 源")

        # 2. 确定目标属性名和期望容器类型
        target_attr_name: str = ""
        expected_container_type: Union[Type[Character], Type[Place], Tuple[Type[Character], Type[Place]]]
        if self.entity_type == "Item":
            target_attr_name = 'location'
            expected_container_type = (Character, Place)
        else:  # Character
            target_attr_name = 'current_place'
            expected_container_type = Place

        # 3. 查找或创建目标容器 ID (使用 _ensure_target_id)
        resolved_target_id = _ensure_target_id(world, self.target_spec, expected_container_type, context + " 目标")

        # 4. 构造位置修改参数
        location_update: Dict[str, Tuple[str, Any]] = {target_attr_name: ('=', resolved_target_id)}

        # 5. 对源实体应用位置修改
        _apply_modifications_to_entity(source_entity, location_update, world, context)


class DestroyCommand(ICommand):
    """处理 @Destroy 指令 (逻辑不变)。"""

    def execute(self, world: WorldState) -> None:
        # (Destroy 逻辑保持不变)
        logging.info(f"执行 Destroy: Type={self.entity_type}, ID={self.entity_id}")
        entity = world.find_entity(self.entity_id, include_destroyed=True)
        if not entity: logging.warning(f"Destroy: 找不到实体 ID '{self.entity_id}'"); return
        if entity.is_destroyed: logging.info(f"实体 '{self.entity_id}' 已销毁"); return
        if entity.entity_type != self.entity_type: raise TypeError(f"Destroy: 类型不匹配 ({entity.entity_type} vs {self.entity_type})")
        entity_name = entity.get_attribute('name', f'<{entity.entity_id}>')
        logging.info(f"开始销毁实体: {entity.entity_type} ID='{entity.entity_id}', Name='{entity_name}'")
        items_to_clear: List[str] = []
        contents_to_clear: List[str] = []
        location_key: Optional[str] = None
        if isinstance(entity, Item):
            location_key = 'location'
        elif isinstance(entity, Character):
            location_key = 'current_place'
            items_to_clear = list(entity.get_attribute('has_items', []))
        elif isinstance(entity, Place):
            contents_to_clear = list(entity.get_attribute('contents', []))
        entity.set_attribute('is_destroyed', True, world=None)
        if location_key:
            try:
                entity.set_attribute(location_key, None, world)
            except Exception as e:
                logging.error(f"销毁 '{entity.entity_id}': 解除位置关系时出错(忽略): {e}")
        if items_to_clear:
            logging.debug(f"角色 '{self.entity_id}' 销毁，清物品位置...")
            for item_id in items_to_clear:
                if (item := world.find_entity(item_id)) and isinstance(item, Item) and not item.is_destroyed:
                    try:
                        item.set_attribute('location', None, world=None)
                    except Exception as e:
                        logging.error(f"...清除物品 '{item_id}' 位置出错(忽略): {e}")
            try:
                entity.set_attribute('has_items', [], world=None)
            except Exception as e:
                logging.error(f"...清空 has_items 出错(忽略): {e}")
        if contents_to_clear:
            logging.debug(f"地点 '{self.entity_id}' 销毁，清内容物位置...")
            for content_id in contents_to_clear:
                if (content := world.find_entity(content_id)) and not content.is_destroyed:
                    content_loc_key = 'location' if isinstance(content, Item) else 'current_place' if isinstance(content, Character) else None
                    if content_loc_key:
                        try:
                            content.set_attribute(content_loc_key, None, world=None)
                        except Exception as e:
                            logging.error(f"...清除内容物 '{content_id}' 位置出错(忽略): {e}")
            try:
                entity.set_attribute('contents', [], world=None)
                entity.set_attribute('exits', {}, world=None)
            except Exception as e:
                logging.error(f"...清空 contents/exits 出错(忽略): {e}")
        logging.info(f"实体 '{self.entity_id}' 销毁完成。")


# --- CommandExecutor (保持不变) ---
class CommandExecutor:
    COMMAND_MAP: Dict[str, Type[ICommand]] = {
        "create": CreateCommand,
        "modify": ModifyCommand,
        "destroy": DestroyCommand,
        "transfer": TransferCommand
    }

    @staticmethod
    def execute_commands(parsed_commands: List[Dict[str, Any]], world: WorldState) -> None:
        if not parsed_commands: return
        logging.info(f"开始执行 {len(parsed_commands)} 条指令...")
        sorted_commands = sort_commands_for_execution(parsed_commands)
        executed_count = 0
        for i, cmd_data in enumerate(sorted_commands):
            command_name = cmd_data.get("command")
            entity_type = cmd_data.get("entity_type")
            entity_id = cmd_data.get("entity_id")
            params = cmd_data.get("params", {})
            command_class = CommandExecutor.COMMAND_MAP.get(command_name.lower())
            if not command_class: logging.warning(f"跳过未知指令: '{command_name}'"); continue
            logging.debug(f"准备执行指令 #{i + 1}: {command_name} {entity_type} {entity_id}")
            try:
                command_instance = command_class(entity_type=entity_type, entity_id=entity_id, params=params)  # type: ignore
                command_instance.execute(world)
                executed_count += 1
                logging.debug(f"指令 #{i + 1} ({command_name} {entity_id}) 执行成功。")
            except (ValueError, TypeError) as e:
                logging.error(f"执行指令 #{i + 1} {cmd_data} 时发生错误: {e}", exc_info=True)
                raise e
            except Exception as e:
                logging.exception(f"执行指令 #{i + 1} {cmd_data} 时发生意外错误:")
                raise e
        logging.info(f"成功执行 {executed_count}/{len(sorted_commands)} 条指令。")


# --- sort_commands_for_execution (保持不变) ---
def sort_commands_for_execution(commands: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    def get_priority(command_data: Dict[str, Any]) -> int:
        command = command_data.get("command", "").lower()
        entity_type = command_data.get("entity_type", "")
        if command == "create":
            if entity_type == "Place":
                return 0
            elif entity_type == "Character":
                return 1
            elif entity_type == "Item":
                return 2
        elif command in ["modify", "transfer"]:
            return 3
        elif command == "destroy":
            return 4
        return 99

    return sorted(commands, key=get_priority)
