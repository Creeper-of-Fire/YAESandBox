# world_state.py
import logging
from typing import List, Optional, Dict, Any, Literal, Union, Set, Tuple, ClassVar

from pydantic import BaseModel, Field, ValidationError, PrivateAttr


# --- 核心数据模型 ---
class BaseEntity(BaseModel):
    """所有游戏世界实体的基类，封装动态属性"""
    entity_id: str = Field(...)
    entity_type: Literal["Item", "Character", "Place"] = Field(...)
    is_destroyed: bool = Field(False)

    _dynamic_attributes: Dict[str, Any] = PrivateAttr(default_factory=dict)

    model_config = {"validate_assignment": True, "extra": "forbid"}
    _CORE_FIELDS: ClassVar[Set[str]] = {'entity_id', 'entity_type', 'is_destroyed'}

    @property
    def name(self) -> str:
        """提供方便的 name 属性访问，从动态属性获取"""
        try:
            value = self.get_attribute('name')
            return str(value) if value is not None else f"<{self.entity_id}>"
        except AttributeError:
            logging.warning(f"实体 '{self.entity_id}' 缺少 'name' 属性，返回 ID 作为替代。")
            return f"<{self.entity_id}>"

    @name.setter
    def name(self, value: str):
        """提供方便的 name 属性设置，存储到动态属性"""
        self.set_attribute('name', value)

    def get_attribute(self, key: str) -> Any:
        """获取核心属性或动态属性"""
        if key in self._CORE_FIELDS:
            return getattr(self, key)
        elif key in self._dynamic_attributes:
            return self._dynamic_attributes[key]
        else:
            raise AttributeError(f"实体 '{self.entity_id}' 上找不到属性 '{key}'")

    def has_attribute(self, key: str) -> bool:
        """检查实体是否具有某个属性（核心或动态）"""
        return key in self._CORE_FIELDS or key in self._dynamic_attributes

    def set_attribute(self, key: str, value: Any):
        """设置核心属性（带验证）或动态属性"""
        if key in self._CORE_FIELDS:
            try:
                setattr(self, key, value)
                logging.debug(f"实体 '{self.entity_id}': 设置核心属性 {key} = {repr(value)}")
            except ValidationError as e:
                logging.error(f"设置核心属性 '{key}' (值: {repr(value)}) 时验证失败: {e}")
                raise e
            except Exception as e:
                logging.error(f"设置核心属性 '{key}' 时发生未知错误: {e}", exc_info=True)
                raise e
        else:
            self._dynamic_attributes[key] = value
            logging.debug(f"实体 '{self.entity_id}': 设置动态属性 {key} = {repr(value)}")

    def delete_attribute(self, key: str) -> bool:
        """删除动态属性，核心属性不可删除"""
        if key in self._CORE_FIELDS:
            logging.warning(f"不能删除核心属性 '{key}' (实体: {self.entity_id})")
            return False
        elif key in self._dynamic_attributes:
            del self._dynamic_attributes[key]
            logging.debug(f"实体 '{self.entity_id}': 删除了动态属性 '{key}'")
            return True
        else:
            logging.debug(f"实体 '{self.entity_id}': 尝试删除不存在的动态属性 '{key}'")
            return False

    def get_all_attributes(self, exclude_internal: Set[str] = {'_dynamic_attributes', 'model_config'}) -> Dict[
        str, Any]:
        """获取实体所有用户可见属性的字典（核心字段 + 动态属性），用于摘要"""
        all_attrs = {}
        for key in self._CORE_FIELDS:  # 添加核心字段
            if key not in exclude_internal:
                value = getattr(self, key)
                if isinstance(value, list):
                    all_attrs[key] = list(value)
                elif isinstance(value, dict):
                    all_attrs[key] = dict(value)
                else:
                    all_attrs[key] = value
        all_attrs.update(self._dynamic_attributes)  # 添加动态属性
        for field in exclude_internal:  # 清理 Pydantic 特有字段
            if field in all_attrs: del all_attrs[field]
        # 清理空的结构性字段
        structure_fields = {'location', 'current_place', 'has_items', 'contents', 'exits'}
        keys_to_remove = set()
        for field in structure_fields:
            # 检查核心字段和动态属性
            if field in all_attrs and not all_attrs[field]: keys_to_remove.add(field)
        for key in keys_to_remove: del all_attrs[key]
        return all_attrs

    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any]) -> bool:
        """修改实体属性，处理操作符。op 保证不为 None。返回 True 如果修改了位置属性"""
        op, value_to_process = opAndValue
        location_updated = False
        logging.debug(
            f"实体 '{self.entity_id}': modify_attribute: Key='{key}', Op='{op}', Value='{repr(value_to_process)}'")
        try:
            if op == '=':
                new_value = value_to_process
                self.set_attribute(key, value_to_process)
            elif op == '-':
                new_value = "<deleted>"
                self.delete_attribute(key)  # 忽略返回值？
            else:  # 处理 +=, -=, +, - (这里的 - 是减法或移除)
                current_value = self.get_attribute(key)
                new_value = None  # 获取当前值
                if op == '+=' or op == '+':
                    if isinstance(current_value, (int, float)) and isinstance(value_to_process, (int, float)):
                        new_value = current_value + value_to_process
                    elif isinstance(current_value, str) and isinstance(value_to_process, str):
                        new_value = current_value + value_to_process
                    elif isinstance(current_value, list):
                        if isinstance(value_to_process, list) and op == '+':
                            new_value = current_value + value_to_process
                        else:
                            new_value = list(current_value)
                            new_value.append(value_to_process)
                    elif isinstance(current_value, dict) and isinstance(value_to_process, dict):
                        new_value = current_value.copy()
                        new_value.update(value_to_process)
                    else:
                        raise TypeError(
                            f"属性 '{key}' (类型 {type(current_value)}) 不支持操作 '{op}' 或值类型 '{type(value_to_process)}'")
                    self.set_attribute(key, new_value)
                elif op == '-=' or op == '-':  # 减法或移除
                    if isinstance(current_value, (int, float)) and isinstance(value_to_process, (int, float)):
                        new_value = current_value - value_to_process
                    elif isinstance(current_value, list):
                        new_value = list(current_value)
                        try:
                            new_value.remove(value_to_process)
                        except ValueError:
                            logging.warning(f"Modify({self.entity_id}): 尝试从列表 '{key}' 移除不存在元素 '{value_to_process}'")
                            new_value = current_value
                    elif isinstance(current_value, dict) and isinstance(value_to_process, str):
                        new_value = current_value.copy()
                        if value_to_process in new_value:
                            del new_value[value_to_process]
                        else:
                            logging.warning(f"Modify({self.entity_id}): 尝试从字典 '{key}' 移除不存在键 '{value_to_process}'")
                            new_value = current_value
                    else:
                        raise TypeError(
                            f"属性 '{key}' (类型 {type(current_value)}) 不支持操作 '{op}' 或值类型 '{type(value_to_process)}'")
                    self.set_attribute(key, new_value)
            logging.debug(f"属性 '{key}' 已更新为: {repr(new_value)}")
            if key == "location" or key == "current_place":
                location_updated = True
        except AttributeError:  # get_attribute 失败 (通常属性不存在)
            if op == '+=' or op == '=':  # 如果是 += 或 =，视为创建属性
                logging.debug(f"属性 '{key}' 不存在，操作 '{op}' 视为设置。")
                self.set_attribute(key, value_to_process)
                if key == "location" or key == "current_place":
                    location_updated = True
            else:
                logging.debug(f"属性 '{key}' 不存在，操作 '{op}' 无效。")
                return False
        except (TypeError, ValueError) as e:
            logging.error(f"Modify({self.entity_id}): 处理属性 '{key}' 失败: {e}", exc_info=True)
            raise e
        except Exception as e:
            logging.error(f"Modify({self.entity_id}): 处理属性 '{key}' 时发生未知错误: {e}", exc_info=True)
            raise e
        return location_updated


# --- 子类定义 (只保留结构性核心字段) ---
class Item(BaseEntity):
    entity_type: Literal["Item"] = "Item"
    quantity: int = Field(1, ge=0)
    location: Optional[str] = Field(None)


class Character(BaseEntity):
    entity_type: Literal["Character"] = "Character"
    current_place: Optional[str] = Field(None)
    has_items: List[str] = Field(default_factory=list)


class Place(BaseEntity):
    entity_type: Literal["Place"] = "Place"
    contents: List[str] = Field(default_factory=list)
    exits: Dict[str, str] = Field(default_factory=dict)


AnyEntity = Union[Item, Character, Place]


# --- WorldState (保持不变) ---
class WorldState(BaseModel):
    items: Dict[str, Item] = Field(default_factory=dict)
    characters: Dict[str, Character] = Field(default_factory=dict)
    places: Dict[str, Place] = Field(default_factory=dict)
    model_config = {'validate_assignment': True}

    def find_entity(self, entity_id: str, include_destroyed: bool = False) -> Optional[AnyEntity]:
        entity = self.items.get(entity_id) or self.characters.get(entity_id) or self.places.get(entity_id)
        if entity and (not entity.is_destroyed or include_destroyed): return entity
        return None

    def find_entity_by_name(self, name: str, entity_type: Optional[Literal["Item", "Character", "Place"]] = None,
                            include_destroyed: bool = False) -> Optional[AnyEntity]:
        search_dicts: List[Dict[str, AnyEntity]] = []
        if entity_type == "Item":
            search_dicts.append(self.items)
        elif entity_type == "Character":
            search_dicts.append(self.characters)
        elif entity_type == "Place":
            search_dicts.append(self.places)
        else:
            search_dicts.extend([self.items, self.characters, self.places])
        for entity_dict in search_dicts:
            for entity in entity_dict.values():
                # 使用 entity.name property
                if entity.name == name and (not entity.is_destroyed or include_destroyed): return entity
        return None

    def get_entity_dict(self, entity_type: Literal["Item", "Character", "Place"]) -> Dict[str, AnyEntity]:
        if entity_type == "Item":
            return self.items
        elif entity_type == "Character":
            return self.characters
        elif entity_type == "Place":
            return self.places
        else:
            raise ValueError(f"未知的实体类型: {entity_type}")

    def add_entity(self, entity: AnyEntity):
        entity_dict = self.get_entity_dict(entity.entity_type)
        if entity.entity_id in entity_dict and not entity_dict[entity.entity_id].is_destroyed: logging.warning(
            f"覆盖已存在且未销毁的实体: {entity.entity_type} ID='{entity.entity_id}'")
        entity_dict[entity.entity_id] = entity
