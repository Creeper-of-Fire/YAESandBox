# core/world_state.py
import logging
import re
from typing import List, Optional, Dict, Any, Literal, Union, Set, Tuple, ClassVar, TYPE_CHECKING, cast, Type

# 从新的类型模块导入
from core.types import TypedID, EntityType

from pydantic import BaseModel, Field, ValidationError, PrivateAttr

# 移除不再需要的导入和函数
# import re # 不再需要 ENTITY_REF_REGEX
# def _ensure_references_exist(...) # 移除
# def _create_placeholder_entity(...) # 移除 (核心不再创建占位符)

if TYPE_CHECKING:
    # 防止循环导入，只在类型检查时导入 WorldState
    pass

# --- 前向声明实体类型 ---
AnyEntity = Union['Item', 'Character', 'Place']

# --- WorldState 定义 (基本不变) ---
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

    # --- find_entity (保持不变) ---
    def find_entity(self, ref: TypedID, include_destroyed: bool = False) -> Optional[AnyEntity]:
        """通过 TypedID 精确查找实体。"""
        entity_dict = self.get_entity_dict(ref.type)
        entity = entity_dict.get(ref.id)
        if entity and (not entity.is_destroyed or include_destroyed):
            return entity
        return None

    # --- find_entity_by_id (旧接口，如果外部还需要，可以保留，内部尽量用 find_entity) ---
    def find_entity_by_id(self, entity_id: str, entity_type: EntityType, include_destroyed: bool = False) -> Optional[AnyEntity]:
        """通过 ID 和 类型 精确查找实体。(兼容旧接口)"""
        ref = TypedID(type=entity_type, id=entity_id)
        return self.find_entity(ref, include_destroyed)

    # --- 移除 find_entity_by_typed_id，直接使用 find_entity ---
    # def find_entity_by_typed_id(self, ref: Optional[TypedID], include_destroyed: bool = False) -> Optional[AnyEntity]:
    #     """通过 TypedID 引用查找实体"""
    #     if ref is None:
    #         return None
    #     return self.find_entity(ref.id, ref.type, include_destroyed)

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

    # --- add_entity (保持不变) ---
    def add_entity(self, entity: AnyEntity):
        """添加实体到世界状态，仅在同类型中检查 ID 冲突。"""
        entity_dict = self.get_entity_dict(entity.entity_type)
        if entity.entity_id in entity_dict:
            existing_entity = entity_dict[entity.entity_id]
            if not existing_entity.is_destroyed:
                logging.warning(f"覆盖已存在且未销毁的实体: {entity.typed_id}")
        # 移除全局查找，因为不推荐跨类型 ID 冲突
        # elif self.find_entity_globally(entity.entity_id):
        #     logging.debug(f"注意: 添加实体 {entity.typed_id} 时，发现其他类型存在同ID实体。")

        entity_dict[entity.entity_id] = entity
        logging.debug(f"实体 '{entity.typed_id}' 已添加到 WorldState。")

    # --- 移除 find_entity_globally ---
    # def find_entity_globally(self, entity_id: str, include_destroyed: bool = False) -> Optional[AnyEntity]:
    #     """(辅助函数) 跨所有类型查找第一个匹配 ID 的实体 (用于调试或旧逻辑兼容)"""
    #     # ... (移除)


# --- 移除 内部辅助函数 ---
# def _create_placeholder_entity(...)
# def _ensure_references_exist(...)


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

    # --- get_attribute, has_attribute, delete_attribute (保持不变) ---
    def get_attribute(self, key: str, default: Any = None) -> Any:
        if key in self._CORE_FIELDS:
            return getattr(self, key)
        else:
            return self._dynamic_attributes.get(key, default)

    def has_attribute(self, key: str) -> bool:
        return key in self._CORE_FIELDS or key in self._dynamic_attributes

    def delete_attribute(self, key: str) -> bool:
        if key in self._CORE_FIELDS:
            logging.warning(f"尝试删除核心属性 '{key}' (实体: {self.typed_id})，已忽略。")
            return False
        elif key in self._dynamic_attributes:
            del self._dynamic_attributes[key]
            logging.debug(f"实体 '{self.typed_id}': 删除动态属性 '{key}'。")
            return True
        else:
            logging.debug(f"实体 '{self.typed_id}': 尝试删除不存在的动态属性 '{key}'。")
            return False

    # --- get_all_attributes (保持不变) ---
    def get_all_attributes(self, exclude_internal: Set[str] = {'_dynamic_attributes', 'model_config'}) -> Dict[str, Any]:
        all_attrs = {}
        for key in self._CORE_FIELDS:
            if key not in exclude_internal:
                value = getattr(self, key)
                all_attrs[key] = value
        for key, value in self._dynamic_attributes.items():
            if isinstance(value, list):
                 all_attrs[key] = list(value)
            elif isinstance(value, dict):
                 all_attrs[key] = dict(value)
            else:
                all_attrs[key] = value
        for field in exclude_internal: all_attrs.pop(field, None)
        return all_attrs

    # --- set_attribute (简化：移除 world 参数和引用检查逻辑) ---
    def set_attribute(self, key: str, value: Any):
        """
        设置属性值。不执行引用检查或关系维护。
        由调用者（通常是原子 API 端点或其内部逻辑）确保值的类型和有效性。
        """
        # 子类可能仍需覆盖此方法进行特定验证 (如 quantity, location 类型)
        # 但不再处理 WorldState 相关的逻辑
        logging.debug(f"实体 '{self.typed_id}': (Core) set_attribute: Key='{key}', Value='{repr(value)}'")

        # 设置核心或动态属性的逻辑保持不变
        if key in self._CORE_FIELDS:
            try:
                # Pydantic 验证仍然有效
                setattr(self, key, value)
            except ValidationError as e:
                logging.error(f"实体 '{self.typed_id}': 设置核心属性 '{key}' 验证失败: {e}", exc_info=False) # 减少堆栈噪音
                raise e # 重新抛出，让上层处理
            except Exception as e:
                logging.error(f"实体 '{self.typed_id}': 设置核心属性 '{key}' 时发生意外错误: {e}", exc_info=True)
                raise e
            logging.debug(f"实体 '{self.typed_id}': 设置核心属性 {key} = {repr(value)}")
        else:
            self._dynamic_attributes[key] = value
            logging.debug(f"实体 '{self.typed_id}': 设置动态属性 {key} = {repr(value)}")

    # --- modify_attribute (简化：移除 world 参数和引用检查逻辑) ---
    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any]):
        """
        修改属性值。基类处理数值、字符串、列表和字典的通用操作。
        不执行引用检查或关系维护。
        """
        op, value = opAndValue
        logging.debug(
            f"实体 '{self.typed_id}': (Core) modify_attribute: Key='{key}', Op='{op}', Value='{repr(value)}'")

        # 核心属性处理 (仅支持 '=')
        if key in self._CORE_FIELDS:
            if op == '=':
                # 调用简化的 set_attribute
                self.set_attribute(key, value)
            else:
                raise ValueError(f"核心属性 '{key}' 不支持操作符 '{op}'")
            return

        # 获取当前值 (不变)
        current_value = self.get_attribute(key)
        was_modified = False
        new_value_to_set = None

        # --- 移除 预处理：确保 value 中的 TypedID 引用存在 ---
        # processed_value = value # 直接使用 value

        # --- 处理各种操作 (逻辑基本不变，依赖 TypedID 的 __eq__) ---

        # 赋值 (=)
        if op == '=':
            # 调用简化的 set_attribute
            self.set_attribute(key, value)
            return # set_attribute 已完成，直接返回

        # 加/合并 (+= / +)
        elif op in ('+=', '+'):
            if current_value is None:
                logging.debug(f"Modify '{key}' {op} {repr(value)}: 当前值为 None，视为首次设置。")
                # 修正：根据 value 类型决定行为 (逻辑不变)
                if isinstance(value, list): new_value_to_set = value
                elif isinstance(value, str): new_value_to_set = value
                else: new_value_to_set = [value] # 默认放入列表
                was_modified = True
            elif isinstance(current_value, (int, float)) and isinstance(value, (int, float)):
                new_value_to_set = current_value + value
                was_modified = True
            elif isinstance(current_value, str) and isinstance(value, str):
                new_value_to_set = current_value + value
                was_modified = True
            elif isinstance(current_value, list):
                current_list_copy = list(current_value)
                items_to_add = value if isinstance(value, list) else [value]
                for item in items_to_add:
                    # 使用 TypedID 的 __eq__ 进行比较
                    if item not in current_list_copy:
                        current_list_copy.append(item)
                        was_modified = True
                if was_modified: new_value_to_set = current_list_copy
            elif isinstance(current_value, dict):
                if isinstance(value, dict):
                    current_dict_copy = dict(current_value)
                    original_len = len(current_dict_copy)
                    current_dict_copy.update(value)
                    if len(current_dict_copy) != original_len or any(current_value.get(k) != v for k, v in value.items()):
                        was_modified = True
                    if was_modified: new_value_to_set = current_dict_copy
                else:
                    raise TypeError(f"字典属性 '{key}' 的 '{op}' 操作需要字典类型的值，得到 {type(value)}")
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
                items_to_remove_raw = value if isinstance(value, list) else [value]
                for item in items_to_remove_raw:
                    try:
                        # 列表移除依赖元素的 __eq__ 方法，TypedID 已实现
                        current_list_copy.remove(item)
                        was_modified = True
                    except ValueError: pass # 忽略移除不存在的元素
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
            # 调用简化的 set_attribute
            self.set_attribute(key, new_value_to_set)
        else:
            logging.debug(f"实体 '{self.typed_id}': 属性 '{key}' 未发生实际修改 (op='{op}')。")

    # --- 移除 _update_location_relationship ---
    # def _update_location_relationship(...)


# --- 子类定义 (移除 world 参数, 移除关系更新调用) ---

class Item(BaseEntity):
    entity_type: Literal["Item"] = "Item"

    # --- 验证函数 (保持不变，仅验证类型) ---
    def _validate_location_value(self, value: Any) -> Union[None, TypedID]:
        """验证 location 值，必须是 ('Place'|'Character', ID) 的 TypedID 或 None"""
        if value is None: return None
        if isinstance(value, TypedID):
            if value.type not in ["Place", "Character"]:
                raise ValueError(f"无效 location TypedID 类型: {value.type}，需要 Place 或 Character")
            return value
        # 移除字符串自动转换，强制要求上游传入 TypedID
        # elif isinstance(value, str): ...
        else:
            raise TypeError(f"无效 location 类型: {type(value)}，需要 TypedID 或 None。")

    # --- set_attribute (移除 world, 移除关系更新) ---
    def set_attribute(self, key: str, value: Any):
        """覆盖：处理 quantity 验证和 location 类型验证。"""
        logging.debug(f"Item '{self.typed_id}': (Core) set_attribute: Key='{key}', Value='{repr(value)}'")
        if key == 'quantity':
            if not isinstance(value, int) or value < 0: raise ValueError("Quantity 必须非负整数")
            super().set_attribute(key, value)
        elif key == 'location':
            validated_value: Optional[TypedID] = self._validate_location_value(value)
            # --- 移除关系更新逻辑 ---
            # old_location: Optional[TypedID] = self.get_attribute('location', None)
            super().set_attribute(key, validated_value)
            # new_location: Optional[TypedID] = self.get_attribute('location', None)
            # if old_location != new_location and world is not None: ... # 移除
        else:
            super().set_attribute(key, value)

    # --- modify_attribute (移除 world) ---
    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any]):
        """覆盖：处理 quantity 计算，其他委托基类。"""
        op, value = opAndValue
        logging.debug(f"Item '{self.typed_id}': (Core) modify_attribute: Key='{key}', Op='{op}', Value='{repr(value)}'")
        if key == 'quantity':
            current_quantity = self.get_attribute('quantity', 0)
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
            # 调用简化的 set_attribute
            self.set_attribute(key, new_quantity)
        elif key == 'location':
            if op == '=':
                # 调用简化的 set_attribute (内部会验证类型)
                self.set_attribute(key, value)
            else:
                raise ValueError(f"Item location 不支持 '{op}'")
        else:
            logging.debug(f"Item '{self.typed_id}': 委托给基类 for key '{key}'")
            super().modify_attribute(key, opAndValue)


class Character(BaseEntity):
    entity_type: Literal["Character"] = "Character"

    # --- 验证函数 (保持不变，仅验证类型) ---
    def _validate_current_place_value(self, value: Any) -> Union[None, TypedID]:
        """验证 current_place 值，必须是 ('Place', ID) 的 TypedID 或 None"""
        if value is None: return None
        if isinstance(value, TypedID):
            if value.type != "Place":
                raise ValueError(f"无效 current_place TypedID 类型: {value.type}，需要 Place")
            return value
        # 移除字符串自动转换
        else:
            raise TypeError(f"无效 current_place 类型: {type(value)}，需要 TypedID 或 None。")

    def _validate_has_items_value(self, value: Any) -> List[TypedID]:
        """验证 has_items 值，必须是包含 ('Item', ID) TypedID 的列表"""
        if not isinstance(value, list): raise TypeError("has_items 必须是列表")
        validated_list: List[TypedID] = []
        for i, item_ref in enumerate(value):
            if isinstance(item_ref, TypedID):
                if item_ref.type != "Item":
                     raise ValueError(f"无效 has_items TypedID 类型在索引 {i}: {item_ref.type}，需要 Item")
                validated_list.append(item_ref)
            # 移除字符串自动转换
            else:
                raise TypeError(f"无效 has_items 元素类型在索引 {i}: {type(item_ref)}，需要 TypedID。")
        return validated_list

    # --- set_attribute (移除 world, 移除关系更新) ---
    def set_attribute(self, key: str, value: Any):
        """覆盖：处理 current_place 和 has_items 列表类型验证。"""
        logging.debug(f"Character '{self.typed_id}': (Core) set_attribute: Key='{key}', Value='{repr(value)}'")
        if key == 'current_place':
            validated_value: Optional[TypedID] = self._validate_current_place_value(value)
            # --- 移除关系更新逻辑 ---
            # old_place: Optional[TypedID] = self.get_attribute('current_place', None)
            super().set_attribute(key, validated_value)
            # new_place: Optional[TypedID] = self.get_attribute('current_place', None)
            # if old_place != new_place and world is not None: ... # 移除
        elif key == 'has_items':
            validated_value: List[TypedID] = self._validate_has_items_value(value)
            super().set_attribute(key, validated_value)
        else:
            super().set_attribute(key, value)

    # --- modify_attribute (移除 world) ---
    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any]):
        """覆盖：处理 current_place，其他委托基类。"""
        op, value = opAndValue
        logging.debug(f"Character '{self.typed_id}': (Core) modify_attribute: Key='{key}', Op='{op}', Value='{repr(value)}'")
        if key == 'current_place':
            if op == '=':
                # 调用简化的 set_attribute (内部验证类型)
                self.set_attribute(key, value)
            else:
                raise ValueError(f"Character current_place 不支持 '{op}'")
        elif key == 'has_items':
            # 基类处理列表操作，验证由 set_attribute (如果基类调用) 或 _validate_has_items_value (如果直接赋值) 完成
            logging.debug(f"Character '{self.typed_id}': 委托给基类处理 has_items (key='{key}')")
            super().modify_attribute(key, opAndValue)
        else:
            logging.debug(f"Character '{self.typed_id}': 委托给基类 for key '{key}'")
            super().modify_attribute(key, opAndValue)


class Place(BaseEntity):
    entity_type: Literal["Place"] = "Place"

    # --- 验证函数 (保持不变，仅验证类型) ---
    def _validate_contents_value(self, value: Any) -> List[TypedID]:
        """验证 contents 值，必须是包含 ('Item'|'Character', ID) TypedID 的列表"""
        if not isinstance(value, list): raise TypeError("contents 必须是列表")
        validated_list: List[TypedID] = []
        for i, entity_ref in enumerate(value):
            if isinstance(entity_ref, TypedID):
                if entity_ref.type not in ["Item", "Character"]:
                    raise ValueError(f"无效 contents TypedID 类型在索引 {i}: {entity_ref.type}，需要 Item 或 Character")
                validated_list.append(entity_ref)
            # 移除字符串自动转换
            else:
                 raise TypeError(f"无效 contents 元素类型在索引 {i}: {type(entity_ref)}，需要 TypedID。")
        return validated_list

    def _validate_exits_value(self, value: Any) -> Dict[str, TypedID]:
        """验证 exits 值，必须是 Dict[str, TypedID('Place', ID)]"""
        if not isinstance(value, dict): raise TypeError("exits 必须是字典")
        validated_exits: Dict[str, TypedID] = {}
        for dir_key, target_val in value.items():
            if not isinstance(dir_key, str): raise TypeError(f"exits 的键必须是字符串，得到 {type(dir_key)}")

            if isinstance(target_val, TypedID):
                if target_val.type != "Place":
                    raise ValueError(f"无效 exits TypedID 类型 for key '{dir_key}': {target_val.type}，需要 Place")
                validated_exits[dir_key] = target_val
            # 移除字符串自动转换
            else:
                raise TypeError(f"无效 exits 目标类型 for key '{dir_key}': {type(target_val)}，需要 TypedID。")
        return validated_exits

    # --- set_attribute (移除 world) ---
    def set_attribute(self, key: str, value: Any):
        """覆盖：处理 contents 和 exits 的格式验证。"""
        logging.debug(f"Place '{self.typed_id}': (Core) set_attribute: Key='{key}', Value='{repr(value)}'")
        if key == 'contents':
            validated_value: List[TypedID] = self._validate_contents_value(value)
            super().set_attribute(key, validated_value)
        elif key == 'exits':
            validated_value: Dict[str, TypedID] = self._validate_exits_value(value)
            super().set_attribute(key, validated_value)
        else:
            super().set_attribute(key, value)

    # --- modify_attribute (移除 world) ---
    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any]):
        """覆盖：基类处理通用逻辑，这里不需要特殊实现。"""
        # contents 和 exits 都由基类处理 (列表和字典)
        logging.debug(f"Place '{self.typed_id}': 委托给基类 for key '{key}'")
        super().modify_attribute(key, opAndValue)