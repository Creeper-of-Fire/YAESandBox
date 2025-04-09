# processing/command_processor.py
import logging
from abc import ABC, abstractmethod
from typing import List, Dict, Any, Literal, Tuple, Optional, Union, Type, cast

# 导入 core 中的 WorldState 和实体类
from core.world_state import WorldState, Item, Character, Place, AnyEntity, BaseEntity

# --- 辅助函数 _apply_modifications_to_entity (保持不变) ---
def _apply_modifications_to_entity(entity: BaseEntity, updates: Dict[str, Tuple[str, Any]], world: WorldState, command_context: str):
    """
    将一系列修改应用到 *给定的* 实体实例上。
    直接调用 entity.modify_attribute。
    """
    entity_id = entity.entity_id
    entity_type = entity.entity_type # 获取类型用于日志
    logging.debug(f"[{command_context}] '{entity_type}:{entity_id}': 开始应用修改: {updates}")
    for key, op_value_tuple in updates.items():
        try:
            logging.debug(f"[{command_context}] '{entity_type}:{entity_id}': 应用 -> {key} {op_value_tuple}")
            entity.modify_attribute(key, op_value_tuple, world)
        except (ValueError, TypeError) as e:
            logging.error(f"[{command_context}] '{entity_type}:{entity_id}': 应用 '{key}={op_value_tuple}' 时失败: {e}", exc_info=True)
            raise e
        except Exception as e:
            logging.exception(f"[{command_context}] '{entity_type}:{entity_id}': 应用 '{key}={op_value_tuple}' 时发生意外错误:")
            raise e
    logging.info(f"[{command_context}] '{entity_type}:{entity_id}' 应用修改完毕。")


# --- 指令接口和实现 (修改：查找使用新的 find_entity) ---
class ICommand(ABC):
    def __init__(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str, params: Dict[str, Any]):
        self.entity_type = entity_type
        self.entity_id = entity_id
        self.params = cast(Dict[str, Tuple[str, Any]], params)
        logging.debug(f"初始化指令: {self.__class__.__name__} (Type={entity_type}, ID={entity_id}, Params={params})")

    @abstractmethod
    def execute(self, world: WorldState) -> None: pass

    # _get_entity_class 保持不变
    @staticmethod
    def _get_entity_class(entity_type: Literal["Item", "Character", "Place"]) -> Type[BaseEntity]:
        if entity_type == "Item": return Item
        if entity_type == "Character": return Character
        if entity_type == "Place": return Place
        raise ValueError(f"未知的实体类型: {entity_type}")


class ModifyCommand(ICommand):
    """处理 @Modify 指令。查找实体（必须存在且未销毁），然后应用修改。"""
    def execute(self, world: WorldState) -> None:
        context = "Modify"
        # 使用新的 find_entity，需要类型
        entity = world.find_entity(self.entity_id, self.entity_type, include_destroyed=False)
        if not entity:
            logging.error(f"[{context}] 操作失败：找不到要修改的实体 '{self.entity_type}:{self.entity_id}' 或已被销毁。")
            raise ValueError(f"Modify 失败: 找不到实体 '{self.entity_type}:{self.entity_id}' 或已销毁。")
        # 类型匹配由 find_entity 保证，无需再次检查

        # 直接调用辅助函数应用修改
        _apply_modifications_to_entity(entity, self.params, world, context)


class CreateCommand(ICommand):
    """
    处理 @Create 指令。
    查找同类型实体，如果不存在则创建；如果已销毁则恢复。
    然后应用所有参数作为修改，覆盖现有属性。
    """
    def execute(self, world: WorldState) -> None:
        context = "Create"
        # 使用新的 find_entity 查找同类型实体
        entity = world.find_entity(self.entity_id, self.entity_type, include_destroyed=True)

        if entity:
            # 实体存在 (同类型)
            if entity.is_destroyed:
                logging.info(f"[{context}] 实体 '{self.entity_type}:{self.entity_id}' 已销毁，将恢复并覆盖属性。")
                entity._dynamic_attributes.clear()
                entity.set_attribute('is_destroyed', False, world=None)
            else:
                logging.info(f"[{context}] 实体 '{self.entity_type}:{self.entity_id}' 已存在，将覆盖其属性。")
                # 不需要清除，modify 会覆盖
        else:
            # 实体完全不存在 (同类型)
            logging.info(f"[{context}] 实体 '{self.entity_type}:{self.entity_id}' 不存在，将创建新的实例。")
            entity_class = self._get_entity_class(self.entity_type)
            entity = entity_class(entity_id=self.entity_id, entity_type=self.entity_type)
            world.add_entity(entity) # add_entity 现在只检查同类型冲突

        # 应用所有参数进行修改/覆盖
        _apply_modifications_to_entity(entity, self.params, world, context)


class TransferCommand(ICommand):
    """
    处理 @Transfer 指令。
    查找源实体，然后调用其 set_attribute 修改位置。
    """
    def __init__(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str, params: Dict[str, Any]):
        if entity_type not in ["Item", "Character"]:
            raise TypeError(f"Transfer 指令只适用于 Item 或 Character，而非 {entity_type}")
        super().__init__(cast(Literal["Item", "Character"], entity_type), entity_id, params)

        target_op_value = self.params.get('target')
        if not target_op_value or target_op_value[0] != '=':
            raise ValueError(f"Transfer 命令 ({self.entity_type}:{self.entity_id}) 缺少必需的 'target' 赋值参数。")

        self.target_value: Optional[Tuple[Literal["Item", "Character", "Place"], str]] = None
        parsed_value = target_op_value[1]

        # 验证解析后的值是否为 (Type, ID) 元组 (逻辑不变)
        if isinstance(parsed_value, tuple) and len(parsed_value) == 2 and \
           isinstance(parsed_value[0], str) and isinstance(parsed_value[1], str) and \
           parsed_value[0].capitalize() in ["Item", "Character", "Place"]:
            self.target_value = cast(Tuple[Literal["Item", "Character", "Place"], str],
                                     (parsed_value[0].capitalize(), parsed_value[1]))
        else:
            raise ValueError(f"Transfer 指令的目标值必须是有效的实体引用元组 (Type, ID)，但解析为: {repr(parsed_value)}")

        # 根据实体类型检查目标类型是否合理 (逻辑不变)
        if self.entity_type == "Item" and self.target_value[0] not in ["Character", "Place"]:
             raise ValueError(f"Item '{self.entity_id}' 不能转移到 {self.target_value[0]} '{self.target_value[1]}'")
        if self.entity_type == "Character" and self.target_value[0] != "Place":
             raise ValueError(f"Character '{self.entity_id}' 只能转移到 Place，而非 {self.target_value[0]} '{self.target_value[1]}'")

    def execute(self, world: WorldState) -> None:
        context = "Transfer"

        # 1. 查找源实体 (需要类型)
        source_entity = world.find_entity(self.entity_id, self.entity_type, include_destroyed=False)
        if not source_entity:
            logging.error(f"[{context}] 操作失败：找不到要转移的源实体 '{self.entity_type}:{self.entity_id}' 或已被销毁。")
            raise ValueError(f"Transfer 失败: 找不到源实体 '{self.entity_type}:{self.entity_id}' 或已销毁。")
        # 类型匹配由 find_entity 保证

        # 2. 确定目标属性名 (不变)
        target_attr_name: str = 'location' if self.entity_type == "Item" else 'current_place'

        # 3. 直接调用 set_attribute (不变)
        logging.info(f"[{context}] 尝试将 '{source_entity.entity_type}:{source_entity.entity_id}' 的 '{target_attr_name}' 设置为 {self.target_value}")
        try:
            source_entity.set_attribute(target_attr_name, self.target_value, world)
            logging.info(f"[{context}] '{source_entity.entity_type}:{source_entity.entity_id}' 转移成功。")
        except Exception as e:
            logging.error(f"[{context}] 对实体 '{source_entity.entity_type}:{source_entity.entity_id}' 执行 set_attribute('{target_attr_name}', {self.target_value}) 时失败: {e}", exc_info=True)
            raise e


class DestroyCommand(ICommand):
    """处理 @Destroy 指令。"""
    def execute(self, world: WorldState) -> None:
        context = "Destroy"
        logging.info(f"[{context}] 执行: Type={self.entity_type}, ID={self.entity_id}")
        # 使用新的 find_entity 查找
        entity = world.find_entity(self.entity_id, self.entity_type, include_destroyed=True)

        if not entity: logging.warning(f"[{context}]: 找不到实体 '{self.entity_type}:{self.entity_id}'"); return
        if entity.is_destroyed: logging.info(f"[{context}]: 实体 '{self.entity_type}:{self.entity_id}' 已销毁"); return
        # 类型匹配由 find_entity 保证

        entity_name = entity.get_attribute('name', f'<{entity.entity_type}:{entity.entity_id}>')
        logging.info(f"[{context}]: 开始销毁实体: {entity.entity_type} ID='{entity.entity_id}', Name='{entity_name}'")

        # --- 获取需要清理的关系 (使用 get_attribute 获取元组引用) ---
        items_to_clear_refs: List[Tuple[Literal["Item"], str]] = [] # (Item, ID)
        contents_to_clear_refs: List[Tuple[Literal["Item", "Character"], str]] = [] # (Type, ID)
        location_key: Optional[str] = None
        old_location_ref: Optional[Tuple[str,str]] = None

        if isinstance(entity, Item):
            location_key = 'location'
            old_location_ref = entity.get_attribute('location') # 假设返回 (Type, ID) 或 None
        elif isinstance(entity, Character):
            location_key = 'current_place'
            old_location_ref = entity.get_attribute('current_place')
            items_to_clear_refs = list(entity.get_attribute('has_items', [])) # 假设返回 List[(Type, ID)]
        elif isinstance(entity, Place):
            contents_to_clear_refs = list(entity.get_attribute('contents', [])) # 假设返回 List[(Type, ID)]

        # --- 标记实体为已销毁 (先做) ---
        entity.set_attribute('is_destroyed', True, world=None)

        # --- 手动清理关系 (查找使用 find_entity_by_ref) ---
        # 1. 从旧容器中移除
        if old_location_ref:
            # world_state 需要有 find_entity_by_ref 方法
            container = world.find_entity_by_ref(old_location_ref)
            if container and not container.is_destroyed:
                 try:
                     attr_to_modify = 'contents' if isinstance(container, Place) else 'has_items' if isinstance(container, Character) else None
                     if attr_to_modify:
                         container.modify_attribute(attr_to_modify, ('-', (entity.entity_type, entity.entity_id)), world)
                 except Exception as e:
                      logging.error(f"[{context}] '{entity.entity_type}:{entity.entity_id}': 从容器 '{old_location_ref}' 移除时出错(忽略): {e}")

        # 2. 清理自身的 location/current_place
        if location_key:
            try: entity.set_attribute(location_key, None, world=None)
            except Exception as e: logging.warning(f"[{context}] '{entity.entity_type}:{entity.entity_id}': 清理自身 {location_key} 出错(忽略): {e}")

        # 3. 处理角色拥有的物品
        if items_to_clear_refs:
            logging.debug(f"[{context}] 角色 '{self.entity_type}:{self.entity_id}' 销毁，清空其物品位置...")
            for item_ref in items_to_clear_refs:
                item = world.find_entity_by_ref(item_ref) # 使用 ref 查找
                if item and isinstance(item, Item) and not item.is_destroyed:
                    try: item.set_attribute('location', None, world=None)
                    except Exception as e: logging.error(f"...清除物品 '{item_ref}' 位置出错(忽略): {e}")
            try: entity.set_attribute('has_items', [], world=None)
            except Exception as e: logging.error(f"...清空 has_items 出错(忽略): {e}")

        # 4. 处理地点的内容物
        if contents_to_clear_refs:
            logging.debug(f"[{context}] 地点 '{self.entity_type}:{self.entity_id}' 销毁，清空其内容物位置...")
            for content_ref in contents_to_clear_refs:
                content = world.find_entity_by_ref(content_ref) # 使用 ref 查找
                if content and not content.is_destroyed:
                    content_loc_key = 'location' if isinstance(content, Item) else 'current_place' if isinstance(content, Character) else None
                    if content_loc_key:
                        try: content.set_attribute(content_loc_key, None, world=None)
                        except Exception as e: logging.error(f"...清除内容物 '{content_ref}' 位置出错(忽略): {e}")
            try:
                entity.set_attribute('contents', [], world=None)
                entity.set_attribute('exits', {}, world=None)
            except Exception as e: logging.error(f"...清空 contents/exits 出错(忽略): {e}")

        logging.info(f"[{context}]: 实体 '{self.entity_type}:{self.entity_id}' 销毁完成。")


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
        # (执行逻辑保持不变)
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
                command_instance = command_class(entity_type=entity_type, entity_id=entity_id, params=params)
                command_instance.execute(world)
                executed_count += 1
                logging.debug(f"指令 #{i + 1} ({command_name} {entity_type}:{entity_id}) 执行成功。")
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
            if entity_type == "Place": return 0
            if entity_type == "Character": return 1
            if entity_type == "Item": return 2
        elif command in ["modify", "transfer"]: return 3
        elif command == "destroy": return 4
        return 99
    return sorted(commands, key=get_priority)