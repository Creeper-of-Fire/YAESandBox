# command_processor.py
import logging
from abc import ABC, abstractmethod
from typing import List, Dict, Any, Literal, Tuple, Optional, Union, Type, cast, Set

from world_state import WorldState, Item, Character, Place, AnyEntity, BaseEntity


# --- 辅助函数 (_create_placeholder_entity, _ensure_entity_exists, _remove_from_container, _add_to_container, _update_container_lists - 保持不变) ---
# (省略这些函数的代码，它们与上一个版本相同)
def _create_placeholder_entity(world: WorldState, entity_type: Literal["Item", "Character", "Place"], entity_id: str, context: str) -> AnyEntity:
    warning_prefix = "Warning: Missing"
    placeholder_name = f"{warning_prefix} {entity_type} [{entity_id}] (Auto-created by: {context})"
    logging.info(f"实体 '{entity_id}' 不存在或需恢复，创建/重置为占位符 ({entity_type})，名称: '{placeholder_name}'")
    model_class: Type[BaseEntity]
    existing = world.find_entity(entity_id, include_destroyed=True)
    if entity_type == "Item":
        model_class = Item
    elif entity_type == "Character":
        model_class = Character
    elif entity_type == "Place":
        model_class = Place
    else:
        raise ValueError(f"无效的实体类型 '{entity_type}'")
    if existing:
        logging.warning(f"恢复或重置已存在实体 '{entity_id}' 为占位符 (原 is_destroyed={existing.is_destroyed})。")
        existing.is_destroyed = False
        existing.name = placeholder_name
        existing.entity_type = entity_type
        existing._dynamic_attributes = {}  # 使用新名称
        if isinstance(existing, Item):
            existing.location = None
            existing.quantity = 1
        elif isinstance(existing, Character):
            existing.current_place = None
            items_to_clear = list(existing.has_items)
            for item_id in items_to_clear:
                (item := world.find_entity(item_id)) and setattr(item, 'location', None)
                existing.has_items = []
        elif isinstance(existing, Place):
            contents_to_clear = list(existing.contents)
            for content_id in contents_to_clear:
                content = world.find_entity(content_id)
                if content:
                    if isinstance(content, Item):
                        content.location = None
                    elif isinstance(content, Character):
                        content.current_place = None
            existing.contents = []
            existing.exits = {}
        return existing
    else:
        new_entity = model_class(entity_id=entity_id, entity_type=entity_type)
        new_entity.name = placeholder_name
        world.add_entity(new_entity)
        return new_entity  # 设置 name property


def _ensure_entity_exists(world: WorldState, entity_spec: Union[str, Tuple[str, str], None],
                          expected_entity_type: Optional[Union[Type[BaseEntity], Tuple[Type[BaseEntity], ...]]] = None, context: str = "未知操作") -> Optional[
    str]:
    if entity_spec is None: return None
    entity_id: str = ""
    provided_type_str: Optional[Literal["Item", "Character", "Place"]] = None
    if isinstance(entity_spec, tuple) and len(entity_spec) == 2:
        raw_type, entity_id = entity_spec
        if isinstance(raw_type, str) and raw_type.capitalize() in ["Item", "Character", "Place"]:
            provided_type_str = cast(Literal["Item", "Character", "Place"], raw_type.capitalize())
        else:
            raise ValueError(f"{context}: 无效的实体类型 '{raw_type}' 在规范 '{entity_spec}' 中。")
    elif isinstance(entity_spec, str):
        entity_id = entity_spec
    else:
        raise TypeError(f"{context}: 无效的实体规范格式: {repr(entity_spec)}")
    if not entity_id: raise ValueError(f"{context}: 无法从规范 '{repr(entity_spec)}' 提取有效 entity_id")
    entity = world.find_entity(entity_id, include_destroyed=False)
    if entity:
        if expected_entity_type and not isinstance(entity, expected_entity_type): logging.warning(
            f"{context}: 找到实体 '{entity_id}' 类型 ({entity.entity_type}) 与期望不符。继续使用。")
        if provided_type_str and entity.entity_type != provided_type_str: logging.warning(
            f"{context}: AI 提供的类型 '{provided_type_str}' 与找到的实体 '{entity_id}' ({entity.entity_type}) 不符。使用找到的实体。")
        logging.debug(f"{context}: 确认实体 '{entity_id}' ({entity.entity_type}) 存在。")
        return entity_id
    else:
        if provided_type_str:
            logging.info(f"{context}: 目标实体 '{entity_id}' 不存在/已销毁，尝试创建/恢复 '{provided_type_str}' 占位符。")
            placeholder = _create_placeholder_entity(world, provided_type_str, entity_id, context)
            if expected_entity_type and not isinstance(placeholder, expected_entity_type): logging.warning(
                f"{context}: 创建/恢复的占位符 '{entity_id}' 类型与期望不符。")
            return placeholder.entity_id
        else:
            logging.error(f"{context}: 目标实体 '{entity_id}' 不存在/已销毁且无类型，无法自动创建/恢复。")
            raise ValueError(f"目标实体 '{entity_id}' 不存在/已销毁且无法自动创建/恢复")


def _remove_from_container(world: WorldState, entity_id: str, container_id: Optional[str]):
    if not container_id:
        return
    container = world.find_entity(container_id, include_destroyed=False)
    if not container:
        logging.warning(f"尝试从无效容器 '{container_id}' 移除 '{entity_id}'，忽略。")
        return
    try:
        if isinstance(container, Character):
            if entity_id in container.has_items:
                container.has_items.remove(entity_id)
                logging.debug(f"从角色 '{container_id}' 移除物品 '{entity_id}'")
            else:
                logging.warning(f"尝试从角色 '{container_id}' 移除不存在物品 '{entity_id}'")
        elif isinstance(container, Place):
            if entity_id in container.contents:
                container.contents.remove(entity_id)
                logging.debug(f"从地点 '{container_id}' 移除实体 '{entity_id}'")
            else:
                logging.warning(f"尝试从地点 '{container_id}' 移除不存在实体 '{entity_id}'")
    except ValueError:
        logging.warning(f"移除实体 '{entity_id}' 时发生 ValueError (可能已不在容器 '{container_id}')。")


def _add_to_container(world: WorldState, entity_id: str, container_id: Optional[str]):
    if not container_id:
        return
    container = world.find_entity(container_id, include_destroyed=False)
    if not container: raise ValueError(f"添加失败: 目标容器 '{container_id}' 无效。")
    entity_to_add = world.find_entity(entity_id, include_destroyed=False)
    if not entity_to_add: raise ValueError(f"无法添加无效实体 '{entity_id}' 到容器 '{container_id}'。")
    if isinstance(container, Character):
        if not isinstance(entity_to_add, Item): raise TypeError(f"不能将非物品 '{entity_id}' ({entity_to_add.entity_type}) 添加到角色 '{container_id}'。")
        if entity_id not in container.has_items:
            container.has_items.append(entity_id)
            logging.debug(f"物品 '{entity_id}' 添加到角色 '{container_id}'")
        else:
            logging.debug(f"物品 '{entity_id}' 已在角色 '{container_id}' 中。")
    elif isinstance(container, Place):
        if entity_id not in container.contents:
            container.contents.append(entity_id)
            logging.debug(f"实体 '{entity_id}' ({entity_to_add.entity_type}) 添加到地点 '{container_id}'")
        else:
            logging.debug(f"实体 '{entity_id}' 已在地点 '{container_id}' 中。")
    else:
        raise TypeError(f"目标实体 '{container_id}' (Item) 不能作为容器。")


def _update_container_lists(world: WorldState, entity_id: str, old_container_id: Optional[str], new_container_id: Optional[str]):
    if old_container_id == new_container_id:
        return
    logging.debug(f"更新容器列表: 实体='{entity_id}', 旧='{old_container_id}', 新='{new_container_id}'")
    _remove_from_container(world, entity_id, old_container_id)
    _add_to_container(world, entity_id, new_container_id)


# --- 指令接口和实现 ---
class ICommand(ABC):
    def __init__(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str, params: Dict[str, Any]):
        self.entity_type = entity_type
        self.entity_id = entity_id
        # params 现在总是 Dict[str, Tuple[str, Any]]
        self.params = cast(Dict[str, Tuple[str, Any]], params)
        logging.debug(f"初始化指令: {self.__class__.__name__} (Type={entity_type}, ID={entity_id}, Params={params})")

    @abstractmethod
    def execute(self, world: WorldState) -> None: pass


class CreateCommand(ICommand):
    """处理 @Create 指令，name 不再特殊处理"""

    def __init__(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str, params: Dict[str, Any]):
        super().__init__(entity_type, entity_id, params)
        # 确保 Create 命令至少提供了 name 参数
        if 'name' not in self.params or self.params['name'][0] != '=':
            raise ValueError(f"Create 命令 ({self.entity_id}) 缺少必需的 'name' 赋值参数。Params: {params}")

    def execute(self, world: WorldState) -> None:
        logging.info(f"执行 Create: Type={self.entity_type}, ID={self.entity_id}")
        context = f"Create {self.entity_type} '{self.entity_id}'"

        # --- 堆叠检查 (仅 Item) ---
        item_quantity_spec = self.params.get("quantity")
        if self.entity_type == "Item" and item_quantity_spec:
            op_q, quantity_val = item_quantity_spec
            if op_q == '=' and isinstance(quantity_val, int) and quantity_val > 0:
                location_spec = self.params.get("location")
                name_spec = self.params.get("name")  # name 必有
                item_name = str(name_spec[1])
                if location_spec and location_spec[0] == '=':
                    loc_val = location_spec[1]
                    try:
                        container_id = _ensure_entity_exists(world, loc_val, (Character, Place), f"{context} 查找容器")
                        if container_id and (container := world.find_entity(container_id)):
                            c_list_attr = "has_items" if isinstance(container, Character) else ("contents" if isinstance(container, Place) else None)
                            if c_list_attr:
                                c_list = getattr(container, c_list_attr, [])
                                for existing_item_id in c_list:
                                    item = world.find_entity(existing_item_id)
                                    if item and isinstance(item, Item) and item.name == item_name and not item.is_destroyed:
                                        logging.info(f"物品堆叠: 在 '{container_id}' 的 '{item_name}' ({existing_item_id}) 增加数量 {quantity_val}")
                                        item.set_attribute('quantity', item.quantity + quantity_val)
                                        return  # 堆叠成功
                    except Exception as e:
                        logging.error(f"{context} 堆叠检查失败: {e}", exc_info=True)
                        raise e

        # --- 创建或恢复/重置实体 ---
        existing_entity = world.find_entity(self.entity_id, include_destroyed=True)
        model_class: Type[BaseEntity]
        core_init_params: Dict[str, Any] = {"entity_id": self.entity_id, "entity_type": self.entity_type}
        core_fields_in_params: Set[str] = set()  # 记录哪些核心字段从 params 设置

        if self.entity_type == "Item":
            model_class = Item
            if (spec := self.params.get("quantity")) and spec[0] == '=' and isinstance(spec[1], int):
                core_init_params['quantity'] = max(0, spec[1])
                core_fields_in_params.add("quantity")
            if (spec := self.params.get("location")) and spec[0] == '=': core_fields_in_params.add("location")  # 暂存标记
        elif self.entity_type == "Character":
            model_class = Character
            if (spec := self.params.get("current_place")) and spec[0] == '=':
                core_fields_in_params.add("current_place")
            if (spec := self.params.get("has_items")) and spec[0] == '=' and isinstance(spec[1], list):
                core_init_params['has_items'] = spec[1]
            core_fields_in_params.add("has_items")  # 可以直接设置列表
        elif self.entity_type == "Place":
            model_class = Place
            if (spec := self.params.get("contents")) and spec[0] == '=' and isinstance(spec[1], list):
                core_init_params['contents'] = spec[1]
                core_fields_in_params.add("contents")
            if (spec := self.params.get("exits")) and spec[0] == '=' and isinstance(spec[1], dict):
                core_init_params['exits'] = spec[1]
                core_fields_in_params.add("exits")
        else:
            raise RuntimeError(f"内部错误：无效的 entity_type {self.entity_type}")

        if existing_entity:
            if not existing_entity.is_destroyed:
                logging.warning(f"{context}: 实体 ID 已存在且未销毁，将被覆盖。")
            else:
                logging.info(f"{context}: 恢复并重置已销毁的实体 ID。")
            new_entity = _create_placeholder_entity(world, self.entity_type, self.entity_id, context)
            for key, value in core_init_params.items():  # 重新应用核心字段
                if key not in ['entity_id', 'entity_type']:
                    try:
                        new_entity.set_attribute(key, value)
                    except Exception as e:
                        logging.error(f"{context} 重置核心字段 '{key}' 失败: {e}")
        else:
            new_entity = model_class(**core_init_params)
            world.add_entity(new_entity)

        # --- 使用 modify_attribute 设置所有参数 (包括 name 和其他动态属性) ---
        location_key_to_process: Optional[str] = None
        location_spec_to_process: Any = None

        for key, op_value_tuple in self.params.items():
            # 跳过已在 core_init_params 中处理或标记的核心结构字段
            if key in core_fields_in_params: continue

            # 检查是否是位置字段
            is_location = (key == 'location' and isinstance(new_entity, Item)) or (key == 'current_place' and isinstance(new_entity, Character))

            try:
                op, value = op_value_tuple
                if op != '=': logging.warning(f"{context}: 属性 '{key}' 在 Create 中使用了非 '=' 操作符 '{op}'，将尝试赋值。")
                if is_location:  # 暂存位置信息
                    location_key_to_process = key
                    location_spec_to_process = ('=', value)  # 确保是赋值
                else:  # 设置其他属性 (包括 name)
                    new_entity.modify_attribute(key, ('=', value))  # 强制赋值
            except Exception as e:
                logging.error(f"{context}: 设置初始属性 '{key}' (值: {repr(op_value_tuple[1])}) 失败: {e}", exc_info=True)
                raise e

        # --- 处理位置 ---
        if location_key_to_process and location_spec_to_process:
            op, loc_val = location_spec_to_process  # op 必定是 '='
            target_container_id: Optional[str] = None
            expected_type: Optional[Union[Type, Tuple[Type, ...]]] = None
            if isinstance(new_entity, Item):
                expected_type = (Character, Place)
            elif isinstance(new_entity, Character):
                expected_type = Place
            try:
                target_container_id = _ensure_entity_exists(world, loc_val, expected_type, f"{context} 查找位置")
                if target_container_id:
                    new_entity.set_attribute(location_key_to_process, target_container_id)
                    _add_to_container(world, self.entity_id, target_container_id)
                    logging.info(f"实体 '{self.entity_id}' 已放置在 '{target_container_id}'")
            except Exception as e:
                logging.error(f"{context}: 处理位置 '{loc_val}' 失败: {e}", exc_info=True)
                raise e


# --- ModifyCommand ---
class ModifyCommand(ICommand):
    """处理 @Modify 指令"""

    def __init__(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str, params: Dict[str, Any]):
        super().__init__(entity_type, entity_id, params)
        self.updates = params  # params 就是更新内容 Dict[str, Tuple[str, Any]]

    def execute(self, world: WorldState) -> None:
        logging.info(f"执行 Modify: Type={self.entity_type}, ID={self.entity_id}, Updates={self.updates}")
        entity = world.find_entity(self.entity_id, include_destroyed=False)
        if not entity: raise ValueError(f"Modify 失败: 找不到实体 '{self.entity_id}' 或已销毁。")
        if entity.entity_type != self.entity_type: raise TypeError(f"Modify 失败: 实体类型不匹配 ({entity.entity_type} vs {self.entity_type})")

        context = f"Modify {self.entity_type} '{self.entity_id}'"
        old_location_or_place: Optional[str] = None
        location_key_being_modified: Optional[str] = None
        location_op_value: Optional[Tuple[str, Any]] = None  # 存储位置的 (op, value)

        if isinstance(entity, Item):
            old_location_or_place = entity.location
        elif isinstance(entity, Character):
            old_location_or_place = entity.current_place

        # 应用属性更新
        for key, op_value_tuple in self.updates.items():
            is_location_key = (key == 'location' and isinstance(entity, Item)) or (key == 'current_place' and isinstance(entity, Character))

            if is_location_key:
                location_key_being_modified = key
                location_op_value = op_value_tuple
                logging.debug(f"计划修改位置属性 '{key}' (指令值: {repr(op_value_tuple)})。")
                continue  # 位置最后处理

            try:  # 处理非位置属性
                op, value_to_use = op_value_tuple
                # 确保 quantity 非负
                if key == 'quantity' and isinstance(entity, Item) and isinstance(value_to_use, int):
                    current_qty = entity.quantity
                    tentative_qty = value_to_use
                    if op == '+=' or op == '+':
                        tentative_qty = current_qty + value_to_use
                    elif op == '-=' or op == '-':
                        tentative_qty = current_qty - value_to_use
                    value_to_use = max(0, tentative_qty)
                    op_value_tuple = (op, value_to_use)  # 更新 tuple 中的值

                entity.modify_attribute(key, op_value_tuple)
                logging.debug(f"Modify: 调用 entity.modify_attribute(key='{key}', update='{repr(op_value_tuple)}')")
            except Exception as e:
                logging.error(f"{context}: 修改属性 '{key}' (更新: {repr(op_value_tuple)}) 失败: {e}", exc_info=True)
                raise e

        # --- 统一处理位置变更 ---
        if location_key_being_modified and location_op_value:
            op, spec_to_process = location_op_value
            final_new_location_id: Optional[str] = None
            if op == '-':
                final_new_location_id = None
                logging.debug(f"位置操作: 移除位置 (op='-')")
            elif op != '=':
                raise ValueError(f"{context}: 位置属性 '{location_key_being_modified}' 不支持操作符 '{op}' (只支持 = 或 -)")
            elif spec_to_process is not None:  # 赋值操作
                expected_type: Optional[Union[Type, Tuple[Type, ...]]] = None
                if isinstance(entity, Item):
                    expected_type = (Character, Place)
                elif isinstance(entity, Character):
                    expected_type = Place
                try:
                    final_new_location_id = _ensure_entity_exists(world, spec_to_process, expected_type, f"{context} 查找新位置")
                    logging.debug(f"位置操作: 设置新位置为 '{final_new_location_id}'")
                except Exception as e:
                    logging.error(f"{context}: 确定或创建新位置 '{spec_to_process}' 失败: {e}", exc_info=True)
                    raise e
            else:
                final_new_location_id = None
                logging.debug(f"位置操作: 设置位置为 None")

            if old_location_or_place != final_new_location_id:
                logging.info(f"位置变更检测: '{self.entity_id}' 从 '{old_location_or_place}' 移动到 '{final_new_location_id}'")
                try:
                    entity.set_attribute(location_key_being_modified, final_new_location_id)
                    _update_container_lists(world, self.entity_id, old_location_or_place, final_new_location_id)
                    logging.info(f"实体 '{self.entity_id}' 位置更新成功。")
                except Exception as e:
                    logging.error(f"应用位置变更失败: {e}", exc_info=True)
                    raise e
            else:
                logging.debug(f"实体 '{self.entity_id}' 位置未改变 (仍是 '{final_new_location_id}')。")


# --- DestroyCommand ---
class DestroyCommand(ICommand):
    """处理 @Destroy 指令"""

    def __init__(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str, params: Dict[str, Any]):
        super().__init__(entity_type, entity_id, params)

    def execute(self, world: WorldState) -> None:
        logging.info(f"执行 Destroy: Type={self.entity_type}, ID={self.entity_id}")
        entity = world.find_entity(self.entity_id, include_destroyed=True)
        if not entity:
            logging.warning(f"Destroy 指令警告: 找不到实体 ID '{self.entity_id}'，跳过。")
            return
        if entity.is_destroyed:
            logging.info(f"实体 '{self.entity_id}' 已被销毁，无需操作。")
            return
        if entity.entity_type != self.entity_type: raise TypeError(f"Destroy 指令错误: 实体类型不匹配 ({entity.entity_type} vs {self.entity_type})")
        logging.info(f"销毁实体: {entity.entity_type} ID='{entity.entity_id}', Name='{entity.name}'")
        entity.is_destroyed = True
        container_id: Optional[str] = None
        items_to_clear: List[str] = []
        contents_to_clear: List[str] = []
        if isinstance(entity, Item):
            container_id = entity.location
            entity.location = None
        elif isinstance(entity, Character):
            container_id = entity.current_place
            entity.current_place = None
            items_to_clear = list(entity.has_items)
            entity.has_items = []
        elif isinstance(entity, Place):
            contents_to_clear = list(entity.contents)
            entity.contents = []
            entity.exits = {}
        if container_id and isinstance(entity, (Item, Character)): _remove_from_container(world, self.entity_id, container_id)
        if items_to_clear:
            logging.debug(f"角色 '{self.entity_id}' 销毁，清除其 {len(items_to_clear)} 个物品的位置...")
            for item_id in items_to_clear: (item := world.find_entity(item_id)) and isinstance(item, Item) and not item.is_destroyed and setattr(item,
                'location', None) and logging.debug(f"物品 '{item_id}' ({item.name}) 位置清除。")
        if contents_to_clear:
            logging.debug(f"地点 '{self.entity_id}' 销毁，清除其 {len(contents_to_clear)} 个内容物的位置...")
            for content_id in contents_to_clear:
                content = world.find_entity(content_id)
                if content and not content.is_destroyed:
                    if isinstance(content, Item):
                        content.location = None
                        logging.debug(f"内容物(Item) '{content_id}' ({content.name}) 位置清除。")
                    elif isinstance(content, Character):
                        content.current_place = None
                        logging.debug(f"内容物(Char) '{content_id}' ({content.name}) 位置清除。")


# --- TransferCommand ---
class TransferCommand(ICommand):
    """处理 @Transfer 指令 (只能用于 Item 或 Character)"""

    def __init__(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str, params: Dict[str, Any]):
        if entity_type not in ["Item", "Character"]: raise TypeError(f"Transfer 指令只适用于 Item 或 Character，而非 {entity_type}")
        super().__init__(cast(Literal["Item", "Character"], entity_type), entity_id, params)
        target_spec_tuple = self.params.get('target')
        if not target_spec_tuple or target_spec_tuple[0] != '=': raise ValueError(
            f"Transfer 命令 ({self.entity_id}) 缺少必需的 'target' 赋值参数。Params: {params}")
        self.target_spec = target_spec_tuple[1]  # 获取 target 的值
        if self.target_spec is None: raise ValueError("Transfer 指令的目标不能为空。")

    def execute(self, world: WorldState) -> None:
        logging.info(f"执行 Transfer: Type={self.entity_type}, ID={self.entity_id}, Target='{self.target_spec}'")
        entity = world.find_entity(self.entity_id, include_destroyed=False)
        if not entity: raise ValueError(f"Transfer 失败: 找不到要转移的实体 '{self.entity_id}' 或已销毁。")
        context = f"Transfer {self.entity_type} '{self.entity_id}'"
        expected_target_type: Optional[Union[Type, Tuple[Type, ...]]] = None
        current_location_attr: Optional[str] = None
        if isinstance(entity, Item):
            expected_target_type = (Character, Place)
            current_location_attr = 'location'
        elif isinstance(entity, Character):
            expected_target_type = Place
            current_location_attr = 'current_place'
        try:
            final_target_id = _ensure_entity_exists(world, self.target_spec, expected_target_type, context)
        except Exception as e:
            logging.error(f"{context}: 确定或创建目标 '{self.target_spec}' 失败: {e}", exc_info=True)
            raise e
        old_container_id: Optional[str] = getattr(entity, current_location_attr, None) if current_location_attr else None
        if old_container_id == final_target_id:
            logging.info(f"Transfer: 实体 '{self.entity_id}' 已在目标 '{final_target_id}'，无需转移。")
            return
        logging.info(f"开始转移实体 '{self.entity_id}' 从 '{old_container_id}' 到 '{final_target_id}'")
        try:
            if current_location_attr: entity.set_attribute(current_location_attr, final_target_id)
            _update_container_lists(world, self.entity_id, old_container_id, final_target_id)
            logging.info(f"实体 '{self.entity_id}' 成功转移到 '{final_target_id}'")
        except Exception as e:
            logging.error(f"执行 Transfer 失败: {e}", exc_info=True)
            raise e


# --- CommandExecutor (保持不变) ---
class CommandExecutor:
    COMMAND_MAP: Dict[str, Type[ICommand]] = {"create": CreateCommand, "modify": ModifyCommand, "destroy": DestroyCommand, "transfer": TransferCommand}

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
            if not command_class:
                logging.warning(f"跳过未知指令类型: '{command_name}'")
                continue
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
