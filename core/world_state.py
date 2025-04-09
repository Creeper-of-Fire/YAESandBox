# core/world_state.py
import logging
import re
from typing import List, Optional, Dict, Any, Literal, Union, Set, Tuple, ClassVar, TYPE_CHECKING, cast

from pydantic import BaseModel, Field, ValidationError, PrivateAttr

# 尝试从 processing 包导入，如果失败则使用本地定义
try:
    from processing.parser import ENTITY_REF_REGEX
except ImportError:
    logging.warning("无法从 processing.parser 导入 ENTITY_REF_REGEX，使用本地定义。")
    ENTITY_REF_REGEX = re.compile(r"^(Item|Character|Place):([\w\-]+)$", re.IGNORECASE)

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

    def get_entity_dict(self, entity_type: Literal["Item", "Character", "Place"]) -> Dict[str, AnyEntity]:
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

    # --- find_entity (修改：需要类型) ---
    def find_entity(self, entity_id: str, entity_type: Literal["Item", "Character", "Place"], include_destroyed: bool = False) -> Optional[AnyEntity]:
        """通过 ID 和 类型 精确查找实体。"""
        entity_dict = self.get_entity_dict(entity_type)
        entity = entity_dict.get(entity_id)
        if entity and (not entity.is_destroyed or include_destroyed):
            return entity
        return None

    # --- find_entity_by_ref (新增或确认存在) ---
    def find_entity_by_ref(self, ref: Optional[Union[Tuple[str, str], List[str]]], include_destroyed: bool = False) -> Optional[AnyEntity]:
        """通过 (Type, ID) 元组引用查找实体"""
        if ref is None: return None
        # 兼容 Pydantic v1 可能存为列表的情况
        if isinstance(ref, list) and len(ref) == 2:
            ref = tuple(ref)

        if isinstance(ref, tuple) and len(ref) == 2 and \
           isinstance(ref[0], str) and isinstance(ref[1], str) and \
           ref[0].capitalize() in ["Item", "Character", "Place"]:
            entity_type = cast(Literal["Item", "Character", "Place"], ref[0].capitalize())
            entity_id = ref[1]
            return self.find_entity(entity_id, entity_type, include_destroyed)
        else:
            logging.warning(f"无效的实体引用格式: {repr(ref)}")
            return None

    def find_entity_by_name(self, name: str, entity_type: Optional[Literal["Item", "Character", "Place"]] = None,
                            include_destroyed: bool = False) -> Optional[AnyEntity]:
        """按名称查找实体（效率较低，保持不变）"""
        search_dicts: List[Dict[str, AnyEntity]] = []
        if entity_type:
            search_dicts.append(self.get_entity_dict(entity_type))
        else:
            search_dicts.extend([self.items, self.characters, self.places]) # type: ignore

        for entity_dict in search_dicts:
            for entity in entity_dict.values():
                entity_name = entity.get_attribute('name')
                if entity_name == name and (not entity.is_destroyed or include_destroyed):
                    return entity
        return None

    # --- add_entity (修改：类型内冲突检查) ---
    def add_entity(self, entity: AnyEntity):
        """添加实体到世界状态，仅在同类型中检查 ID 冲突。"""
        entity_dict = self.get_entity_dict(entity.entity_type)
        if entity.entity_id in entity_dict:
             existing_entity = entity_dict[entity.entity_id]
             if not existing_entity.is_destroyed:
                  # 仅当未销毁的同类型同 ID 实体存在时才警告
                  logging.warning(f"覆盖已存在且未销毁的实体: {entity.entity_type} ID='{entity.entity_id}'")
             # 如果已销毁，直接覆盖是正常操作
        elif self.find_entity_globally(entity.entity_id):
             # 检查其他类型是否有同ID实体 (可选，用于调试)
             logging.debug(f"注意: 添加实体 {entity.entity_type}:{entity.entity_id} 时，发现其他类型存在同ID实体。")

        entity_dict[entity.entity_id] = entity
        logging.debug(f"实体 '{entity.entity_id}' ({entity.entity_type}) 已添加到 WorldState。")

    def find_entity_globally(self, entity_id: str, include_destroyed: bool = False) -> Optional[AnyEntity]:
         """(辅助函数) 跨所有类型查找第一个匹配 ID 的实体 (用于调试或旧逻辑兼容)"""
         for entity_type in ["Item", "Character", "Place"]:
              entity = self.find_entity(entity_id, entity_type, include_destroyed)
              if entity: return entity
         return None


# --- 内部辅助函数: 创建占位符 (修改：使用新的 find_entity) ---
def _create_placeholder_entity(entity_type: Literal["Item", "Character", "Place"], entity_id: str, world: WorldState, context: str = "未知操作"):
    """内部函数：创建指定类型的占位符实体并添加到 world。"""
    # 使用新的 find_entity 检查是否存在
    if world.find_entity(entity_id, entity_type, include_destroyed=True):
        logging.debug(f"[{context}] 尝试创建占位符 '{entity_id}' ({entity_type})，但同类型实体已存在（可能已销毁）。跳过创建。")
        return

    # 创建逻辑保持不变
    model_class: type[BaseEntity]
    if entity_type == "Item": model_class = Item
    elif entity_type == "Character": model_class = Character
    elif entity_type == "Place": model_class = Place
    else: raise ValueError(f"[{context}] 无效的实体类型 '{entity_type}'")

    new_entity = model_class(entity_id=entity_id, entity_type=entity_type)
    warning_prefix = "Warning: Missing"
    placeholder_name = f"{warning_prefix} {entity_type} [{entity_id}] (Auto-created by: {context})"
    new_entity._dynamic_attributes['name'] = placeholder_name
    logging.info(f"[{context}] 创建了占位符实体 '{entity_id}' ({entity_type})，名称: '{placeholder_name}'")
    world.add_entity(new_entity)


# --- 内部辅助函数: 递归确保引用存在 (修改：使用新的 find_entity) ---
def _ensure_references_exist(value: Any, world: WorldState, context: str = "未知属性"):
    """
    递归检查值中的 (Type, ID) 元组引用，如果实体不存在则创建占位符。
    直接修改传入的可变对象（列表/字典）。
    """
    if isinstance(value, tuple) and len(value) == 2 and value[0] in ["Item", "Character", "Place"] and isinstance(value[1], str):
        entity_type, entity_id = value
        # 使用新的 find_entity 检查
        if not world.find_entity(entity_id, entity_type, include_destroyed=False):
            logging.warning(f"[{context}] 引用实体 '{entity_id}' ({entity_type}) 不存在，将创建占位符。")
            try:
                # 创建逻辑不变，内部会再次用 find_entity 检查
                _create_placeholder_entity(entity_type, entity_id, world, context=f"引用自 {context}")
            except Exception as e:
                logging.error(f"[{context}] 创建占位符 '{entity_id}' ({entity_type}) 失败: {e}", exc_info=True)
                # raise RuntimeError(f"无法为引用 '{value}' 创建占位符") from e
    elif isinstance(value, list):
        for i, item in enumerate(value):
            _ensure_references_exist(item, world, context=f"{context}[{i}]")
    elif isinstance(value, dict):
        # 只检查字典的值，不检查键 (假设键不是实体引用)
        for k, v in value.items():
            _ensure_references_exist(v, world, context=f"{context}['{k}']")


# --- 核心数据模型 ---
class BaseEntity(BaseModel):
    # ... (字段定义和 get/has/delete/get_all_attributes 保持不变) ...
    entity_id: str = Field(...)
    entity_type: Literal["Item", "Character", "Place"] = Field(...)
    is_destroyed: bool = Field(False)
    _dynamic_attributes: Dict[str, Any] = PrivateAttr(default_factory=dict)
    model_config = {"validate_assignment": True, "extra": "forbid"}
    _CORE_FIELDS: ClassVar[Set[str]] = {'entity_id', 'entity_type', 'is_destroyed'}

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
                if isinstance(value, list):
                    all_attrs[key] = list(value)  # 返回副本
                elif isinstance(value, dict):
                    all_attrs[key] = dict(value)  # 返回副本
                else:
                    all_attrs[key] = value
        for key, value in self._dynamic_attributes.items():
            if isinstance(value, list):
                all_attrs[key] = list(value)  # 返回副本
            elif isinstance(value, dict):
                all_attrs[key] = dict(value)  # 返回副本
            else:
                all_attrs[key] = value
        for field in exclude_internal: all_attrs.pop(field, None)
        return all_attrs

    # --- set_attribute (保持不变，内部调用 _ensure_references_exist 已更新) ---
    def set_attribute(self, key: str, value: Any, world: Optional[WorldState] = None):
        """
        设置属性值。在设置前，会递归检查 value 中的实体引用并创建占位符。
        需要传入 world 实例。
        """
        processed_value = value  # 默认值
        if world:
            try:
                # 创建副本以避免修改原始输入值
                value_copy = None
                if isinstance(value, list):
                    value_copy = list(value)
                elif isinstance(value, dict):
                    value_copy = dict(value)
                else:
                    value_copy = value # 对于不可变类型，复制不是必须的，但保持一致
                # _ensure_references_exist 内部会使用新的 find_entity
                _ensure_references_exist(value_copy, world, context=f"{self.entity_type} '{self.entity_id}'.{key}")
                processed_value = value_copy
            except Exception as e:
                logging.error(f"实体 '{self.entity_id}': 确保属性 '{key}' 的引用时失败: {e}", exc_info=True)
                raise RuntimeError(f"确保属性 '{key}' 引用失败") from e
        elif isinstance(value, (tuple, list, dict)):
            # 检查是否有潜在未检查的引用
            has_potential_ref = False
            if isinstance(value, tuple) and len(value) == 2 and value[0] in ["Item", "Character", "Place"]:
                has_potential_ref = True
            elif isinstance(value, list):
                 # 简单检查，不递归
                 if any(isinstance(item, tuple) and len(item) == 2 and item[0] in ["Item", "Character", "Place"] for item in value):
                     has_potential_ref = True
            elif isinstance(value, dict):
                 # 简单检查值的类型
                 if any(isinstance(v, tuple) and len(v) == 2 and v[0] in ["Item", "Character", "Place"] for v in value.values()):
                     has_potential_ref = True
            if has_potential_ref:
                logging.warning(f"实体 '{self.entity_id}': set_attribute('{key}') 缺少 world，且值包含潜在引用，无法确保引用存在。")

        # 设置核心或动态属性的逻辑保持不变
        if key in self._CORE_FIELDS:
            try:
                setattr(self, key, processed_value)
            except ValidationError as e:
                 logging.error(f"实体 '{self.entity_id}': 设置核心属性 '{key}' 验证失败: {e}", exc_info=True)
                 raise e
            except Exception as e:
                logging.error(f"实体 '{self.entity_id}': 设置核心属性 '{key}' 时发生意外错误: {e}", exc_info=True)
                raise e
            logging.debug(f"实体 '{self.entity_id}': 设置核心属性 {key} = {repr(processed_value)}")
        else:
            self._dynamic_attributes[key] = processed_value
            logging.debug(f"实体 '{self.entity_id}': 设置动态属性 {key} = {repr(processed_value)}")


    # --- modify_attribute (保持不变，内部调用 _ensure_references_exist 已更新) ---
    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any], world: Optional[WorldState] = None):
        """
        修改属性值。基类现在处理数值、字符串、列表和字典的通用操作。
        需要传入 world 实例以确保引用存在。
        """
        op, value = opAndValue
        logging.debug(
            f"实体 '{self.entity_id}': 基类 modify_attribute: Key='{key}', Op='{op}', Value='{repr(value)}'")

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

        # 预处理: 确保 value 中的引用存在 (仅对 +=/+ 操作，内部已更新)
        processed_value = value
        if op in ('+=', '+'):
            if world:
                try:
                    value_copy = None
                    if isinstance(value, list): value_copy = list(value)
                    elif isinstance(value, dict): value_copy = dict(value)
                    else: value_copy = value
                    _ensure_references_exist(value_copy, world, context=f"{self.entity_type} '{self.entity_id}'.{key} (op {op})")
                    processed_value = value_copy
                except Exception as e:
                    logging.error(f"实体 '{self.entity_id}': 确保属性 '{key}' (op {op}) 的操作值引用时失败: {e}", exc_info=True)
                    raise RuntimeError(f"确保属性 '{key}' (op {op}) 操作值引用失败") from e
            elif isinstance(value, (tuple, list, dict)):
                 # 同样的潜在引用检查
                 has_potential_ref = False
                 if isinstance(value, tuple) and len(value) == 2 and value[0] in ["Item", "Character", "Place"]: has_potential_ref = True
                 elif isinstance(value, list) and any(isinstance(i, tuple) and len(i) == 2 and i[0] in ["Item", "Character", "Place"] for i in value): has_potential_ref = True
                 elif isinstance(value, dict) and any(isinstance(v, tuple) and len(v) == 2 and v[0] in ["Item", "Character", "Place"] for v in value.values()): has_potential_ref = True
                 if has_potential_ref:
                    logging.warning(f"实体 '{self.entity_id}': modify_attribute('{key}', '{op}') 缺少 world，且值包含潜在引用，无法确保操作值中的引用存在。")

        # --- 处理各种操作 (逻辑基本不变) ---
        new_value_to_set = None # 存储最终要通过 set_attribute 设置的值

        # 赋值 (=)
        if op == '=':
            self.set_attribute(key, processed_value, world) # 注意: processed_value 在这里是原始 value
            return

        # 加/合并 (+= / +)
        elif op in ('+=', '+'):
            if current_value is None:
                logging.debug(f"Modify '{key}' {op} {repr(processed_value)}: 当前值为 None，视为赋值创建。")
                new_value_to_set = processed_value # processed_value 已经过引用检查
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
                    if item not in current_list_copy:
                        current_list_copy.append(item)
                        was_modified = True
                if was_modified: new_value_to_set = current_list_copy
            elif isinstance(current_value, dict):
                if isinstance(processed_value, dict):
                    current_dict_copy = dict(current_value)
                    original_len = len(current_dict_copy)
                    current_dict_copy.update(processed_value)
                    if len(current_dict_copy) != original_len or any(current_value.get(k) != v for k,v in processed_value.items()):
                        was_modified = True
                    if was_modified: new_value_to_set = current_dict_copy
                else:
                    raise TypeError(f"字典属性 '{key}' 的 '{op}' 操作需要字典类型的值，得到 {type(processed_value)}")
            else:
                raise TypeError(f"类型 {type(current_value)} 不支持 '{op}' 操作 for key '{key}'")

        # 减/移除 (-= / -)
        elif op in ('-=', '-'):
            if current_value is None:
                logging.warning(f"Modify({self.entity_id}): 尝试对不存在的属性 '{key}' 执行 '{op}' 操作，已忽略。")
                return
            elif isinstance(current_value, (int, float)) and isinstance(value, (int, float)):
                new_value_to_set = current_value - value
                was_modified = True
            elif isinstance(current_value, list):
                current_list_copy = list(current_value)
                items_to_remove_raw = value if isinstance(value, list) else [value]
                for item in items_to_remove_raw:
                    try:
                        current_list_copy.remove(item)
                        was_modified = True
                    except ValueError: pass # 忽略移除不存在的项
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
            logging.debug(f"实体 '{self.entity_id}': 属性 '{key}' 已通过 '{op}' 修改，调用 set_attribute 写回。")
            self.set_attribute(key, new_value_to_set, world)
        else:
            logging.debug(f"实体 '{self.entity_id}': 属性 '{key}' 未发生实际修改 (op='{op}')。")


    # --- _update_location_relationship (修改：查找使用 find_entity_by_ref) ---
    def _update_location_relationship(self,
                                      old_container_spec: Optional[Union[str, Tuple[str, str], List[str]]],
                                      new_container_spec: Optional[Union[str, Tuple[str, str], List[str]]],
                                      world: WorldState):
        """更新实体移动时的容器关系。查找使用 find_entity_by_ref。"""

        def get_spec_tuple(spec: Optional[Union[str, Tuple[str, str], List[str]]]) -> Optional[Tuple[Literal["Item", "Character", "Place"], str]]:
             """确保返回 (Type, ID) 元组或 None"""
             if spec is None: return None
             # 兼容列表
             if isinstance(spec, list) and len(spec) == 2: spec = tuple(spec)

             if isinstance(spec, tuple) and len(spec) == 2 and spec[0] in ["Item", "Character", "Place"] and isinstance(spec[1], str):
                 return cast(Tuple[Literal["Item", "Character", "Place"], str], spec)
             elif isinstance(spec, str):
                 match = ENTITY_REF_REGEX.match(spec)
                 if match:
                     # 显式提供类型的字符串引用
                     return cast(Tuple[Literal["Item", "Character", "Place"], str], (match.group(1).capitalize(), match.group(2)))
                 else:
                     # 裸 ID - 不再支持，返回 None 或抛异常？这里返回 None 并记录警告
                     logging.warning(f"_update_location_relationship: 不支持裸 ID '{spec}'，无法确定类型。关系更新可能不完整。")
                     return None
             else:
                  logging.warning(f"无法解析容器标识符: {repr(spec)}")
                  return None

        old_container_ref = get_spec_tuple(old_container_spec)
        new_container_ref = get_spec_tuple(new_container_spec)

        if old_container_ref == new_container_ref: return
        logging.debug(f"实体 '{self.entity_id}': 更新容器关系: 从 '{old_container_ref}' 到 '{new_container_ref}'")

        # 从旧容器移除
        if old_container_ref:
            old_container = world.find_entity_by_ref(old_container_ref) # 使用 ref 查找
            if old_container:
                content_key = 'has_items' if isinstance(old_container, Character) else 'contents' if isinstance(old_container, Place) else None
                if content_key:
                    try:
                        # modify_attribute 使用 (Type, ID) 元组
                        old_container.modify_attribute(content_key, ('-', (self.entity_type, self.entity_id)), world=world)
                    except Exception as e:
                        logging.warning(f"从旧容器 '{old_container_ref}' 移除 '{self.entity_id}' 时出错(忽略): {e}")
            # 如果旧容器找不到，不再需要警告，因为 find_entity_by_ref 返回 None

        # 添加到新容器
        if new_container_ref:
            # _ensure_references_exist 已经被 set_attribute 调用过，这里理论上不需要再次检查
            # 但为保险起见，可以再次确认或直接查找
            new_container = world.find_entity_by_ref(new_container_ref) # 使用 ref 查找
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
                        # modify_attribute 使用 (Type, ID) 元组
                        new_container.modify_attribute(content_key, ('+', (self.entity_type, self.entity_id)), world=world)
                    except Exception as e:
                        raise RuntimeError(f"未能将 '{self.entity_id}' 添加到 '{new_container_ref}': {e}") from e
                elif not target_type_ok:
                    raise TypeError(f"类型不匹配：不能将 {self.entity_type} '{self.entity_id}' 添加到 {new_container.entity_type} '{new_container_ref}'")
            else:
                # 如果 set_attribute 里的 _ensure_references_exist 工作正常，这里不应该发生
                raise RuntimeError(f"内部错误：未能定位新容器 '{new_container_ref}' 即使在检查之后。")


# --- 子类定义 (基本保持不变，验证函数需要强制返回元组) ---

class Item(BaseEntity):
    entity_type: Literal["Item"] = "Item"

    # --- 验证函数 (修改：强制返回元组或 None，不再接受裸 ID) ---
    def _validate_location_value(self, value: Any) -> Union[None, Tuple[Literal["Place", "Character"], str]]:
        """验证 location 值，必须是 (Type, ID) 元组或 None"""
        if value is None: return None
        # 兼容列表
        if isinstance(value, list) and len(value) == 2: value = tuple(value)

        if isinstance(value, tuple):
            if not (len(value) == 2 and value[0] in ["Place", "Character"] and isinstance(value[1], str) and value[1]):
                raise ValueError(f"无效 location 元组: {repr(value)}")
            return value # type: ignore
        elif isinstance(value, str):
            match = ENTITY_REF_REGEX.match(value)
            if match and match.group(1).capitalize() in ["Place", "Character"]:
                logging.warning(f"Item '{self.entity_id}' location: 接收到字符串 '{value}'，已自动转换为元组。建议直接使用元组。")
                return cast(Tuple[Literal["Place", "Character"], str], (match.group(1).capitalize(), match.group(2)))
            else:
                raise ValueError(f"无效 location 字符串: '{value}'。必须是 'Type:ID' 格式或 (Type, ID) 元组。")
        else:
            raise TypeError(f"无效 location 类型: {type(value)}，需要元组或 'Type:ID' 字符串。")

    # --- set_attribute 和 modify_attribute (保持不变，依赖验证函数的更新) ---
    def set_attribute(self, key: str, value: Any, world: Optional[WorldState] = None):
        """覆盖：处理 quantity 验证和 location 关系更新。"""
        logging.debug(f"Item '{self.entity_id}': set_attribute: Key='{key}', Value='{repr(value)}'")
        if key == 'quantity':
            if not isinstance(value, int) or value < 0: raise ValueError("Quantity 必须非负整数")
            super().set_attribute(key, value, world)
        elif key == 'location':
            # 验证并获取标准格式 (元组或 None)
            validated_value = self._validate_location_value(value)
            old_location = self.get_attribute('location', None) # 获取当前值用于比较
            # 调用基类设置，基类会处理引用检查
            super().set_attribute(key, validated_value, world)
            # 在基类设置成功后，再次获取新值进行比较并更新关系
            new_location = self.get_attribute('location', None)
            if old_location != new_location and world is not None: # 仅当 world 存在时更新关系
                try:
                    self._update_location_relationship(old_location, new_location, world)
                except Exception as e:
                    # 在关系更新失败时回滚位置更改？或者记录错误并继续？
                    # 简单起见，记录错误并允许传播
                    logging.error(f"Item '{self.entity_id}' 更新 location 关系时失败 (从 {old_location} 到 {new_location}): {e}", exc_info=True)
                    raise e # 或者可以只记录错误，不重新抛出，取决于业务需求
        else:
            super().set_attribute(key, value, world)

    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any], world: Optional[WorldState] = None):
        """覆盖：处理 quantity 计算，其他委托基类。"""
        op, value = opAndValue
        logging.debug(f"Item '{self.entity_id}': modify_attribute: Key='{key}', Op='{op}', Value='{repr(value)}'")
        if key == 'quantity':
            current_quantity = self.get_attribute('quantity', 1) # 假设默认值？或者应该检查是否存在？
            new_quantity = None
            if op == '=': new_quantity = value
            elif op in ('+=', '+') and isinstance(value, int): new_quantity = current_quantity + value
            elif op in ('-=', '-') and isinstance(value, int): new_quantity = current_quantity - value
            else: raise ValueError(f"Quantity 不支持 '{op}' 或无效类型 ({type(value)})")
            # 使用 set_attribute 设置新值，它会进行验证
            self.set_attribute(key, new_quantity, world)
        elif key == 'location':
            if op == '=':
                 # 委托给 set_attribute 处理验证和关系更新
                 self.set_attribute(key, value, world)
            else:
                 raise ValueError(f"Item location 不支持 '{op}'")
        else:
            logging.debug(f"Item '{self.entity_id}': 委托给基类 for key '{key}'")
            super().modify_attribute(key, opAndValue, world)


class Character(BaseEntity):
    entity_type: Literal["Character"] = "Character"

    # --- 验证函数 (修改：强制返回元组或 None) ---
    def _validate_current_place_value(self, value: Any) -> Union[None, Tuple[Literal["Place"], str]]:
        """验证 current_place 值，必须是 ('Place', ID) 元组或 None"""
        if value is None: return None
        if isinstance(value, list) and len(value) == 2: value = tuple(value) # 兼容

        if isinstance(value, tuple):
            if not (len(value) == 2 and value[0] == "Place" and isinstance(value[1], str) and value[1]):
                 raise ValueError(f"无效 current_place 元组: {repr(value)}")
            return value # type: ignore
        elif isinstance(value, str):
             match = ENTITY_REF_REGEX.match(value)
             if match and match.group(1).capitalize() == "Place":
                 logging.warning(f"Character '{self.entity_id}' current_place: 接收到字符串 '{value}'，已自动转换为元组。建议直接使用元组。")
                 return cast(Tuple[Literal["Place"], str], ("Place", match.group(2)))
             else:
                 raise ValueError(f"无效 current_place 字符串: '{value}'。必须是 'Place:ID' 格式或 ('Place', ID) 元组。")
        else:
            raise TypeError(f"无效 current_place 类型: {type(value)}，需要元组或 'Place:ID' 字符串。")

    def _validate_has_items_value(self, value: Any) -> List[Tuple[Literal["Item"], str]]:
        """验证 has_items 值，必须是包含 ('Item', ID) 元组的列表"""
        if not isinstance(value, list): raise TypeError("has_items 必须是列表")
        validated_list = []
        for item_ref in value:
             ref_tuple = None
             if isinstance(item_ref, list) and len(item_ref) == 2: item_ref = tuple(item_ref) # 兼容

             if isinstance(item_ref, tuple):
                 if not (len(item_ref) == 2 and item_ref[0] == "Item" and isinstance(item_ref[1], str) and item_ref[1]):
                      raise ValueError(f"无效 has_items 元组: {repr(item_ref)}")
                 ref_tuple = item_ref # type: ignore
             elif isinstance(item_ref, str):
                 match = ENTITY_REF_REGEX.match(item_ref)
                 if match and match.group(1).capitalize() == "Item":
                      logging.warning(f"Character '{self.entity_id}' has_items: 接收到字符串 '{item_ref}'，已自动转换为元组。建议直接使用元组。")
                      ref_tuple = cast(Tuple[Literal["Item"], str], ("Item", match.group(2)))
                 else:
                      raise ValueError(f"无效 has_items 字符串: '{item_ref}'。必须是 'Item:ID' 格式或 ('Item', ID) 元组。")
             else:
                 raise TypeError(f"无效 has_items 元素类型: {type(item_ref)}")
             validated_list.append(ref_tuple)
        return validated_list

    # --- set_attribute 和 modify_attribute (保持不变，依赖验证函数更新) ---
    def set_attribute(self, key: str, value: Any, world: Optional[WorldState] = None):
        """覆盖：处理 current_place 关系更新和 has_items 列表类型验证。"""
        logging.debug(f"Character '{self.entity_id}': set_attribute: Key='{key}', Value='{repr(value)}'")
        if key == 'current_place':
            validated_value = self._validate_current_place_value(value)
            old_place = self.get_attribute('current_place', None)
            super().set_attribute(key, validated_value, world) # 基类处理引用检查
            new_place = self.get_attribute('current_place', None)
            if old_place != new_place and world is not None:
                try:
                    self._update_location_relationship(old_place, new_place, world)
                except Exception as e:
                    logging.error(f"Character '{self.entity_id}' 更新 current_place 关系时失败 (从 {old_place} 到 {new_place}): {e}", exc_info=True)
                    raise e
        elif key == 'has_items':
            validated_value = self._validate_has_items_value(value) # 仅验证格式
            super().set_attribute(key, validated_value, world) # 基类处理引用检查和设置
        else:
            super().set_attribute(key, value, world)

    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any], world: Optional[WorldState] = None):
        """覆盖：处理 current_place，其他委托基类。"""
        op, value = opAndValue
        logging.debug(f"Character '{self.entity_id}': modify_attribute: Key='{key}', Op='{op}', Value='{repr(value)}'")
        if key == 'current_place':
            if op == '=':
                self.set_attribute(key, value, world)
            else:
                raise ValueError(f"Character current_place 不支持 '{op}'")
        elif key == 'has_items':
            # 不再需要检查类型，基类会处理
            logging.debug(f"Character '{self.entity_id}': 委托给基类处理 has_items (key='{key}')")
            super().modify_attribute(key, opAndValue, world)
        else:
            logging.debug(f"Character '{self.entity_id}': 委托给基类 for key '{key}'")
            super().modify_attribute(key, opAndValue, world)


class Place(BaseEntity):
    entity_type: Literal["Place"] = "Place"

    # --- 验证函数 (修改：强制返回元组或 None) ---
    def _validate_contents_value(self, value: Any) -> List[Tuple[Literal["Item", "Character"], str]]:
        """验证 contents 值，必须是包含 ('Item'|'Character', ID) 元组的列表"""
        if not isinstance(value, list): raise TypeError("contents 必须是列表")
        validated_list = []
        for entity_ref in value:
            ref_tuple = None
            if isinstance(entity_ref, list) and len(entity_ref) == 2: entity_ref = tuple(entity_ref) # 兼容

            if isinstance(entity_ref, tuple):
                if not (len(entity_ref) == 2 and entity_ref[0] in ["Item", "Character"] and isinstance(entity_ref[1], str) and entity_ref[1]):
                     raise ValueError(f"无效 contents 元组: {repr(entity_ref)}")
                ref_tuple = cast(Tuple[Literal["Item", "Character"], str], entity_ref)
            elif isinstance(entity_ref, str):
                 match = ENTITY_REF_REGEX.match(entity_ref)
                 if match and match.group(1).capitalize() in ["Item", "Character"]:
                      logging.warning(f"Place '{self.entity_id}' contents: 接收到字符串 '{entity_ref}'，已自动转换为元组。建议直接使用元组。")
                      ref_tuple = cast(Tuple[Literal["Item", "Character"], str], (match.group(1).capitalize(), match.group(2)))
                 else:
                      raise ValueError(f"无效 contents 字符串: '{entity_ref}'。必须是 'Type:ID' 格式或 (Type, ID) 元组。")
            else:
                raise TypeError(f"无效 contents 元素类型: {type(entity_ref)}")
            validated_list.append(ref_tuple)
        return validated_list

    # --- set_attribute 和 modify_attribute (保持不变，依赖验证函数更新) ---
    def set_attribute(self, key: str, value: Any, world: Optional[WorldState] = None):
        """覆盖：处理 contents 和 exits 的格式验证。"""
        logging.debug(f"Place '{self.entity_id}': set_attribute: Key='{key}', Value='{repr(value)}'")
        if key == 'contents':
            validated_value = self._validate_contents_value(value)
            super().set_attribute(key, validated_value, world) # 基类处理引用检查和设置
        # exits 字典的值也可能是实体引用，set_attribute 的基类实现会通过 _ensure_references_exist 处理
        # exits 键是字符串，不需要特殊验证
        # exits 值应为 ('Place', ID) 元组
        elif key == 'exits':
            if not isinstance(value, dict): raise TypeError("exits 必须是字典")
            # 可以在这里添加对字典值的验证，确保它们是 ('Place', ID) 格式
            validated_exits = {}
            for dir_key, target_val in value.items():
                if isinstance(target_val, list) and len(target_val) == 2: target_val = tuple(target_val) # 兼容
                if isinstance(target_val, tuple) and len(target_val) == 2 and target_val[0] == "Place" and isinstance(target_val[1], str):
                    validated_exits[dir_key] = target_val
                elif isinstance(target_val, str):
                    match = ENTITY_REF_REGEX.match(target_val)
                    if match and match.group(1).capitalize() == "Place":
                        logging.warning(f"Place '{self.entity_id}' exits['{dir_key}']: 接收到字符串 '{target_val}'，已自动转换为元组。建议直接使用元组。")
                        validated_exits[dir_key] = ("Place", match.group(2))
                    else:
                         raise ValueError(f"无效 exits 目标字符串: '{target_val}' for key '{dir_key}'")
                else:
                    raise TypeError(f"无效 exits 目标类型: {type(target_val)} for key '{dir_key}'")
            super().set_attribute(key, validated_exits, world)
        else:
            super().set_attribute(key, value, world)

    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any], world: Optional[WorldState] = None):
        """覆盖：验证 exits/contents 操作的值类型，其他委托基类。"""
        op, value = opAndValue
        logging.debug(f"Place '{self.entity_id}': modify_attribute: Key='{key}', Op='{op}', Value='{repr(value)}'")

        # contents 和 exits 都由基类处理 (列表和字典)
        # 不再需要检查类型，基类会处理
        logging.debug(f"Place '{self.entity_id}': 委托给基类 for key '{key}'")
        super().modify_attribute(key, opAndValue, world)