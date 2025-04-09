# core/world_state.py
import logging
import re
from typing import List, Optional, Dict, Any, Literal, Union, Set, Tuple, ClassVar, TYPE_CHECKING, cast, Type

# 从新的类型模块导入
from core.types import TypedID, EntityType

from pydantic import BaseModel, Field, ValidationError, PrivateAttr

# 移除旧的 ENTITY_REF_REGEX 导入和定义，因为现在主要依赖 TypedID
# try:
#     from processing.parser import ENTITY_REF_REGEX
# except ImportError:
#     logging.warning("无法从 processing.parser 导入 ENTITY_REF_REGEX，使用本地定义。")
#     ENTITY_REF_REGEX = re.compile(r"^(Item|Character|Place):([\w\-]+)$", re.IGNORECASE)

if TYPE_CHECKING:
    # 防止循环导入，只在类型检查时导入 WorldState
    pass

# --- 前向声明实体类型 ---
AnyEntity = Union['Item', 'Character', 'Place']


# --- WorldState 定义 ---
class WorldState(BaseModel):
    items: Dict[str, 'Item'] = Field(default_factory=dict)
    characters: Dict[str, 'Character'] = Field(default_factory=dict)
    places: Dict[str, 'Place'] = Field(default_factory=dict)
    model_config = {'validate_assignment': True}

    def get_entity_dict(self, entity_type: EntityType) -> Dict[str, AnyEntity]:
        """根据实体类型获取对应的存储字典 (Helper)"""
        if entity_type == "Item":
            return self.items  # type: ignore
        elif entity_type == "Character":
            return self.characters  # type: ignore
        elif entity_type == "Place":
            return self.places  # type: ignore
        else:
            # 应由类型提示覆盖，但作为保险
            raise ValueError(f"未知的实体类型: {entity_type}")

    # --- find_entity (保持不变，接口清晰) ---
    def find_entity(self, entity_id: str, entity_type: EntityType, include_destroyed: bool = False) -> Optional[AnyEntity]:
        """通过 ID 和 类型 精确查找实体。"""
        entity_dict = self.get_entity_dict(entity_type)
        entity = entity_dict.get(entity_id)
        if entity and (not entity.is_destroyed or include_destroyed):
            return entity
        return None

    # --- find_entity_by_typed_id (新方法，替代 find_entity_by_ref) ---
    def find_entity_by_typed_id(self, ref: Optional[TypedID], include_destroyed: bool = False) -> Optional[AnyEntity]:
        """通过 TypedID 引用查找实体"""
        if ref is None:
            return None
        return self.find_entity(ref.id, ref.type, include_destroyed)

    def find_entity_by_name(self, name: str, entity_type: Optional[EntityType] = None,
                            include_destroyed: bool = False) -> Optional[AnyEntity]:
        """按名称查找实体（效率较低，保持不变）"""
        search_dicts: List[Dict[str, AnyEntity]] = []
        if entity_type:
            search_dicts.append(self.get_entity_dict(entity_type))
        else:
            search_dicts.extend([self.items, self.characters, self.places])  # type: ignore

        for entity_dict in search_dicts:
            for entity in entity_dict.values():
                entity_name = entity.get_attribute('name')
                if entity_name == name and (not entity.is_destroyed or include_destroyed):
                    return entity
        return None

    # --- add_entity (保持不变，内部查找逻辑已更新) ---
    def add_entity(self, entity: AnyEntity):
        """添加实体到世界状态，仅在同类型中检查 ID 冲突。"""
        entity_dict = self.get_entity_dict(entity.entity_type)
        if entity.entity_id in entity_dict:
            existing_entity = entity_dict[entity.entity_id]
            if not existing_entity.is_destroyed:
                logging.warning(f"覆盖已存在且未销毁的实体: {entity.typed_id}") # 使用 typed_id
            # 如果已销毁，直接覆盖是正常操作
        elif self.find_entity_globally(entity.entity_id):
            logging.debug(f"注意: 添加实体 {entity.typed_id} 时，发现其他类型存在同ID实体。") # 使用 typed_id

        entity_dict[entity.entity_id] = entity
        logging.debug(f"实体 '{entity.typed_id}' 已添加到 WorldState。") # 使用 typed_id

    def find_entity_globally(self, entity_id: str, include_destroyed: bool = False) -> Optional[AnyEntity]:
        """(辅助函数) 跨所有类型查找第一个匹配 ID 的实体 (用于调试或旧逻辑兼容)"""
        # 注意：这个函数仍然使用裸 ID，因为它不知道类型。
        # 如果全局 ID 冲突，它会返回找到的第一个。
        for entity_type in cast(Tuple[EntityType, ...], ("Item", "Character", "Place")):
            entity = self.find_entity(entity_id, entity_type, include_destroyed)
            if entity: return entity
        return None


# --- 内部辅助函数: 创建占位符 (保持不变，查找逻辑已更新) ---
def _create_placeholder_entity(entity_type: EntityType, entity_id: str, world: WorldState, context: str = "未知操作"):
    """内部函数：创建指定类型的占位符实体并添加到 world。"""
    if world.find_entity(entity_id, entity_type, include_destroyed=True):
        logging.debug(f"[{context}] 尝试创建占位符 '{entity_type}:{entity_id}'，但同类型实体已存在（可能已销毁）。跳过创建。")
        return

    model_class: Type[BaseEntity]
    if entity_type == "Item":
        model_class = Item
    elif entity_type == "Character":
        model_class = Character
    elif entity_type == "Place":
        model_class = Place
    else:
        # 这不应该发生，因为 EntityType 是 Literal
        raise ValueError(f"[{context}] 无效的实体类型 '{entity_type}'")

    new_entity = model_class(entity_id=entity_id, entity_type=entity_type)
    warning_prefix = "Warning: Missing"
    placeholder_name = f"{warning_prefix} {entity_type} [{entity_id}] (Auto-created by: {context})"
    new_entity._dynamic_attributes['name'] = placeholder_name
    logging.info(f"[{context}] 创建了占位符实体 '{entity_type}:{entity_id}'，名称: '{placeholder_name}'")
    world.add_entity(new_entity)


# --- 内部辅助函数: 递归确保引用存在 (修改：处理 TypedID) ---
def _ensure_references_exist(value: Any, world: WorldState, context: str = "未知属性"):
    """
    递归检查值中的 TypedID 引用，如果实体不存在则创建占位符。
    直接修改传入的可变对象（列表/字典）。
    """
    if isinstance(value, TypedID):
        # 使用新的 find_entity 检查
        if not world.find_entity(value.id, value.type, include_destroyed=False):
            logging.warning(f"[{context}] 引用实体 '{value}' 不存在，将创建占位符。")
            try:
                # 创建逻辑不变，内部会再次用 find_entity 检查
                _create_placeholder_entity(value.type, value.id, world, context=f"引用自 {context}")
            except Exception as e:
                logging.error(f"[{context}] 创建占位符 '{value}' 失败: {e}", exc_info=True)
                # raise RuntimeError(f"无法为引用 '{value}' 创建占位符") from e
    elif isinstance(value, list):
        # 对列表中的每个元素递归调用
        for i, item in enumerate(value):
            _ensure_references_exist(item, world, context=f"{context}[{i}]")
    elif isinstance(value, dict):
        # 对字典的每个值递归调用 (键通常不是引用)
        for k, v in value.items():
            _ensure_references_exist(v, world, context=f"{context}['{k}']")
    # 其他类型 (str, int, bool, None, etc.) 不包含引用，直接忽略


# --- 核心数据模型 ---
class BaseEntity(BaseModel):
    entity_id: str = Field(...)
    entity_type: EntityType = Field(...)
    is_destroyed: bool = Field(False)
    _dynamic_attributes: Dict[str, Any] = PrivateAttr(default_factory=dict)
    model_config = {"validate_assignment": True, "extra": "forbid"}
    _CORE_FIELDS: ClassVar[Set[str]] = {'entity_id', 'entity_type', 'is_destroyed'}

    @property
    def typed_id(self) -> TypedID:
        """返回实体的 TypedID 表示。"""
        return TypedID(type=self.entity_type, id=self.entity_id)

    def get_attribute(self, key: str, default: Any = None) -> Any:
        if key in self._CORE_FIELDS:
            return getattr(self, key)
        else:
            return self._dynamic_attributes.get(key, default)

    def has_attribute(self, key: str) -> bool:
        return key in self._CORE_FIELDS or key in self._dynamic_attributes

    def delete_attribute(self, key: str) -> bool:
        if key in self._CORE_FIELDS:
            return False
        elif key in self._dynamic_attributes:
            del self._dynamic_attributes[key]
            return True
        else:
            return False

    def get_all_attributes(self, exclude_internal: Set[str] = {'_dynamic_attributes', 'model_config'}) -> Dict[str, Any]:
        all_attrs = {}
        for key in self._CORE_FIELDS:
            if key not in exclude_internal:
                value = getattr(self, key)
                # Pydantic 应该能处理 TypedID 的序列化，这里直接赋值
                all_attrs[key] = value
        # 动态属性也可能包含 TypedID
        for key, value in self._dynamic_attributes.items():
             # Pydantic 应该能处理 TypedID 的序列化，这里直接赋值
             # 如果需要手动处理副本或特殊序列化，可以在这里进行
            if isinstance(value, list):
                 all_attrs[key] = list(value) # 仍然创建列表副本
            elif isinstance(value, dict):
                 all_attrs[key] = dict(value) # 仍然创建字典副本
            else:
                all_attrs[key] = value

        for field in exclude_internal: all_attrs.pop(field, None)
        return all_attrs

    # --- set_attribute (修改：处理 TypedID 检查逻辑) ---
    def set_attribute(self, key: str, value: Any, world: Optional[WorldState] = None):
        """
        设置属性值。在设置前，会递归检查 value 中的 TypedID 引用并创建占位符。
        需要传入 world 实例。
        """
        processed_value = value  # 默认值
        if world:
            try:
                # 创建副本以避免修改原始输入值，特别是对于列表/字典
                value_copy = None
                if isinstance(value, list):
                    value_copy = list(value)
                elif isinstance(value, dict):
                    value_copy = dict(value)
                else:
                    # 对于 TypedID 和其他不可变类型，浅拷贝即可
                    value_copy = value

                # _ensure_references_exist 现在处理 TypedID
                _ensure_references_exist(value_copy, world, context=f"{self.typed_id}.{key}")
                processed_value = value_copy
            except Exception as e:
                logging.error(f"实体 '{self.typed_id}': 确保属性 '{key}' 的引用时失败: {e}", exc_info=True)
                raise RuntimeError(f"确保属性 '{key}' 引用失败") from e
        elif isinstance(value, (TypedID, list, dict)): # 检查类型是否可能包含引用
            # 简单的潜在引用检查 (不递归)
            has_potential_ref = False
            if isinstance(value, TypedID):
                has_potential_ref = True
            elif isinstance(value, list) and any(isinstance(item, TypedID) for item in value):
                 has_potential_ref = True
            elif isinstance(value, dict) and any(isinstance(v, TypedID) for v in value.values()):
                 has_potential_ref = True

            if has_potential_ref:
                logging.warning(f"实体 '{self.typed_id}': set_attribute('{key}') 缺少 world，且值包含 TypedID 引用，无法确保引用存在。")

        # 设置核心或动态属性的逻辑保持不变
        if key in self._CORE_FIELDS:
            try:
                setattr(self, key, processed_value)
            except ValidationError as e:
                logging.error(f"实体 '{self.typed_id}': 设置核心属性 '{key}' 验证失败: {e}", exc_info=True)
                raise e
            except Exception as e:
                logging.error(f"实体 '{self.typed_id}': 设置核心属性 '{key}' 时发生意外错误: {e}", exc_info=True)
                raise e
            logging.debug(f"实体 '{self.typed_id}': 设置核心属性 {key} = {repr(processed_value)}")
        else:
            self._dynamic_attributes[key] = processed_value
            logging.debug(f"实体 '{self.typed_id}': 设置动态属性 {key} = {repr(processed_value)}")

    # --- modify_attribute (修改：处理 TypedID 检查逻辑) ---
    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any], world: Optional[WorldState] = None):
        """
        修改属性值。基类现在处理数值、字符串、列表和字典的通用操作。
        需要传入 world 实例以确保 TypedID 引用存在。
        """
        op, value = opAndValue
        logging.debug(
            f"实体 '{self.typed_id}': 基类 modify_attribute: Key='{key}', Op='{op}', Value='{repr(value)}'")

        # 核心属性处理 (不变)
        if key in self._CORE_FIELDS:
            if op == '=':
                self.set_attribute(key, value, world)
            else:
                raise ValueError(f"核心属性 '{key}' 不支持操作符 '{op}'")
            return

        # 获取当前值 (不变)
        current_value = self.get_attribute(key)
        was_modified = False

        # 预处理: 确保 value 中的 TypedID 引用存在 (仅对 +=/+ 操作)
        processed_value = value
        if op in ('+=', '+'):
            if world:
                try:
                    value_copy = None
                    if isinstance(value, list):
                        value_copy = list(value)
                    elif isinstance(value, dict):
                        value_copy = dict(value)
                    else:
                        value_copy = value
                    # _ensure_references_exist 现在处理 TypedID
                    _ensure_references_exist(value_copy, world, context=f"{self.typed_id}.{key} (op {op})")
                    processed_value = value_copy
                except Exception as e:
                    logging.error(f"实体 '{self.typed_id}': 确保属性 '{key}' (op {op}) 的操作值引用时失败: {e}", exc_info=True)
                    raise RuntimeError(f"确保属性 '{key}' (op {op}) 操作值引用失败") from e
            elif isinstance(value, (TypedID, list, dict)):
                # 简单的潜在引用检查
                has_potential_ref = False
                if isinstance(value, TypedID):
                    has_potential_ref = True
                elif isinstance(value, list) and any(isinstance(item, TypedID) for item in value):
                    has_potential_ref = True
                elif isinstance(value, dict) and any(isinstance(v, TypedID) for v in value.values()):
                    has_potential_ref = True
                if has_potential_ref:
                    logging.warning(f"实体 '{self.typed_id}': modify_attribute('{key}', '{op}') 缺少 world，且值包含 TypedID 引用，无法确保操作值中的引用存在。")

        # --- 处理各种操作 (逻辑基本不变，但列表/字典比较需要注意 TypedID 的 __eq__) ---
        new_value_to_set = None

        # 赋值 (=)
        if op == '=':
            self.set_attribute(key, processed_value, world)
            return

        # 加/合并 (+= / +)
        elif op in ('+=', '+'):
            if current_value is None:
                logging.debug(f"Modify '{key}' {op} {repr(processed_value)}: 当前值为 None，视为首次设置。")
                # --- 修正：根据 processed_value 类型决定行为 ---
                if isinstance(processed_value, list):
                    new_value_to_set = processed_value  # 初始值是列表
                elif isinstance(processed_value, str):
                    new_value_to_set = processed_value  # 初始值是字符串
                # elif isinstance(processed_value, (int, float)): # 数值 += None 没有意义，但可以设为初始值
                #    new_value_to_set = processed_value
                # elif isinstance(processed_value, dict): # 字典 += None 设为初始字典
                #     new_value_to_set = processed_value
                # elif isinstance(processed_value, TypedID): # TypedID += None 没有意义，设为初始值？(可能需要列表)
                #     new_value_to_set = [processed_value] # 更安全的做法是假设引用通常在列表中
                else:
                    # 对于 TypedID 或其他类型，更安全的默认行为是放入列表
                    new_value_to_set = [processed_value]
                # ---
                was_modified = True
            elif isinstance(current_value, (int, float)) and isinstance(processed_value, (int, float)):
                new_value_to_set = current_value + processed_value
                was_modified = True
            elif isinstance(current_value, str) and isinstance(processed_value, str):
                new_value_to_set = current_value + processed_value
                was_modified = True
            elif isinstance(current_value, list):
                current_list_copy = list(current_value)
                items_to_add = processed_value if isinstance(processed_value, list) else [processed_value]
                for item in items_to_add:
                    # 使用 TypedID 的 __eq__ 进行比较
                    if item not in current_list_copy:
                        current_list_copy.append(item)
                        was_modified = True
                if was_modified: new_value_to_set = current_list_copy
            elif isinstance(current_value, dict):
                if isinstance(processed_value, dict):
                    current_dict_copy = dict(current_value)
                    original_len = len(current_dict_copy)
                    current_dict_copy.update(processed_value)
                    # 字典比较也可能涉及 TypedID
                    if len(current_dict_copy) != original_len or any(current_value.get(k) != v for k, v in processed_value.items()):
                        was_modified = True
                    if was_modified: new_value_to_set = current_dict_copy
                else:
                    raise TypeError(f"字典属性 '{key}' 的 '{op}' 操作需要字典类型的值，得到 {type(processed_value)}")
            else:
                raise TypeError(f"类型 {type(current_value)} 不支持 '{op}' 操作 for key '{key}'")

        # 减/移除 (-= / -)
        elif op in ('-=', '-'):
            if current_value is None:
                logging.warning(f"Modify({self.typed_id}): 尝试对不存在的属性 '{key}' 执行 '{op}' 操作，已忽略。")
                return
            elif isinstance(current_value, (int, float)) and isinstance(value, (int, float)):
                new_value_to_set = current_value - value
                was_modified = True
            elif isinstance(current_value, list):
                current_list_copy = list(current_value)
                # value 可能包含 TypedID 或要比较的字符串等
                items_to_remove_raw = value if isinstance(value, list) else [value]
                for item in items_to_remove_raw:
                    try:
                        # 列表移除依赖元素的 __eq__ 方法，TypedID 已实现
                        current_list_copy.remove(item)
                        was_modified = True
                    except ValueError:
                        pass
                if was_modified: new_value_to_set = current_list_copy
            elif isinstance(current_value, dict):
                current_dict_copy = dict(current_value)
                keys_to_remove = [value] if isinstance(value, str) else value if isinstance(value, list) and all(isinstance(k, str) for k in value) else None
                if keys_to_remove is None:
                    raise TypeError(f"字典属性 '{key}' 的 '{op}' 操作需要字符串键或键列表，得到 {type(value)}")
                for k in keys_to_remove:
                    if k in current_dict_copy:
                        del current_dict_copy[k]
                        was_modified = True
                if was_modified: new_value_to_set = current_dict_copy
            else:
                raise TypeError(f"类型 {type(current_value)} 不支持 '{op}' 操作 for key '{key}'")
        else:
            raise ValueError(f"未知的修改操作符 '{op}' for key '{key}'")

        # 如果发生修改，则通过 set_attribute 写回 (不变)
        if was_modified:
            logging.debug(f"实体 '{self.typed_id}': 属性 '{key}' 已通过 '{op}' 修改，调用 set_attribute 写回。")
            self.set_attribute(key, new_value_to_set, world)
        else:
            logging.debug(f"实体 '{self.typed_id}': 属性 '{key}' 未发生实际修改 (op='{op}')。")

    # --- _update_location_relationship (修改：使用 TypedID) ---
    def _update_location_relationship(self,
                                      old_container_spec: Optional[Union[TypedID, str]],
                                      new_container_spec: Optional[Union[TypedID, str]],
                                      world: WorldState):
        """更新实体移动时的容器关系。现在处理 TypedID。"""

        def get_spec_typed_id(spec: Optional[Union[TypedID, str]]) -> Optional[TypedID]:
            """从输入解析或返回 TypedID"""
            if spec is None: return None
            if isinstance(spec, TypedID): return spec
            if isinstance(spec, str):
                try:
                    # 尝试从字符串解析 (需要 TypedID.from_string 方法)
                    # 假设已在 TypedID 类中添加了 from_string 方法
                    # return TypedID.from_string(spec) # Pydantic模型方法
                    # 或者手动解析：
                    parts = spec.split(':', 1)
                    if len(parts) == 2 and parts[0].capitalize() in ("Item", "Character", "Place"):
                        etype = cast(EntityType, parts[0].capitalize())
                        eid = parts[1]
                        return TypedID(type=etype, id=eid)
                    else:
                        # 裸 ID 不再支持
                        logging.warning(f"_update_location_relationship: 不支持裸 ID '{spec}' 或无效 'Type:ID' 格式，无法确定类型。关系更新可能不完整。")
                        return None
                except ValueError as e:
                    logging.warning(f"无法从字符串解析容器标识符 '{spec}': {e}")
                    return None
            else:
                logging.warning(f"无法解析容器标识符类型: {type(spec)}")
                return None

        old_container_ref: Optional[TypedID] = get_spec_typed_id(old_container_spec)
        new_container_ref: Optional[TypedID] = get_spec_typed_id(new_container_spec)

        if old_container_ref == new_container_ref: return
        logging.debug(f"实体 '{self.typed_id}': 更新容器关系: 从 '{old_container_ref}' 到 '{new_container_ref}'")

        # 从旧容器移除
        if old_container_ref:
            old_container = world.find_entity_by_typed_id(old_container_ref) # 使用 TypedID 查找
            if old_container:
                content_key = 'has_items' if isinstance(old_container, Character) else 'contents' if isinstance(old_container, Place) else None
                if content_key:
                    try:
                        # modify_attribute 使用 TypedID
                        old_container.modify_attribute(content_key, ('-', self.typed_id), world=world)
                    except Exception as e:
                        logging.warning(f"从旧容器 '{old_container_ref}' 移除 '{self.typed_id}' 时出错(忽略): {e}")

        # 添加到新容器
        if new_container_ref:
            # _ensure_references_exist 已处理占位符创建
            new_container = world.find_entity_by_typed_id(new_container_ref) # 使用 TypedID 查找
            if new_container:
                content_key = None
                target_type_ok = False
                if isinstance(new_container, Character) and isinstance(self, Item):
                    content_key = 'has_items'
                    target_type_ok = True
                elif isinstance(new_container, Place) and isinstance(self, (Item, Character)):
                    content_key = 'contents'
                    target_type_ok = True

                if content_key and target_type_ok:
                    try:
                        # modify_attribute 使用 TypedID
                        new_container.modify_attribute(content_key, ('+', self.typed_id), world=world)
                    except Exception as e:
                        raise RuntimeError(f"未能将 '{self.typed_id}' 添加到 '{new_container_ref}': {e}") from e
                elif not target_type_ok:
                    raise TypeError(f"类型不匹配：不能将 {self.entity_type} '{self.typed_id}' 添加到 {new_container.entity_type} '{new_container_ref}'")
            else:
                # 如果 set_attribute 里的 _ensure_references_exist 工作正常，这里不应该发生
                raise RuntimeError(f"内部错误：未能定位新容器 '{new_container_ref}' 即使在检查之后。")


# --- 子类定义 (修改验证函数和类型注解) ---

class Item(BaseEntity):
    entity_type: Literal["Item"] = "Item"

    # --- 验证函数 (修改：使用 TypedID) ---
    def _validate_location_value(self, value: Any) -> Union[None, TypedID]:
        """验证 location 值，必须是 ('Place'|'Character', ID) 的 TypedID 或 None"""
        if value is None: return None
        if isinstance(value, TypedID):
            if value.type not in ["Place", "Character"]:
                raise ValueError(f"无效 location TypedID 类型: {value.type}，需要 Place 或 Character")
            return value
        elif isinstance(value, str):
            try:
                # 尝试从 'Type:ID' 字符串解析
                # parts = value.split(':', 1)
                # if len(parts) == 2 and parts[0].capitalize() in ["Place", "Character"]:
                #     etype = cast(Literal["Place", "Character"], parts[0].capitalize())
                #     eid = parts[1]
                #     logging.warning(f"Item '{self.typed_id}' location: 接收到字符串 '{value}'，已自动转换为 TypedID。建议直接使用 TypedID。")
                #     return TypedID(type=etype, id=eid)
                # else:
                #     raise ValueError(f"无效 location 字符串: '{value}'。必须是 'Type:ID' 格式。")
                # 使用 TypedID.from_string (假设已实现)
                typed_id = TypedID.from_string(value)
                if typed_id.type not in ["Place", "Character"]:
                    raise ValueError(f"无效 location TypedID 类型: {typed_id.type}，需要 Place 或 Character")
                logging.warning(f"Item '{self.typed_id}' location: 接收到字符串 '{value}'，已自动转换为 TypedID。建议直接使用 TypedID。")
                return typed_id
            except ValueError as e:
                raise ValueError(f"无效 location 字符串: '{value}'. {e}") from e
        else:
            raise TypeError(f"无效 location 类型: {type(value)}，需要 TypedID 或 'Type:ID' 字符串。")

    # --- set_attribute 和 modify_attribute (接口不变，内部依赖更新) ---
    def set_attribute(self, key: str, value: Any, world: Optional[WorldState] = None):
        """覆盖：处理 quantity 验证和 location 关系更新。"""
        logging.debug(f"Item '{self.typed_id}': set_attribute: Key='{key}', Value='{repr(value)}'")
        if key == 'quantity':
            if not isinstance(value, int) or value < 0: raise ValueError("Quantity 必须非负整数")
            super().set_attribute(key, value, world)
        elif key == 'location':
            validated_value: Optional[TypedID] = self._validate_location_value(value)
            old_location: Optional[TypedID] = self.get_attribute('location', None)
            super().set_attribute(key, validated_value, world)
            new_location: Optional[TypedID] = self.get_attribute('location', None)
            if old_location != new_location and world is not None:
                try:
                    # 传递 Optional[TypedID] 给关系更新函数
                    self._update_location_relationship(old_location, new_location, world)
                except Exception as e:
                    logging.error(f"Item '{self.typed_id}' 更新 location 关系时失败 (从 {old_location} 到 {new_location}): {e}", exc_info=True)
                    raise e
        else:
            super().set_attribute(key, value, world)

    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any], world: Optional[WorldState] = None):
        """覆盖：处理 quantity 计算，其他委托基类。"""
        op, value = opAndValue
        logging.debug(f"Item '{self.typed_id}': modify_attribute: Key='{key}', Op='{op}', Value='{repr(value)}'")
        if key == 'quantity':
            current_quantity = self.get_attribute('quantity', 0) # 默认0更安全
            new_quantity = None
            if op == '=':
                 if not isinstance(value, int): raise ValueError("Quantity 必须是整数")
                 new_quantity = value
            elif op in ('+=', '+') and isinstance(value, int):
                new_quantity = current_quantity + value
            elif op in ('-=', '-') and isinstance(value, int):
                new_quantity = current_quantity - value
            else:
                raise ValueError(f"Quantity 不支持 '{op}' 或无效类型 ({type(value)})")
            self.set_attribute(key, new_quantity, world)
        elif key == 'location':
            if op == '=':
                self.set_attribute(key, value, world) # 委托给 set_attribute 处理
            else:
                raise ValueError(f"Item location 不支持 '{op}'")
        else:
            logging.debug(f"Item '{self.typed_id}': 委托给基类 for key '{key}'")
            super().modify_attribute(key, opAndValue, world)


class Character(BaseEntity):
    entity_type: Literal["Character"] = "Character"

    # --- 验证函数 (修改：使用 TypedID) ---
    def _validate_current_place_value(self, value: Any) -> Union[None, TypedID]:
        """验证 current_place 值，必须是 ('Place', ID) 的 TypedID 或 None"""
        if value is None: return None
        if isinstance(value, TypedID):
            if value.type != "Place":
                raise ValueError(f"无效 current_place TypedID 类型: {value.type}，需要 Place")
            return value
        elif isinstance(value, str):
            try:
                # 尝试从 'Type:ID' 字符串解析
                typed_id = TypedID.from_string(value) # 假设已实现
                if typed_id.type != "Place":
                    raise ValueError(f"无效 current_place TypedID 类型: {typed_id.type}，需要 Place")
                logging.warning(f"Character '{self.typed_id}' current_place: 接收到字符串 '{value}'，已自动转换为 TypedID。建议直接使用 TypedID。")
                return typed_id
            except ValueError as e:
                raise ValueError(f"无效 current_place 字符串: '{value}'. {e}") from e
        else:
            raise TypeError(f"无效 current_place 类型: {type(value)}，需要 TypedID 或 'Place:ID' 字符串。")

    def _validate_has_items_value(self, value: Any) -> List[TypedID]:
        """验证 has_items 值，必须是包含 ('Item', ID) TypedID 的列表"""
        if not isinstance(value, list): raise TypeError("has_items 必须是列表")
        validated_list: List[TypedID] = []
        for i, item_ref in enumerate(value):
            validated_id: Optional[TypedID] = None
            if isinstance(item_ref, TypedID):
                if item_ref.type != "Item":
                     raise ValueError(f"无效 has_items TypedID 类型在索引 {i}: {item_ref.type}，需要 Item")
                validated_id = item_ref
            elif isinstance(item_ref, str):
                try:
                    typed_id = TypedID.from_string(item_ref) # 假设已实现
                    if typed_id.type != "Item":
                        raise ValueError(f"无效 has_items TypedID 类型在索引 {i}: {typed_id.type}，需要 Item")
                    logging.warning(f"Character '{self.typed_id}' has_items[{i}]: 接收到字符串 '{item_ref}'，已自动转换为 TypedID。建议直接使用 TypedID。")
                    validated_id = typed_id
                except ValueError as e:
                    raise ValueError(f"无效 has_items 字符串在索引 {i}: '{item_ref}'. {e}") from e
            else:
                raise TypeError(f"无效 has_items 元素类型在索引 {i}: {type(item_ref)}，需要 TypedID 或 'Item:ID' 字符串。")

            validated_list.append(validated_id)
        return validated_list

    # --- set_attribute 和 modify_attribute (接口不变，内部依赖更新) ---
    def set_attribute(self, key: str, value: Any, world: Optional[WorldState] = None):
        """覆盖：处理 current_place 关系更新和 has_items 列表类型验证。"""
        logging.debug(f"Character '{self.typed_id}': set_attribute: Key='{key}', Value='{repr(value)}'")
        if key == 'current_place':
            validated_value: Optional[TypedID] = self._validate_current_place_value(value)
            old_place: Optional[TypedID] = self.get_attribute('current_place', None)
            super().set_attribute(key, validated_value, world)
            new_place: Optional[TypedID] = self.get_attribute('current_place', None)
            if old_place != new_place and world is not None:
                try:
                    self._update_location_relationship(old_place, new_place, world)
                except Exception as e:
                    logging.error(f"Character '{self.typed_id}' 更新 current_place 关系时失败 (从 {old_place} 到 {new_place}): {e}", exc_info=True)
                    raise e
        elif key == 'has_items':
            validated_value: List[TypedID] = self._validate_has_items_value(value)
            super().set_attribute(key, validated_value, world)
        else:
            super().set_attribute(key, value, world)

    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any], world: Optional[WorldState] = None):
        """覆盖：处理 current_place，其他委托基类。"""
        op, value = opAndValue
        logging.debug(f"Character '{self.typed_id}': modify_attribute: Key='{key}', Op='{op}', Value='{repr(value)}'")
        if key == 'current_place':
            if op == '=':
                self.set_attribute(key, value, world)
            else:
                raise ValueError(f"Character current_place 不支持 '{op}'")
        elif key == 'has_items':
            # 基类会处理列表操作，这里可能需要预验证 value (如果 op 是 +/-)
            # 但基类的 _ensure_references_exist 会检查，所以这里直接委托
            logging.debug(f"Character '{self.typed_id}': 委托给基类处理 has_items (key='{key}')")
            super().modify_attribute(key, opAndValue, world)
        else:
            logging.debug(f"Character '{self.typed_id}': 委托给基类 for key '{key}'")
            super().modify_attribute(key, opAndValue, world)


class Place(BaseEntity):
    entity_type: Literal["Place"] = "Place"

    # --- 验证函数 (修改：使用 TypedID) ---
    def _validate_contents_value(self, value: Any) -> List[TypedID]:
        """验证 contents 值，必须是包含 ('Item'|'Character', ID) TypedID 的列表"""
        if not isinstance(value, list): raise TypeError("contents 必须是列表")
        validated_list: List[TypedID] = []
        for i, entity_ref in enumerate(value):
            validated_id: Optional[TypedID] = None
            if isinstance(entity_ref, TypedID):
                if entity_ref.type not in ["Item", "Character"]:
                    raise ValueError(f"无效 contents TypedID 类型在索引 {i}: {entity_ref.type}，需要 Item 或 Character")
                validated_id = entity_ref
            elif isinstance(entity_ref, str):
                try:
                    typed_id = TypedID.from_string(entity_ref) # 假设已实现
                    if typed_id.type not in ["Item", "Character"]:
                        raise ValueError(f"无效 contents TypedID 类型在索引 {i}: {typed_id.type}，需要 Item 或 Character")
                    logging.warning(f"Place '{self.typed_id}' contents[{i}]: 接收到字符串 '{entity_ref}'，已自动转换为 TypedID。建议直接使用 TypedID。")
                    validated_id = typed_id
                except ValueError as e:
                    raise ValueError(f"无效 contents 字符串在索引 {i}: '{entity_ref}'. {e}") from e
            else:
                 raise TypeError(f"无效 contents 元素类型在索引 {i}: {type(entity_ref)}，需要 TypedID 或 'Type:ID' 字符串。")

            validated_list.append(validated_id)
        return validated_list

    def _validate_exits_value(self, value: Any) -> Dict[str, TypedID]:
        """验证 exits 值，必须是 Dict[str, TypedID('Place', ID)]"""
        if not isinstance(value, dict): raise TypeError("exits 必须是字典")
        validated_exits: Dict[str, TypedID] = {}
        for dir_key, target_val in value.items():
            if not isinstance(dir_key, str): raise TypeError(f"exits 的键必须是字符串，得到 {type(dir_key)}")

            validated_target: Optional[TypedID] = None
            if isinstance(target_val, TypedID):
                if target_val.type != "Place":
                    raise ValueError(f"无效 exits TypedID 类型 for key '{dir_key}': {target_val.type}，需要 Place")
                validated_target = target_val
            elif isinstance(target_val, str):
                 try:
                    typed_id = TypedID.from_string(target_val) # 假设已实现
                    if typed_id.type != "Place":
                        raise ValueError(f"无效 exits TypedID 类型 for key '{dir_key}': {typed_id.type}，需要 Place")
                    logging.warning(f"Place '{self.typed_id}' exits['{dir_key}']: 接收到字符串 '{target_val}'，已自动转换为 TypedID。建议直接使用 TypedID。")
                    validated_target = typed_id
                 except ValueError as e:
                    raise ValueError(f"无效 exits 目标字符串 for key '{dir_key}': '{target_val}'. {e}") from e
            else:
                raise TypeError(f"无效 exits 目标类型 for key '{dir_key}': {type(target_val)}，需要 TypedID 或 'Place:ID' 字符串。")

            validated_exits[dir_key] = validated_target
        return validated_exits

    # --- set_attribute 和 modify_attribute (接口不变，内部依赖更新) ---
    def set_attribute(self, key: str, value: Any, world: Optional[WorldState] = None):
        """覆盖：处理 contents 和 exits 的格式验证。"""
        logging.debug(f"Place '{self.typed_id}': set_attribute: Key='{key}', Value='{repr(value)}'")
        if key == 'contents':
            validated_value: List[TypedID] = self._validate_contents_value(value)
            super().set_attribute(key, validated_value, world)
        elif key == 'exits':
            validated_value: Dict[str, TypedID] = self._validate_exits_value(value)
            super().set_attribute(key, validated_value, world)
        else:
            super().set_attribute(key, value, world)

    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any], world: Optional[WorldState] = None):
        """覆盖：基类处理通用逻辑，这里不需要特殊实现。"""
        # contents 和 exits 都由基类处理 (列表和字典)
        logging.debug(f"Place '{self.typed_id}': 委托给基类 for key '{key}'")
        super().modify_attribute(key, opAndValue, world)