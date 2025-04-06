# game_state.py
import logging
import re
from typing import List, Optional, Dict, Any, Literal, Union, Set, Tuple, Type

import yaml  # 导入 YAML 库
from pydantic import BaseModel, Field, ValidationError


# --- 核心数据模型 ---

class BaseEntity(BaseModel):
    """
    所有游戏世界实体的基类 (最终重构版)。
    - 属性访问和修改逻辑完全封装。
    """
    entity_id: str = Field(...)
    name: str = Field(...)
    status: Dict[str, Any] = Field(default_factory=dict)
    is_destroyed: bool = Field(False)
    entity_type: Literal["Item", "Character", "Place"] = Field(...)

    @property
    def core_fields(self) -> Set[str]:
        return set(self.model_fields.keys()) - {'status'}

    # --- 属性访问方法 (不变) ---
    def get_attribute(self, key: str) -> Any:
        if key in self.core_fields:
            return getattr(self, key)
        elif key in self.status:
            return self.status[key]
        else:
            raise AttributeError(f"实体 '{self.entity_id}' 上找不到属性 '{key}'")

    def has_attribute(self, key: str) -> bool:
        return key in self.core_fields or key in self.status

    def delete_attribute(self, key: str) -> bool:
        if key in self.core_fields:
            logging.warning(f"不能删除核心属性 '{key}' (实体: {self.entity_id})")
            return False
        elif key in self.status:
            del self.status[key]
            return True
        else:
            return False  # 不存在，无需删除

    def set_attribute(self, key: str, value: Any):
        # (set_attribute 逻辑保持不变，包含类型转换和验证尝试)
        if key in self.core_fields:
            field_info = self.model_fields[key]
            expected_type = field_info.annotation
            converted_value = value
            try:
                setattr(self, key, converted_value)
            except ValidationError as e:
                # 尝试类型转换
                if expected_type and not isinstance(value, expected_type):
                    try:
                        if getattr(expected_type, "__origin__", None) is Union and type(None) in getattr(expected_type,
                                                                                                         "__args__",
                                                                                                         ()):
                            if value is None:
                                converted_value = None
                            else:
                                non_none_type = next(
                                    (t for t in getattr(expected_type, "__args__", ()) if t is not type(None)), None)
                                if non_none_type and not isinstance(value,
                                                                    non_none_type): converted_value = non_none_type(
                                    value)
                        elif isinstance(expected_type, type):
                            converted_value = expected_type(value)
                        setattr(self, key, converted_value)
                    except (TypeError, ValueError, ValidationError) as conversion_error:
                        logging.error(f"设置核心属性 '{key}' (值: {repr(value)}) 时转换或验证失败: {conversion_error}")
                        raise conversion_error
                    except Exception as final_set_error:
                        logging.error(
                            f"设置核心属性 '{key}' (转换后: {repr(converted_value)}) 时发生意外错误: {final_set_error}",
                            exc_info=True)
                        raise final_set_error
                else:
                    logging.error(f"设置核心属性 '{key}' (值: {repr(value)}) 时验证失败: {e}")
                    raise e
            except Exception as e:
                logging.error(f"设置核心属性 '{key}' 时发生未知错误: {e}", exc_info=True)
                raise e
        else:
            self.status[key] = value

    def get_all_attributes(self, exclude_internal: Set[str] = {'status', 'is_destroyed', 'entity_type', 'entity_id'}) -> \
            Dict[str, Any]:
        # (get_all_attributes 逻辑保持不变)
        all_attrs = {}
        for key in self.core_fields:
            if key not in exclude_internal:
                value = getattr(self, key)
                all_attrs[key] = list(value) if isinstance(value, list) else value
        for key, value in self.status.items():
            if key not in exclude_internal and key not in all_attrs:
                all_attrs[key] = list(value) if isinstance(value, list) else value
        structure_fields = {'location', 'current_place', 'has_items', 'contents'}
        keys_to_remove = {field for field in structure_fields if field in all_attrs and not all_attrs[field]}
        for key in keys_to_remove: del all_attrs[key]
        return all_attrs

    def modify_attribute(self, key: str, opAndValue: Tuple[Optional[str], Any]) -> bool:
        """
        修改实体属性，内部处理 +=, -=, +, - 操作符。
        返回 True 如果修改了位置属性 (location/current_place)，否则 False。
        """
        op, value_to_process = opAndValue
        location_updated = False
        current_value: Any = None
        attr_exists = self.has_attribute(key)
        if attr_exists:
            try:
                current_value = self.get_attribute(key)
            except AttributeError:
                attr_exists = False  # 防御性编程

        new_value: Any = None
        operation_performed = False
        skip_set_value = False  # 用于标记是否需要跳过最后的 set_attribute

        try:
            # --- 处理带操作符的情况 (+, -, +=, -=) ---
            if op:
                if op == '+=' or op == '+':
                    # 尝试数值加法
                    if isinstance(current_value, (int, float)) and isinstance(value_to_process, (int, float)):
                        new_value = current_value + value_to_process
                    # 尝试字符串拼接
                    elif isinstance(current_value, str) and isinstance(value_to_process, str):
                        new_value = current_value + value_to_process
                    # 尝试列表追加
                    elif isinstance(current_value, list):
                        new_value = list(current_value) + [value_to_process]  # 创建新列表
                    # 如果属性不存在，+= 视为设置
                    elif not attr_exists:
                        new_value = value_to_process
                        if isinstance(value_to_process, list): new_value = list(value_to_process)  # 创建列表副本
                    else:  # 类型不匹配或不支持
                        logging.warning(
                            f"Modify({self.entity_id}): 属性 '{key}' (类型 {type(current_value)}) 不支持操作符 '{op}' 或值类型不匹配。")
                        skip_set_value = True  # 不设置值
                    operation_performed = True

                elif op == '-=' or op == '-':
                    # 尝试数值减法
                    if isinstance(current_value, (int, float)) and isinstance(value_to_process, (int, float)):
                        new_value = current_value - value_to_process
                    # 尝试列表移除
                    elif isinstance(current_value, list):
                        new_value = list(current_value)  # 创建副本
                        try:
                            new_value.remove(value_to_process)
                        except ValueError:
                            logging.warning(
                                f"Modify({self.entity_id}): 列表 '{key}' 中无元素 '{value_to_process}' 可移除")
                    # 尝试属性删除 (如果属性存在)
                    elif attr_exists:
                        if self.delete_attribute(key):
                            skip_set_value = True  # 删除成功，跳过设置
                        else:
                            logging.warning(f"Modify({self.entity_id}): 无法使用 '-=' 删除属性 '{key}' (核心属性?)")
                    # 如果属性不存在，-= 无操作
                    else:
                        skip_set_value = True  # 属性不存在，跳过设置
                    operation_performed = True

            # --- 处理无操作符的情况 (替换/设置) ---
            else:  # op is None
                new_value = value_to_process
                operation_performed = True

            # --- 应用新值 ---
            if operation_performed and not skip_set_value:
                self.set_attribute(key, new_value)
                # 检查是否修改了位置属性
                if key == "location" or key == "current_place":
                    location_updated = True

            return location_updated  # 返回位置是否更新的标记

        except AttributeError:
            logging.warning(f"Modify({self.entity_id}): 尝试访问不存在的属性 '{key}' (在操作中)")
            return False
        except Exception as e:
            logging.error(f"Modify({self.entity_id}): 处理属性 '{key}' (值和操作符: {opAndValue}) 时发生错误: {e}",
                          exc_info=True)
            raise e


class Item(BaseEntity):
    """代表游戏中的物品"""
    entity_type: Literal["Item"] = "Item"
    quantity: int = Field(1, description="物品数量，用于堆叠", ge=1)
    location: Optional[str] = Field(None, description="物品所在位置的 entity_id (可以是 Place 或 Character 的 ID)")


class Character(BaseEntity):
    """代表游戏中的角色"""
    entity_type: Literal["Character"] = "Character"
    current_place: Optional[str] = Field(None, description="角色当前所在地的 entity_id (必须是 Place 的 ID)")
    has_items: List[str] = Field(default_factory=list, description="角色持有的物品 entity_id 列表")


class Place(BaseEntity):
    """代表游戏中的地点"""
    entity_type: Literal["Place"] = "Place"
    contents: List[str] = Field(default_factory=list,
                                description="地点包含的实体 entity_id 列表 (可以是 Character 或 Item 的 ID)")


AnyEntity = Union[Item, Character, Place]  # 联合类型方便类型提示


class WorldState(BaseModel):
    """存储所有游戏实体的容器"""
    items: Dict[str, Item] = Field(default_factory=dict)
    characters: Dict[str, Character] = Field(default_factory=dict)
    places: Dict[str, Place] = Field(default_factory=dict)
    user_focus: List[str] = Field(default_factory=list, description="用户聚焦的地点/角色")


# --- 游戏状态管理器 ---

class GameState:
    """管理游戏世界状态"""

    def __init__(self):
        self.world = WorldState()
        logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

    def _get_entity_dict(self, entity_type: Literal["Item", "Character", "Place"]) -> Dict[str, AnyEntity]:
        """根据类型获取对应的实体字典"""
        if entity_type == "Item":
            return self.world.items
        elif entity_type == "Character":
            return self.world.characters
        elif entity_type == "Place":
            return self.world.places
        else:
            # 在原型阶段，如果类型错误直接崩溃
            raise ValueError(f"未知的实体类型: {entity_type}")

    def find_entity(self, entity_id: str, include_destroyed: bool = False) -> Optional[AnyEntity]:
        """通过 ID 查找任何类型的实体"""
        for entity_dict in [self.world.items, self.world.characters, self.world.places]:
            if entity_id in entity_dict:
                entity = entity_dict[entity_id]
                if not entity.is_destroyed or include_destroyed:
                    return entity
        return None

    def find_entity_by_name(self, name: str, entity_type: Optional[Literal["Item", "Character", "Place"]] = None,
                            include_destroyed: bool = False) -> Optional[AnyEntity]:
        """按名称查找实体（效率较低，主要用于 AI 指令容错）"""
        search_dicts = []
        if entity_type:
            search_dicts.append(self._get_entity_dict(entity_type))
        else:
            search_dicts.extend([self.world.items, self.world.characters, self.world.places])

        for entity_dict in search_dicts:
            for entity in entity_dict.values():
                if entity.name == name and (not entity.is_destroyed or include_destroyed):
                    return entity
        return None

    def _add_entity(self, entity: AnyEntity):
        """内部添加实体方法"""
        entity_dict = self._get_entity_dict(entity.entity_type)
        if entity.entity_id in entity_dict:
            # ID 冲突警告，但仍会覆盖（根据原型原则简化处理）
            logging.warning(f"警告: 实体 ID '{entity.entity_id}' 已存在，将被覆盖。")
        entity_dict[entity.entity_id] = entity
        logging.info(f"创建了实体: {entity.entity_type} ID='{entity.entity_id}', Name='{entity.name}'")

    def _remove_from_container(self, entity_id: str, container_id: Optional[str]):
        """从旧容器的列表中移除实体 ID"""
        if not container_id:
            return
        container = self.find_entity(container_id, include_destroyed=True)  # 允许查找已删除的容器以清理关系
        if not container:
            logging.warning(f"尝试从不存在的容器 '{container_id}' 移除 '{entity_id}'")
            return

        if isinstance(container, Character) and entity_id in container.has_items:
            container.has_items.remove(entity_id)
            logging.debug(f"从角色 '{container_id}' 移除物品 '{entity_id}'")
        elif isinstance(container, Place) and entity_id in container.contents:
            container.contents.remove(entity_id)
            logging.debug(f"从地点 '{container_id}' 移除实体 '{entity_id}'")
        # else: 物品不能包含其他东西，无需处理

    def _add_to_container(self, entity_id: str, container_id: Optional[str]):
        """将实体 ID 添加到新容器的列表中 (重要: 容器必须已存在)"""
        if not container_id: return
        container = self.find_entity(container_id)
        # 移除了这里的自动创建逻辑，转移到 _ensure_entity_exists
        if not container:
            # 这是内部逻辑错误，调用者应确保容器存在
            raise ValueError(f"_add_to_container 内部错误: 目标容器 '{container_id}' 不存在或已被销毁。")

        entity_to_add = self.find_entity(entity_id)
        if not entity_to_add:
            raise ValueError(f"无法添加不存在或已销毁的实体 '{entity_id}' 到容器 '{container_id}'。")

        # 类型检查和添加逻辑不变
        if isinstance(container, Character):
            if entity_to_add.entity_type != "Item":
                raise TypeError(
                    f"不能将非物品实体 '{entity_id}' ({entity_to_add.entity_type}) 添加到角色 '{container_id}'。")
            if entity_id not in container.has_items:
                container.has_items.append(entity_id)
                logging.debug(f"物品 '{entity_id}' 添加到角色 '{container_id}'")
        elif isinstance(container, Place):
            if entity_id not in container.contents:
                container.contents.append(entity_id)
                logging.debug(f"实体 '{entity_id}' ({entity_to_add.entity_type}) 添加到地点 '{container_id}'")
        else:
            raise TypeError(f"目标实体 '{container_id}' 类型为 Item，不能作为容器。")

    def _create_placeholder_entity(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str, context: str = "未知指令") -> AnyEntity:
        """内部方法，用于创建缺失的占位符实体，名称带有 Warning: 前缀"""
        # 标准化前缀，方便查找
        warning_prefix = "Warning: MissingName"
        # 包含类型、ID 和创建上下文，方便 AI 理解
        placeholder_name = f"{warning_prefix} {entity_type} [OriginID:{entity_id}] (AutoCreated by: {context})"

        logging.info(f"实体 '{entity_id}' 不存在，自动创建占位符 ({entity_type})，名称: '{placeholder_name}'")

        model_class: Type[BaseEntity]
        if entity_type == "Item": model_class = Item
        elif entity_type == "Character": model_class = Character
        elif entity_type == "Place": model_class = Place
        else: raise ValueError(f"无效的实体类型 '{entity_type}' 无法创建占位符")

        try:
            new_entity = model_class(entity_id=entity_id, name=placeholder_name, entity_type=entity_type)
            self._add_entity(new_entity)
            return new_entity
        except Exception as e:
            logging.error(f"自动创建占位符实体 '{entity_id}' ({entity_type}) 失败: {e}", exc_info=True)
            print(f"[系统严重错误] 自动创建占位符实体 '{entity_id}' 失败: {e}")
            raise e

    # --- 新增：获取所有“问题”实体 ---
    def get_problematic_entities(self) -> List[Dict[str, Any]]:
        """
        查找所有由系统自动创建且尚未被 AI '修复' (即名称仍带有特定前缀) 的实体。
        """
        problematic = []
        warning_prefix = "Warning: Missing" # 与 _create_placeholder_entity 中使用的前缀一致
        entity_dicts = [self.world.items, self.world.characters, self.world.places]

        # logging.debug(f"开始查找名称以 '{warning_prefix}' 开头的问题实体...")
        count = 0
        for entity_dict in entity_dicts:
            for entity_id, entity in entity_dict.items():
                # 检查是否未被销毁，并且名称是否以特定前缀开头
                if not entity.is_destroyed and entity.name.startswith(warning_prefix):
                    problematic.append({
                        "entity_id": entity.entity_id,
                        "entity_type": entity.entity_type,
                        "current_name": entity.name
                        # 可以考虑添加其他信息，如创建时间或上下文，但目前保持简洁
                    })
                    count += 1
                    logging.debug(f"  找到问题实体: {entity.entity_type} '{entity_id}' - '{entity.name}'")

        # logging.debug(f"共找到 {count} 个问题实体。")
        return problematic

    # --- 新增：确保实体存在，否则尝试创建占位符 ---
    def _ensure_entity_exists(self,
                              entity_spec: Union[str, Tuple[str, str], None],
                              expected_container_type: Optional[
                                  Union[Type[Character], Type[Place], Tuple[Type[Character], Type[Place]]]] = None,
                              context: str = "未知操作") -> Optional[str]:
        """
        检查实体是否存在。如果不存在，根据 entity_spec 尝试创建占位符。
        返回确认存在（或已创建）的实体 ID，如果无法处理则返回 None (或在原型中崩溃)。

        Args:
            entity_spec: 实体标识符。
                         - str: 纯 entity_id (旧格式，不推荐，无法自动创建)。
                         - Tuple[str, str]: (entity_type, entity_id) (新格式，推荐)。
                         - None: 表示没有指定目标。
            expected_container_type: 期望的实体类型 (例如 Place 或 (Character, Place))，用于验证。
            context: 用于日志记录的操作上下文。

        Returns:
            Optional[str]: 最终有效的目标实体 ID，如果无法找到或创建则返回 None (或崩溃)。
        """
        if entity_spec is None:
            return None  # 没有指定目标

        entity_id: str = ""
        provided_type: Optional[Literal["Item", "Character", "Place"]] = None

        if isinstance(entity_spec, tuple) and len(entity_spec) == 2:
            provided_type, entity_id = entity_spec
            if provided_type not in ["Item", "Character", "Place"]:
                # 解析器应该已经处理了，但再次检查
                logging.error(f"{context}: 无效的实体类型 '{provided_type}' 在规范 '{entity_spec}' 中。")
                raise ValueError(f"无效的实体类型 '{provided_type}'")  # 崩溃
            logging.debug(f"{context}: 检查实体存在性，需要类型 '{provided_type}' ID '{entity_id}'。")
        elif isinstance(entity_spec, str):
            entity_id = entity_spec
            # 旧格式，没有提供类型
            logging.warning(f"{context}: 使用了旧的实体 ID 格式 '{entity_id}' (无类型)。如果实体不存在，将无法自动创建。")
        else:
            logging.error(f"{context}: 无法识别的实体规范格式 '{repr(entity_spec)}'。")
            raise TypeError(f"无效的实体规范格式: {repr(entity_spec)}")  # 崩溃

        if not entity_id:  # 防御性检查
            logging.error(f"{context}: 从规范 '{repr(entity_spec)}' 中未能提取有效的 entity_id。")
            raise ValueError("无法提取有效的 entity_id")  # 崩溃

        # 查找实体
        entity = self.find_entity(entity_id)

        if entity:
            # 实体存在，检查类型是否符合预期 (如果提供了预期类型)
            if expected_container_type:
                if not isinstance(entity, expected_container_type):
                    # 类型不匹配，AI 可能搞错了目标类型，但我们仍然返回 ID
                    # 在原型阶段，我们信任 AI 的意图，让后续操作去处理可能的类型错误
                    logging.warning(
                        f"{context}: 找到实体 '{entity_id}'，但其类型 ({entity.entity_type}) 与期望的容器类型不完全匹配。继续使用该 ID。")
            # 检查提供的类型是否与找到的实体类型一致 (如果提供了类型)
            if provided_type and entity.entity_type != provided_type:
                logging.warning(
                    f"{context}: AI 提供的类型 '{provided_type}' 与找到的实体 '{entity_id}' 的实际类型 '{entity.entity_type}' 不符。将使用找到的实体。")

            logging.debug(f"{context}: 确认实体 '{entity_id}' ({entity.entity_type}) 存在。")
            return entity_id  # 返回存在的实体 ID
        else:
            # 实体不存在
            if provided_type:
                # 新格式，可以尝试创建占位符
                logging.info(
                    f"{context}: 目标实体 '{entity_id}' 不存在，尝试根据提供的类型 '{provided_type}' 自动创建占位符。")
                try:
                    # 调用内部创建方法
                    placeholder = self._create_placeholder_entity(provided_type, entity_id, context)
                    # 再次验证类型（如果需要），占位符类型应该匹配
                    if expected_container_type and not isinstance(placeholder, expected_container_type):
                        logging.warning(
                            f"{context}: 创建的占位符 '{entity_id}' 类型 ({placeholder.entity_type}) 可能不满足严格的预期容器类型。")
                    return placeholder.entity_id  # 返回新创建的占位符 ID
                except Exception as e:
                    # 创建占位符失败，按原型原则崩溃
                    logging.error(f"{context}: 自动创建实体 '{entity_id}' ({provided_type}) 失败。错误: {e}",
                                  exc_info=True)
                    raise e  # 崩溃
            else:
                # 旧格式，无法创建，失败
                logging.error(f"{context}: 目标实体 '{entity_id}' 不存在，且未提供类型信息，无法自动创建。")
                # 按原型原则，让它崩溃，因为指令无法完成
                raise ValueError(f"目标实体 '{entity_id}' 不存在且无法自动创建")

        # --- 指令执行方法 (更新以使用 _ensure_entity_exists) ---

    def execute_create(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str,
                       params: Dict[str, Any]):
        """执行 Create 指令 (更新：检查 location/current_place 的存在性)"""
        existing_entity = self.find_entity(entity_id, include_destroyed=True)
        if existing_entity and not existing_entity.is_destroyed:
            logging.warning(f"Create 警告: 实体 ID '{entity_id}' 已存在且未销毁，将被覆盖。")
            # 可以在这里选择是更新还是失败，目前行为是覆盖

        # 物品堆叠检查 (逻辑不变，但 location 可能需要检查)
        if entity_type == "Item" and params.get("quantity", 1) > 0:
            location_spec = params.get("location")  # 可能为 (Type, ID), ID, 或 None
            item_name = params.get("name")
            if location_spec and item_name:
                # 确保容器存在，但不强制类型检查，因为可能堆叠在角色或地点
                container_id = self._ensure_entity_exists(location_spec,
                                                          context=f"Create Item '{entity_id}' ")
                if container_id:
                    container = self.find_entity(container_id)  # 此时容器保证存在
                    if container:  # 再次检查以防万一
                        c_list_attr = None
                        if isinstance(container, Character):
                            c_list_attr = "has_items"
                        elif isinstance(container, Place):
                            c_list_attr = "contents"

                        if c_list_attr:
                            c_list = getattr(container, c_list_attr, [])
                            for existing_item_id in c_list:
                                item = self.find_entity(existing_item_id)
                                if item and isinstance(item,
                                                       Item) and item.name == item_name and not item.is_destroyed:
                                    qty_to_add = params.get("quantity", 1)
                                    logging.info(
                                        f"物品堆叠: 在 '{container_id}' 中的 '{item_name}' ({existing_item_id}) 增加数量 {qty_to_add}")
                                    # 使用 execute_modify 来处理数量增加
                                    self.execute_modify("Item", existing_item_id, {"quantity": ('+=', qty_to_add)})
                                    # 堆叠成功，不再创建新物品
                                    return
        # --- 创建实体本身 (逻辑不变) ---
        model_class: Type[BaseEntity]
        if entity_type == "Item":
            model_class = Item
        elif entity_type == "Character":
            model_class = Character
        elif entity_type == "Place":
            model_class = Place
        else:
            raise ValueError(f"无效类型: {entity_type}")

        # 准备初始化参数，移除 location/current_place，后面单独处理
        init_params = {"entity_id": entity_id, "entity_type": entity_type}
        if 'name' in params:
            init_params['name'] = params['name']
        else:
            raise ValueError(f"Create 指令 ({entity_id}) 缺少 'name' 参数")  # name 是必须的

        try:
            new_entity = model_class(**init_params)
        except Exception as e:
            logging.error(f"创建 '{entity_id}' ({entity_type}) 初始化失败: {e}", exc_info=True)
            raise

        # --- 设置其他属性 (除了 location/current_place) ---
        initial_location_spec: Union[str, Tuple[str, str], None] = None
        location_key = 'location' if entity_type == 'Item' else (
            'current_place' if entity_type == 'Character' else None)

        for key, value in params.items():
            if key in init_params or key == location_key:  # 跳过已处理的或待处理的位置属性
                continue
            try:
                # value 可能是 (op, val) 或普通值，Create 不应有 op
                actual_value = value
                op = None
                if isinstance(value, tuple) and len(value) == 2 and value[0] in ('+=', '-=', '+', '-'):
                    op, actual_value = value
                    logging.warning(
                        f"Create ({entity_id}): 属性 '{key}' 含无效操作符 '{op}'，将直接使用值 '{repr(actual_value)}'")

                # actual_value 也可能是 (Type, ID)
                # 在创建时，如果属性值是 (Type, ID)，我们应该检查目标是否存在吗？
                # 比如 @Create Item A (owner="Character:B")
                # 暂时不检查非 location/current_place 的 (Type, ID) 引用，直接设置
                new_entity.set_attribute(key, actual_value)

            except Exception as e:
                logging.warning(f"创建 '{entity_id}' 时设置属性 '{key}' (值: {repr(value)}) 失败: {e}")
                # 原型阶段可以忽略这个属性的设置，或者让它崩溃
                # raise e # 取消注释则单个属性失败导致创建失败

        # 记录初始位置规范 (可能是 (Type, ID), ID, 或 None)
        if location_key and location_key in params:
            initial_location_spec = params[location_key]
            # 如果有操作符，移除操作符
            if isinstance(initial_location_spec, tuple) and len(initial_location_spec) == 2 and \
                    initial_location_spec[0] in ('+=', '-=', '+', '-'):
                op, initial_location_spec = initial_location_spec
                logging.warning(f"Create ({entity_id}): 位置属性 '{location_key}' 含无效操作符 '{op}'，已忽略。")

        # --- 添加实体到世界状态 ---
        self._add_entity(new_entity)

        # --- 处理位置 (确保目标存在并添加到容器) ---
        if location_key and initial_location_spec:
            context = f"Create '{entity_id}' ({entity_type}) 设置位置"
            expected_type: Optional[Union[Type, Tuple[Type, ...]]] = None
            if entity_type == 'Item':
                expected_type = (Character, Place)  # 物品可在角色或地点
            elif entity_type == 'Character':
                expected_type = Place  # 角色只能在地点

            # 确保目标容器/地点存在 (或自动创建)
            final_location_id = self._ensure_entity_exists(initial_location_spec, expected_type, context)

            if final_location_id:
                try:
                    # 设置实体的位置属性
                    new_entity.set_attribute(location_key, final_location_id)
                    # 将实体添加到目标容器的列表
                    self._add_to_container(new_entity.entity_id, final_location_id)
                    logging.info(f"实体 '{entity_id}' 已放置在 '{final_location_id}'")
                except Exception as e:
                    logging.error(f"创建 '{entity_id}' 后设置位置 '{final_location_id}' 或加入容器失败: {e}",
                                  exc_info=True)
                    # 原型阶段让其崩溃
                    raise e
            else:
                # _ensure_entity_exists 在失败时应该已经崩溃了，这里是防御性代码
                logging.error(f"{context}: 未能确定或创建有效的位置 ID，实体 '{entity_id}' 未设置位置。")
                # 或者在这里也崩溃？
                raise ValueError(f"未能为 '{entity_id}' 确定有效位置")

        # 特殊：如果是 Place 被创建，自动聚焦 (按之前逻辑)
        if isinstance(new_entity, Place) and entity_id not in self.world.user_focus:
            logging.info(f"新地点 '{entity_id}' 已创建，自动添加到用户焦点。")
            self.add_focus(entity_id)

    def execute_modify(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str,
                       updates: Dict[str, Any]):
        """执行 Modify 指令 (更新：检查 location/current_place 变更)"""
        entity = self.find_entity(entity_id)
        if not entity: raise ValueError(f"Modify: 找不到实体 '{entity_id}'")
        if entity.entity_type != entity_type: raise TypeError(f"Modify: 实体类型不匹配")

        logging.info(f"开始修改实体 '{entity_id}' ({entity.name})。 更新内容: {updates}")

        old_location_or_place: Optional[str] = None
        location_key_being_modified: Optional[str] = None  # 记录是否正在修改位置属性
        new_location_spec: Any = None  # 记录新的位置规范 (可能是 (Type,ID), ID, None)

        try:  # 获取修改前的旧位置
            if isinstance(entity, Item):
                old_location_or_place = entity.location
            elif isinstance(entity, Character):
                old_location_or_place = entity.current_place
        except AttributeError:
            pass

        # --- 循环处理每个更新 ---
        for key, raw_update_value in updates.items():
            op: Optional[str] = None
            value_to_process: Any = None

            # 解析 op 和 value (同之前修正后的逻辑)
            if isinstance(raw_update_value, tuple) and len(raw_update_value) == 2 and raw_update_value[0] in (
                    '+=', '-=', '+', '-'):
                op, value_to_process = raw_update_value
            else:
                op = None
                value_to_process = raw_update_value

            # --- 特殊处理位置属性的修改 ---
            is_location_key = (key == 'location' and isinstance(entity, Item)) or \
                              (key == 'current_place' and isinstance(entity, Character))

            if is_location_key:
                location_key_being_modified = key
                new_location_spec = value_to_process  # 记录新的位置规范
                # 位置属性的修改不应该有 +=, -=, + 操作符，只允许 = (None op) 或 - (表示移除)
                if op and op != '-':
                    logging.warning(
                        f"Modify ({entity_id}): 不支持对位置属性 '{key}' 使用操作符 '{op}'。将尝试直接赋值。")
                    op = None  # 强制视为赋值
                if op == '-':  # 如果是 op 是 '-'，表示尝试移除位置
                    logging.debug(f"  处理位置移除: {key} = None")
                    new_location_spec = None  # 目标是移除位置
                    value_to_process = None
                    op = None  # 转为赋值 None

                # **在这里不直接调用 entity.modify_attribute**
                # 我们需要先确保新位置存在 (如果不是 None)，然后再统一处理
                logging.debug(f"  计划修改位置属性 '{key}' 为 '{repr(new_location_spec)}'。")
                continue  # 跳过对此 key 的 modify_attribute 调用，稍后处理

            # --- 处理非位置属性 ---
            try:
                # value_to_process 可能是 (Type, ID)，modify_attribute 会处理
                # modify_attribute 返回的 location_updated 在这里不再重要
                entity.modify_attribute(key, (op, value_to_process))
                logging.debug(f"  已修改非位置属性: {key} {op or '='} {repr(value_to_process)}")
            except Exception as e:
                logging.error(
                    f"执行 Modify 操作失败: 实体='{entity_id}', Key='{key}', Op='{op}', Value='{repr(value_to_process)}'. 错误: {e}",
                    exc_info=True)
                raise e  # 崩溃

        # --- 统一处理位置变更 ---
        if location_key_being_modified:
            context = f"Modify '{entity_id}' 更新位置 '{location_key_being_modified}'"
            final_new_location_id: Optional[str] = None  # 最终确定的新位置 ID

            if new_location_spec is not None:  # 如果目标不是移除位置
                expected_type: Optional[Union[Type, Tuple[Type, ...]]] = None
                if isinstance(entity, Item):
                    expected_type = (Character, Place)
                elif isinstance(entity, Character):
                    expected_type = Place

                # 确保新位置存在 (或自动创建)
                final_new_location_id = self._ensure_entity_exists(new_location_spec, expected_type, context)
                # 如果 _ensure_entity_exists 失败会崩溃

            # --- 应用位置变更 ---
            if old_location_or_place != final_new_location_id:
                logging.info(
                    f"位置变更检测: '{entity_id}' 从 '{old_location_or_place}' 移动到 '{final_new_location_id}'")
                try:
                    # 1. 更新实体自身的位置属性
                    entity.set_attribute(location_key_being_modified, final_new_location_id)
                    # 2. 更新旧容器和新容器的列表
                    self._update_container_lists(entity_id, old_location_or_place,
                                                 new_container_id=final_new_location_id)
                    logging.debug(f"实体 '{entity_id}' 位置属性和容器列表已更新。")
                except Exception as e:
                    logging.error(
                        f"应用位置变更从 '{old_location_or_place}' 到 '{final_new_location_id}' 失败: {e}",
                        exc_info=True)
                    raise e  # 崩溃
            else:
                logging.debug(
                    f"实体 '{entity_id}' 的位置属性 '{location_key_being_modified}' 被指令修改，但最终位置 ID ('{final_new_location_id}') 与旧值相同，无需更新容器列表。")

        logging.info(f"实体 '{entity_id}' 修改完成。")

    def execute_destroy(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str):
        """执行 Destroy 指令 (基本不变，但需要更健壮地处理容器移除)"""
        entity = self.find_entity(entity_id, include_destroyed=True)
        if not entity:
            logging.warning(f"Destroy 指令警告: 找不到实体 ID '{entity_id}'")
            return
        if entity.is_destroyed:
            logging.info(f"实体 '{entity_id}' 已被销毁，无需操作。")
            return
        if entity.entity_type != entity_type:
            raise TypeError(
                f"Destroy 指令错误: 实体 '{entity_id}' 类型为 {entity.entity_type}, 但指令指定为 {entity_type}")

        logging.info(f"销毁实体: {entity.entity_type} ID='{entity_id}', Name='{entity.name}'")
        entity.is_destroyed = True

        # 从当前容器中移除
        container_id: Optional[str] = None
        items_to_handle: List[str] = []  # 角色/地点销毁时需要处理的物品
        contents_to_handle: List[str] = []  # 地点销毁时需要处理的内容

        if isinstance(entity, Item):
            container_id = entity.location
            entity.location = None  # 清除位置信息 (标记为无位置)
        elif isinstance(entity, Character):
            container_id = entity.current_place
            entity.current_place = None
            items_to_handle = list(entity.has_items)  # 复制列表
            entity.has_items = []  # 清空持有列表
        elif isinstance(entity, Place):
            # 地点销毁时，内部的实体需要处理
            contents_to_handle = list(entity.contents)  # 复制列表
            entity.contents = []  # 清空内容列表
            # 地点本身没有 'container_id'

        # 从旧容器列表中移除被销毁的实体 (如果它在容器里)
        if container_id:
            try:
                self._remove_from_container(entity_id, container_id)
            except Exception as e:
                logging.error(f"销毁实体 '{entity_id}' 时从容器 '{container_id}' 移除失败: {e}", exc_info=True)
                # 继续执行，容器列表可能不一致

        # --- 处理被销毁容器内的物品/角色 ---
        # 原型阶段：简单地将它们的 location/current_place 设为 None
        if items_to_handle:  # 角色被销毁
            logging.info(f"角色 '{entity_id}' 已销毁，处理其持有的 {len(items_to_handle)} 个物品...")
            for item_id in items_to_handle:
                item = self.find_entity(item_id)
                if item and isinstance(item, Item) and not item.is_destroyed:
                    logging.debug(f"  物品 '{item_id}' ({item.name}) 位置因持有者销毁而清除。")
                    item.location = None  # 失去持有者，位置变为空
        if contents_to_handle:  # 地点被销毁
            logging.info(f"地点 '{entity_id}' 已销毁，处理其包含的 {len(contents_to_handle)} 个实体...")
            for content_id in contents_to_handle:
                content = self.find_entity(content_id)
                if content and not content.is_destroyed:
                    if isinstance(content, Item):
                        logging.debug(f"  物品 '{content_id}' ({content.name}) 位置因所在地点销毁而清除。")
                        content.location = None
                    elif isinstance(content, Character):
                        logging.debug(f"  角色 '{content_id}' ({content.name}) 位置因所在地点销毁而清除。")
                        content.current_place = None
                    # 如果地点里还有地点？这不应该发生

        # 如果被销毁的是焦点，移除焦点
        self.remove_focus(entity_id)

    def execute_transfer(self, entity_type: Literal["Item", "Character"], entity_id: str,
                         target_spec: Union[str, Tuple[str, str]]):
        """执行 Transfer 指令 (使用 _ensure_entity_exists)"""
        entity = self.find_entity(entity_id)
        if not entity: raise ValueError(f"Transfer: 找不到要转移的实体 '{entity_id}'")
        if entity.entity_type != entity_type: raise TypeError(f"Transfer: 实体类型不匹配")

        context = f"Transfer '{entity_id}' ({entity_type})"
        expected_target_type: Optional[Union[Type, Tuple[Type, ...]]] = None
        current_location_attr: Optional[str] = None

        # 确定期望的目标类型和当前位置属性名
        if isinstance(entity, Item):
            expected_target_type = (Character, Place)
            current_location_attr = 'location'
        elif isinstance(entity, Character):
            expected_target_type = Place
            current_location_attr = 'current_place'
        else:  # 不应该发生
            raise TypeError(f"Transfer 不支持转移类型 {entity_type}")

        # --- 确保目标容器存在 (或自动创建) ---
        final_target_id = self._ensure_entity_exists(target_spec, expected_target_type, context)
        if not final_target_id:
            # _ensure_entity_exists 在失败时应已崩溃
            raise ValueError(f"{context}: 未能确定或创建有效的转移目标。")

        # 获取旧容器 ID (用于更新列表)
        old_container_id: Optional[str] = None
        if current_location_attr:
            try:
                old_container_id = getattr(entity, current_location_attr)
            except AttributeError:
                pass  # 应该存在

        # --- 执行转移 ---
        if old_container_id == final_target_id:
            logging.info(f"Transfer: 实体 '{entity_id}' 已在目标 '{final_target_id}' 中，无需转移。")
            return

        logging.info(f"开始转移实体 '{entity_id}' 从 '{old_container_id}' 到 '{final_target_id}'")
        try:
            # 1. 更新实体的位置属性
            if current_location_attr:
                entity.set_attribute(current_location_attr, final_target_id)
            # 2. 更新旧容器和新容器的列表
            self._update_container_lists(entity_id, old_container_id, new_container_id=final_target_id)
            logging.info(f"实体 '{entity_id}' 成功转移到 '{final_target_id}'")
        except Exception as e:
            logging.error(f"执行 Transfer 从 '{old_container_id}' 到 '{final_target_id}' 失败: {e}", exc_info=True)
            # 转移失败，状态可能不一致，原型阶段崩溃
            raise e

    def _update_container_lists(self, entity_id: str, old_container_id: Optional[str],
                                new_container_id: Optional[str]):
        """原子地更新旧容器和新容器的内容列表 (逻辑不变)"""
        if old_container_id == new_container_id: return
        # 从旧容器移除 (如果旧容器存在)
        if old_container_id:
            try:
                self._remove_from_container(entity_id, old_container_id)
            except Exception as e:
                logging.error(f"从旧容器 '{old_container_id}' 移除 '{entity_id}' 时出错: {e}")
        # 添加到新容器 (如果新容器存在)
        if new_container_id:
            try:
                self._add_to_container(entity_id, new_container_id)
            except Exception as e:
                logging.error(f"将实体 '{entity_id}' 添加到新容器 '{new_container_id}' 时出错: {e}")

    # --- 新增：焦点管理方法 ---
    def set_focus(self, entity_ids: List[str]):
        """设置用户焦点，覆盖之前的焦点"""
        valid_ids = []
        for entity_id in entity_ids:
            entity = self.find_entity(entity_id)
            if entity:
                valid_ids.append(entity_id)
                logging.info(f"用户焦点设置为: {entity.entity_type} '{entity.name}' (ID: {entity_id})")
            else:
                logging.warning(f"设置焦点失败: 找不到实体 ID '{entity_id}'")
        self.world.user_focus = valid_ids

    def add_focus(self, entity_id: str) -> bool:
        """添加一个实体到用户焦点列表 (如果不存在)"""
        if entity_id in self.world.user_focus:
            logging.info(f"实体 '{entity_id}' 已在焦点列表中。")
            return True  # 已经在焦点中

        entity = self.find_entity(entity_id)
        if entity:
            self.world.user_focus.append(entity_id)
            logging.info(f"添加焦点: {entity.entity_type} '{entity.name}' (ID: {entity_id})")
            return True
        else:
            logging.warning(f"添加焦点失败: 找不到实体 ID '{entity_id}'")
            return False

    def remove_focus(self, entity_id: str) -> bool:
        """从用户焦点列表中移除一个实体"""
        if entity_id in self.world.user_focus:
            self.world.user_focus.remove(entity_id)
            logging.info(f"移除焦点: {entity_id}")
            return True
        else:
            logging.warning(f"移除焦点失败: 实体 ID '{entity_id}' 不在焦点列表中。")
            return False

    def clear_focus(self):
        """清除所有用户焦点"""
        logging.info("清除所有用户焦点。")
        self.world.user_focus = []

    def get_current_focus(self) -> List[str]:
        """获取当前的用户焦点列表"""
        return self.world.user_focus

        # --- 状态摘要 (不变) ---

    def get_state_summary(self) -> str:
        """生成基于焦点的 YAML 摘要 (逻辑不变)"""
        # ... (代码同上一个版本) ...
        summary_data: Dict[str, Any] = {"focused_entities": [], "world_slice": {"places": {}}}
        relevant_place_ids: Set[str] = set()
        focused_chars_no_place: Set[str] = set()
        active_focus_ids = []
        for fid in self.get_current_focus():
            entity = self.find_entity(fid)
            if not entity: continue
            active_focus_ids.append(fid)
            if isinstance(entity, Place):
                relevant_place_ids.add(fid)
            elif isinstance(entity, Character):
                pid = entity.current_place
                if pid and self.find_entity(pid):
                    relevant_place_ids.add(pid)
                else:
                    focused_chars_no_place.add(fid)
            elif isinstance(entity, Item):
                loc = entity.location
                if loc:
                    cont = self.find_entity(loc)
                    if cont:
                        if isinstance(cont, Place):
                            relevant_place_ids.add(loc)
                        elif isinstance(cont, Character):
                            cpid = cont.current_place
                            if cpid and self.find_entity(cpid): relevant_place_ids.add(cpid)
        summary_data["focused_entities"] = active_focus_ids
        places_slice = summary_data["world_slice"]["places"]
        processed_chars: Set[str] = set()
        for place_id in relevant_place_ids:
            place = self.find_entity(place_id)
            if not place or not isinstance(place, Place): continue
            place_data = place.get_all_attributes()
            place_data["characters"] = {}
            place_data["items"] = {}
            for cid in place.contents:
                cont = self.find_entity(cid)
                if not cont: continue
                if isinstance(cont, Character):
                    char_data = cont.get_all_attributes()
                    char_data["items"] = {}
                    for iid in cont.has_items:
                        item = self.find_entity(iid)
                        if item and isinstance(item, Item):
                            char_data["items"][iid] = item.get_all_attributes()
                    place_data["characters"][cid] = char_data
                    processed_chars.add(cid)
                elif isinstance(cont, Item):
                    place_data["items"][cid] = cont.get_all_attributes()
            places_slice[place_id] = place_data
        unplaced_chars = focused_chars_no_place - processed_chars
        if unplaced_chars:
            unplaced_data = {}
            for char_id in unplaced_chars:
                char = self.find_entity(char_id)
                if char and isinstance(char, Character):
                    char_data = char.get_all_attributes()
                    char_data["items"] = {}
                    for iid in char.has_items:
                        item = self.find_entity(iid)
                        if item and isinstance(item, Item):
                            char_data["items"][iid] = item.get_all_attributes()
                    unplaced_data[char_id] = char_data
            if unplaced_data: summary_data["world_slice"]["unplaced_focused_characters"] = unplaced_data
        try:
            if not summary_data["world_slice"].get("places"): summary_data["world_slice"].pop("places", None)
            if not summary_data["world_slice"].get("unplaced_focused_characters"): summary_data["world_slice"].pop(
                "unplaced_focused_characters", None)
            if not summary_data["world_slice"]: summary_data.pop("world_slice", None)
            if not summary_data.get("focused_entities"): summary_data.pop("focused_entities", None)
            if not summary_data: return "当前焦点区域无可见内容或无焦点。"
            yaml_string = yaml.dump(summary_data, Dumper=yaml.Dumper, default_flow_style=False, allow_unicode=True,
                                    sort_keys=False)
            return re.sub(r'!!python/object:[^\s]+', '', yaml_string)
        except Exception as e:
            logging.error(f"生成 YAML 摘要时出错: {e}")
            return f"错误: 生成摘要失败: {e}"
