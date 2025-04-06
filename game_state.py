# game_state.py
import logging
import json
import re
import yaml
import random # 导入 random
from typing import List, Optional, Dict, Any, Literal, Union, Set, Tuple, Type, cast, Deque
from pathlib import Path
from datetime import datetime
from collections import deque
from pydantic import BaseModel, Field, ValidationError

# --- 导入 parser 定义的数据类 ---
from parser import DiceRollRequest

# --- 核心数据模型 ---
class BaseEntity(BaseModel):
    """所有游戏世界实体的基类"""
    entity_id: str = Field(...)
    name: str = Field(...)
    status: Dict[str, Any] = Field(default_factory=dict)
    is_destroyed: bool = Field(False)
    entity_type: Literal["Item", "Character", "Place"] = Field(...)

    @property
    def core_fields(self) -> Set[str]:
        # 返回模型的核心字段名（非 status）
        return set(self.model_fields.keys()) - {'status', 'entity_type'} # 移除 entity_type

    def get_attribute(self, key: str) -> Any:
        """获取核心属性或 status 属性"""
        if key in self.core_fields:
            # 检查属性是否存在于模型实例上，以防 Pydantic 模型更新
            if hasattr(self, key):
                return getattr(self, key)
            else:
                 raise AttributeError(f"实体 '{self.entity_id}' 模型上找不到核心属性 '{key}' (可能模型已更新)")
        elif key in self.status:
            return self.status[key]
        else:
            raise AttributeError(f"实体 '{self.entity_id}' 上找不到属性 '{key}'")

    def has_attribute(self, key: str) -> bool:
        """检查实体是否具有某个属性（核心或 status）"""
        return (key in self.core_fields and hasattr(self, key)) or key in self.status

    def delete_attribute(self, key: str) -> bool:
        """删除 status 中的属性，核心属性不可删除"""
        if key in self.core_fields:
            logging.warning(f"不能删除核心属性 '{key}' (实体: {self.entity_id})")
            return False
        elif key in self.status:
            del self.status[key]
            logging.debug(f"实体 '{self.entity_id}': 删除了 status 属性 '{key}'")
            return True
        else:
            return False # 不存在，无需删除

    def set_attribute(self, key: str, value: Any):
        """设置核心属性（带类型验证/转换尝试）或 status 属性"""
        if key in self.core_fields:
            field_info = self.model_fields.get(key)
            if not field_info:
                 # Pydantic v2 中 model_fields 应该包含所有字段
                 logging.error(f"内部错误: 无法获取核心字段 '{key}' 的信息 (实体: {self.entity_id})")
                 # 或者抛出异常，或者尝试直接 setattr (不安全)
                 # setattr(self, key, value) # 不推荐
                 return # 或者直接失败

            expected_type = field_info.annotation
            current_value = getattr(self, key, None) # 获取当前值用于比较和日志

            # 尝试直接设置，Pydantic 会进行验证
            try:
                setattr(self, key, value)
                logging.debug(f"实体 '{self.entity_id}': 设置核心属性 {key} = {repr(value)}")
            except ValidationError as e:
                logging.warning(f"设置核心属性 '{key}' (值: {repr(value)}) 时验证失败: {e}。将尝试类型转换。")
                converted_value = value
                try:
                    # 尝试根据期望类型进行转换
                    # Pydantic v2 的验证可能已经处理了一些转换，这里做更显式的尝试
                    # 注意：这里的转换逻辑可能需要根据具体类型细化
                    if expected_type and not isinstance(value, expected_type):
                         # 处理 Optional[T] 的情况
                         is_optional = getattr(expected_type, "__origin__", None) is Union and \
                                       type(None) in getattr(expected_type, "__args__", ())
                         if is_optional and value is None:
                              converted_value = None
                         else:
                              # 获取非 None 的类型
                              actual_type = expected_type
                              if is_optional:
                                   actual_type = next((t for t in getattr(expected_type, "__args__", ()) if t is not type(None)), None)

                              if actual_type and not isinstance(value, actual_type):
                                   try:
                                       # 尝试调用类型构造函数
                                       converted_value = actual_type(value)
                                       logging.debug(f"  尝试将值 '{repr(value)}' 转换为类型 {actual_type}")
                                   except (TypeError, ValueError) as conversion_error:
                                        logging.error(f"  将值 '{repr(value)}' 转换为类型 {actual_type} 失败: {conversion_error}")
                                        raise e # 转换失败，重新抛出原始验证错误
                    # 再次尝试设置转换后的值
                    setattr(self, key, converted_value)
                    logging.debug(f"实体 '{self.entity_id}': 转换后设置核心属性 {key} = {repr(converted_value)}")
                except Exception as final_set_error:
                    logging.error(f"设置核心属性 '{key}' (尝试转换后) 失败: {final_set_error}")
                    raise final_set_error # 最终设置失败，抛出错误
            except Exception as e:
                 logging.error(f"设置核心属性 '{key}' 时发生未知错误: {e}", exc_info=True)
                 raise e
        else:
            # 设置 status 属性
            self.status[key] = value
            logging.debug(f"实体 '{self.entity_id}': 设置 status 属性 {key} = {repr(value)}")

    def get_all_attributes(self, exclude_internal: Set[str] = {'status', 'is_destroyed', 'entity_type', 'entity_id'}) -> Dict[str, Any]:
        """获取实体所有用户可见属性的字典（核心 + status）"""
        all_attrs = {}
        # 获取核心属性值
        for key in self.core_fields:
            if key not in exclude_internal and hasattr(self, key):
                 value = getattr(self, key)
                 # 对列表进行拷贝，避免外部修改影响内部状态 (虽然 pydantic 可能有保护)
                 all_attrs[key] = list(value) if isinstance(value, list) else value
        # 合并 status 属性
        for key, value in self.status.items():
             # 避免覆盖核心属性（理论上不应发生），并排除内部字段
            if key not in exclude_internal and key not in all_attrs:
                all_attrs[key] = list(value) if isinstance(value, list) else value

        # 清理空的结构性字段（如空的 contents, has_items, location=None）
        structure_fields = {'location', 'current_place', 'has_items', 'contents', 'exits'} # 添加 exits
        keys_to_remove = {field for field in structure_fields if field in all_attrs and not all_attrs[field]}
        for key in keys_to_remove:
             del all_attrs[key]

        return all_attrs

    def modify_attribute(self, key: str, opAndValue: Tuple[Optional[str], Any]) -> bool:
        """
        修改实体属性，内部处理 +=, -=, +, - 操作符。
        传入的值应该是已解析好的 (非 DiceRollRequest)。
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
                attr_exists = False # 防御性编程

        new_value: Any = None
        operation_performed = False
        skip_set_value = False # 用于标记是否需要跳过最后的 set_attribute

        logging.debug(f"实体 '{self.entity_id}': 尝试修改属性 '{key}', Op='{op}', Value='{repr(value_to_process)}', 当前值='{repr(current_value)}'")

        try:
            # --- 处理带操作符的情况 (+, -, +=, -=) ---
            if op:
                if not attr_exists and op in ('+=', '-='):
                     # 如果属性不存在，+= 视为设置，-= 无操作
                     if op == '+=':
                         new_value = value_to_process
                         logging.debug(f"  属性 '{key}' 不存在，+= 视为设置。")
                     else: # op == '-='
                         skip_set_value = True
                         logging.debug(f"  属性 '{key}' 不存在，-= 无操作。")
                     operation_performed = True
                elif not attr_exists and op in ('+', '-'):
                     # + 或 - 对不存在的属性无意义
                     skip_set_value = True
                     logging.warning(f"  属性 '{key}' 不存在，无法执行 '{op}' 操作。")
                     operation_performed = True # 标记已处理（无操作也是处理）
                else: # 属性存在
                    if op == '+=' or op == '+':
                        # 数值加法
                        if isinstance(current_value, (int, float)) and isinstance(value_to_process, (int, float)):
                            new_value = current_value + value_to_process
                        # 字符串拼接
                        elif isinstance(current_value, str) and isinstance(value_to_process, str):
                            new_value = current_value + value_to_process
                        # 列表追加 (+= 和 + 行为一致：添加元素)
                        elif isinstance(current_value, list):
                            # 确保 value_to_process 不是列表本身，而是要添加的元素
                            if isinstance(value_to_process, list) and op == '+':
                                 # op == '+' 且 value 是列表，执行列表合并
                                 new_value = current_value + value_to_process
                            else:
                                 # op == '+=' 或者 op == '+' 但 value 不是列表，添加元素
                                 new_value = list(current_value) # 创建副本
                                 new_value.append(value_to_process)
                        # 字典合并 (+= 和 + 行为一致：更新/添加键值对)
                        elif isinstance(current_value, dict) and isinstance(value_to_process, dict):
                             new_value = current_value.copy() # 创建副本
                             new_value.update(value_to_process)
                        else:
                            logging.warning(f"Modify({self.entity_id}): 属性 '{key}' (类型 {type(current_value)}) 不支持操作符 '{op}' 或值类型 '{type(value_to_process)}' 不匹配。")
                            skip_set_value = True
                        operation_performed = True

                    elif op == '-=' or op == '-':
                        # 数值减法
                        if isinstance(current_value, (int, float)) and isinstance(value_to_process, (int, float)):
                            new_value = current_value - value_to_process
                        # 列表移除 ( -= 和 - 行为一致：移除第一个匹配项)
                        elif isinstance(current_value, list):
                            new_value = list(current_value) # 创建副本
                            try:
                                new_value.remove(value_to_process)
                            except ValueError:
                                logging.warning(f"Modify({self.entity_id}): 尝试从列表 '{key}' 移除不存在的元素 '{value_to_process}'")
                                # 元素不存在，保持原列表或设为None？保持原列表更安全
                                new_value = current_value # 无变化
                                # 或者可以选择 skip_set_value = True
                        # 字典移除键 ( -= 和 - 行为一致：根据 key 移除)
                        elif isinstance(current_value, dict) and isinstance(value_to_process, str): # 假设用字符串指定 key 来移除
                             new_value = current_value.copy()
                             if value_to_process in new_value:
                                 del new_value[value_to_process]
                             else:
                                 logging.warning(f"Modify({self.entity_id}): 尝试从字典 '{key}' 移除不存在的键 '{value_to_process}'")
                                 new_value = current_value # 无变化
                        # 字符串移除？（标准库无直接移除子串的操作，忽略）
                        # 属性删除 (仅当 op 为 - 时)
                        elif op == '-':
                             if self.delete_attribute(key):
                                 skip_set_value = True # 删除成功，跳过设置
                             else:
                                 logging.warning(f"Modify({self.entity_id}): 无法使用 '-' 操作符删除属性 '{key}' (核心属性?)")
                                 skip_set_value = True # 即使删除失败也跳过设置
                        else: # op == '-='
                             logging.warning(f"Modify({self.entity_id}): 属性 '{key}' (类型 {type(current_value)}) 不支持操作符 '-=' 或值类型 '{type(value_to_process)}' 不匹配。")
                             skip_set_value = True
                        operation_performed = True

            # --- 处理无操作符的情况 (替换/设置) ---
            else: # op is None
                new_value = value_to_process
                operation_performed = True

            # --- 应用新值 ---
            if operation_performed and not skip_set_value:
                self.set_attribute(key, new_value)
                logging.debug(f"  属性 '{key}' 已更新为: {repr(new_value)}")
                # 检查是否修改了位置属性
                if key == "location" or key == "current_place":
                    location_updated = True
            elif skip_set_value:
                 logging.debug(f"  属性 '{key}' 的修改操作跳过了值的设置。")
            # else: !operation_performed (理论上不应发生)

            return location_updated # 返回位置是否更新的标记

        except AttributeError as e:
            # 这通常发生在尝试 get_attribute 不存在的属性时
            logging.warning(f"Modify({self.entity_id}): 尝试访问不存在的属性 '{key}' 时出错: {e}")
            return False
        except Exception as e:
            logging.error(f"Modify({self.entity_id}): 处理属性 '{key}' (Op='{op}', Value='{repr(value_to_process)}') 时发生错误: {e}", exc_info=True)
            raise e # 让错误向上冒泡


class Item(BaseEntity):
    """代表游戏中的物品"""
    entity_type: Literal["Item"] = "Item"
    quantity: int = Field(1, description="物品数量，用于堆叠", ge=0) # 允许数量为0？通常至少为1
    location: Optional[str] = Field(None, description="物品所在位置的 entity_id (可以是 Place 或 Character 的 ID)")

class Character(BaseEntity):
    """代表游戏中的角色"""
    entity_type: Literal["Character"] = "Character"
    current_place: Optional[str] = Field(None, description="角色当前所在地的 entity_id (必须是 Place 的 ID)")
    has_items: List[str] = Field(default_factory=list, description="角色持有的物品 entity_id 列表")

class Place(BaseEntity):
    """代表游戏中的地点"""
    entity_type: Literal["Place"] = "Place"
    contents: List[str] = Field(default_factory=list, description="地点包含的实体 entity_id 列表 (Character 或 Item)")
    # 新增：出口信息
    exits: Dict[str, str] = Field(default_factory=dict, description="出口方向到目标地点ID的映射, e.g., {'north': 'forest-path'}")


AnyEntity = Union[Item, Character, Place]

class WorldState(BaseModel):
    """存储所有游戏实体的容器"""
    items: Dict[str, Item] = Field(default_factory=dict)
    characters: Dict[str, Character] = Field(default_factory=dict)
    places: Dict[str, Place] = Field(default_factory=dict)
    # user_focus 不再直接存储在 WorldState，移到 GameState 层面管理，不参与快照核心
    # 因为焦点是临时的 UI 状态，不应影响回滚的核心世界数据
    # 如果需要持久化焦点，可以放在 SaveData.game_state_metadata 中

# --- 新增：存档数据模型 ---
class SaveData(BaseModel):
    """用于保存和加载游戏状态的数据结构"""
    current_world: WorldState = Field(...)
    # 对话记录（结构需与 main_gui.py 保持一致）
    conversation_log: List[Dict[str, Any]] = Field(default_factory=list)
    # 回滚历史快照 (存储为 List)
    world_history: List[WorldState] = Field(default_factory=list)
    # 游戏状态的额外信息
    game_state_metadata: Dict[str, Any] = Field(default_factory=dict)
    # 可选的游戏元数据
    metadata: Dict[str, Any] = Field(default_factory=dict)


# --- 游戏状态管理器 ---
class GameState:
    """管理游戏世界状态 (增加历史快照、保存、加载、掷骰结果)"""

    def __init__(self, max_history: int = 10):
        self.world = WorldState()
        self.history: Deque[WorldState] = deque(maxlen=max_history)
        self.max_history = max_history
        # 焦点现在是 GameState 的属性，不属于 WorldState
        self.user_focus: List[str] = []
        # logging.basicConfig(...) # 移到入口

    def _get_entity_dict(self, entity_type: Literal["Item", "Character", "Place"]) -> Dict[str, AnyEntity]:
        """根据类型获取对应的实体字典"""
        if entity_type == "Item": return self.world.items
        elif entity_type == "Character": return self.world.characters
        elif entity_type == "Place": return self.world.places
        else: raise ValueError(f"未知的实体类型: {entity_type}")

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
        """按名称查找实体（效率较低）"""
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
        if entity.entity_id in entity_dict and not entity_dict[entity.entity_id].is_destroyed:
            logging.warning(f"警告: 实体 ID '{entity.entity_id}' 已存在且未被销毁，将被覆盖。")
        entity_dict[entity.entity_id] = entity
        logging.info(f"添加/更新了实体: {entity.entity_type} ID='{entity.entity_id}', Name='{entity.name}'")

    def _remove_from_container(self, entity_id: str, container_id: Optional[str]):
        """从旧容器的列表中移除实体 ID"""
        if not container_id: return
        container = self.find_entity(container_id, include_destroyed=True)
        if not container:
            logging.warning(f"尝试从不存在的容器 '{container_id}' 移除 '{entity_id}'")
            return

        if isinstance(container, Character) and entity_id in container.has_items:
            container.has_items.remove(entity_id)
            logging.debug(f"从角色 '{container_id}' 移除物品 '{entity_id}'")
        elif isinstance(container, Place) and entity_id in container.contents:
            container.contents.remove(entity_id)
            logging.debug(f"从地点 '{container_id}' 移除实体 '{entity_id}'")

    def _add_to_container(self, entity_id: str, container_id: Optional[str]):
        """将实体 ID 添加到新容器的列表中 (容器必须已存在)"""
        if not container_id: return
        container = self.find_entity(container_id)
        if not container:
             raise ValueError(f"_add_to_container 内部错误: 目标容器 '{container_id}' 不存在或已被销毁。")

        entity_to_add = self.find_entity(entity_id)
        if not entity_to_add:
            raise ValueError(f"无法添加不存在或已销毁的实体 '{entity_id}' 到容器 '{container_id}'。")

        if isinstance(container, Character):
            if entity_to_add.entity_type != "Item":
                raise TypeError(f"不能将非物品实体 '{entity_id}' ({entity_to_add.entity_type}) 添加到角色 '{container_id}'。")
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
        warning_prefix = "Warning: Missing"
        placeholder_name = f"{warning_prefix} {entity_type} [{entity_id}] (Auto-created by: {context})"
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

    def _ensure_entity_exists(self,
                              entity_spec: Union[str, Tuple[str, str], None],
                              expected_container_type: Optional[Union[Type[Character], Type[Place], Tuple[Type[Character], Type[Place]]]] = None,
                              context: str = "未知操作") -> Optional[str]:
        """检查实体是否存在，不存在则尝试创建占位符，返回有效 ID"""
        if entity_spec is None: return None

        entity_id: str = ""
        provided_type: Optional[Literal["Item", "Character", "Place"]] = None

        if isinstance(entity_spec, tuple) and len(entity_spec) == 2:
            raw_type, entity_id = entity_spec
            # 验证并标准化类型
            if isinstance(raw_type, str) and raw_type.capitalize() in ["Item", "Character", "Place"]:
                 provided_type = cast(Literal["Item", "Character", "Place"], raw_type.capitalize())
            else:
                 raise ValueError(f"{context}: 无效的实体类型 '{raw_type}' 在规范 '{entity_spec}' 中。")
            logging.debug(f"{context}: 检查实体存在性，需要类型 '{provided_type}' ID '{entity_id}'。")
        elif isinstance(entity_spec, str):
            entity_id = entity_spec
            logging.warning(f"{context}: 使用了旧的实体 ID 格式 '{entity_id}' (无类型)。如果实体不存在将失败。")
        else:
            raise TypeError(f"{context}: 无效的实体规范格式: {repr(entity_spec)}")

        if not entity_id: raise ValueError(f"{context}: 无法从规范 '{repr(entity_spec)}' 提取有效 entity_id")

        entity = self.find_entity(entity_id)

        if entity:
            # 验证类型 (如果提供了预期类型)
            if expected_container_type and not isinstance(entity, expected_container_type):
                logging.warning(f"{context}: 找到实体 '{entity_id}'，但其类型 ({entity.entity_type}) 与期望容器类型不匹配。继续使用。")
            if provided_type and entity.entity_type != provided_type:
                 logging.warning(f"{context}: AI 提供的类型 '{provided_type}' 与找到的实体 '{entity_id}' ({entity.entity_type}) 不符。使用找到的实体。")
            logging.debug(f"{context}: 确认实体 '{entity_id}' ({entity.entity_type}) 存在。")
            return entity_id
        else:
            # 实体不存在
            if provided_type:
                logging.info(f"{context}: 目标实体 '{entity_id}' 不存在，尝试根据类型 '{provided_type}' 自动创建。")
                try:
                    placeholder = self._create_placeholder_entity(provided_type, entity_id, context)
                    return placeholder.entity_id
                except Exception as e:
                    logging.error(f"{context}: 自动创建实体 '{entity_id}' ({provided_type}) 失败。错误: {e}", exc_info=True)
                    raise e # 让其崩溃
            else:
                logging.error(f"{context}: 目标实体 '{entity_id}' 不存在，且未提供类型，无法自动创建。")
                raise ValueError(f"目标实体 '{entity_id}' 不存在且无法自动创建")

    # --- 掷骰计算 ---
    def _calculate_dice_roll(self, request: DiceRollRequest) -> int:
        """根据 DiceRollRequest 计算掷骰结果"""
        expression = request.expression.lower().strip()
        match = re.fullmatch(r"(\d+)d(\d+)\s*(([+-])\s*(\d+))?", expression)
        if not match:
            raise ValueError(f"无效或无法解析的骰子表达式: {request.expression}")

        num_dice = int(match.group(1))
        dice_type = int(match.group(2))
        modifier_op = match.group(4)
        modifier_val = int(match.group(5) or 0)

        if num_dice <= 0 or dice_type <= 0:
             raise ValueError("骰子数量和面数必须为正")
        # 放宽限制，但日志记录
        if num_dice > 1000 or dice_type > 10000:
             logging.warning(f"执行非常大的掷骰计算: {request.expression}")

        total = sum(random.randint(1, dice_type) for _ in range(num_dice))

        if modifier_op == '+':
            total += modifier_val
        elif modifier_op == '-':
            total -= modifier_val

        logging.info(f"计算掷骰: {request.expression} -> 结果 = {total}")
        return total

    def _resolve_value(self, value: Any) -> Any:
        """如果值是 DiceRollRequest，则计算结果，否则返回值本身"""
        if isinstance(value, DiceRollRequest):
            return self._calculate_dice_roll(value)
        # 递归处理列表或字典中的 DiceRollRequest
        elif isinstance(value, list):
             return [self._resolve_value(item) for item in value]
        elif isinstance(value, dict):
             return {k: self._resolve_value(v) for k, v in value.items()}
        return value

    # --- 指令执行方法 ---
    def execute_create(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str,
                       params: Dict[str, Any]):
        """执行 Create 指令 (处理随机值, 检查位置存在性)"""
        existing_entity = self.find_entity(entity_id, include_destroyed=True)
        if existing_entity and not existing_entity.is_destroyed:
            logging.warning(f"Create 警告: 实体 ID '{entity_id}' 已存在且未销毁，将被覆盖。")

        # 堆叠检查 (在创建实体前)
        if entity_type == "Item": # 只有物品需要堆叠检查
            raw_quantity = params.get("quantity", 1) # 获取原始 quantity 值
            try:
                # 尝试解析 quantity (可能是 DiceRollRequest)
                resolved_quantity = self._resolve_value(raw_quantity)
                # 确保存储的是解析后的整数值，且 >= 0
                item_quantity = max(0, int(resolved_quantity)) if isinstance(resolved_quantity, (int, float)) else 1
            except Exception as e:
                logging.warning(f"Create Item '{entity_id}': 解析 quantity '{raw_quantity}' 失败: {e}。默认为 1。")
                item_quantity = 1

            if item_quantity > 0: # 数量大于0才可能堆叠
                location_spec = params.get("location")
                item_name = params.get("name")
                if location_spec and item_name:
                    context = f"Create Item '{entity_id}' 堆叠检查"
                    try:
                        container_id = self._ensure_entity_exists(location_spec, context=context)
                        if container_id:
                            container = self.find_entity(container_id)
                            if container:
                                c_list_attr = "has_items" if isinstance(container, Character) else ("contents" if isinstance(container, Place) else None)
                                if c_list_attr:
                                    c_list = getattr(container, c_list_attr, [])
                                    for existing_item_id in c_list:
                                        item = self.find_entity(existing_item_id)
                                        if item and isinstance(item, Item) and item.name == item_name and not item.is_destroyed:
                                            logging.info(f"物品堆叠: 在 '{container_id}' 的 '{item_name}' ({existing_item_id}) 增加数量 {item_quantity}")
                                            self.execute_modify("Item", existing_item_id, {"quantity": ('+=', item_quantity)})
                                            return # 堆叠成功，不再创建新物品
                    except Exception as e:
                         # 堆叠检查失败不应阻止创建，记录错误后继续
                         logging.error(f"{context} 失败: {e}")


        # 创建实体本身
        model_class: Type[BaseEntity]
        if entity_type == "Item": model_class = Item
        elif entity_type == "Character": model_class = Character
        elif entity_type == "Place": model_class = Place
        else: raise ValueError(f"无效类型: {entity_type}")

        init_params = {"entity_id": entity_id, "entity_type": entity_type}
        if 'name' not in params: raise ValueError(f"Create 指令 ({entity_id}) 缺少必需的 'name' 参数")
        init_params['name'] = params['name']

        try:
            new_entity = model_class(**init_params)
        except Exception as e:
            logging.error(f"创建 '{entity_id}' ({entity_type}) 初始化失败: {e}", exc_info=True)
            raise

        # 设置其他属性 (解析随机值)
        initial_location_spec: Any = None
        location_key = 'location' if entity_type == 'Item' else ('current_place' if entity_type == 'Character' else None)

        for key, raw_value in params.items():
            if key in init_params or key == location_key: continue
            try:
                actual_value = raw_value
                if isinstance(raw_value, tuple) and len(raw_value) == 2 and raw_value[0] in ('+=','-=','+','-'):
                    op, actual_value = raw_value
                    logging.warning(f"Create ({entity_id}): 属性 '{key}' 含无效操作符 '{op}'，已忽略。")

                final_value = self._resolve_value(actual_value) # 解析随机值

                # 特殊处理 quantity (确保 > 0, 整数)
                if key == 'quantity' and entity_type == 'Item':
                     if isinstance(final_value, (int, float)):
                          final_value = max(0, int(final_value)) # 确保 >= 0 并取整
                          if final_value == 0:
                               logging.info(f"Create ({entity_id}): 计算出的 quantity 为 0，实体将立即创建但可能无用。")
                     else:
                          logging.warning(f"Create ({entity_id}): quantity 解析结果非数值 ({type(final_value)})，使用默认值 1。")
                          final_value = 1 # 如果解析失败给个默认值

                new_entity.set_attribute(key, final_value)
                logging.debug(f"  Create: 设置属性 {key} = {repr(final_value)} (原始值: {repr(actual_value)})")
            except Exception as e:
                logging.warning(f"创建 '{entity_id}' 时设置属性 '{key}' (原始值: {repr(raw_value)}) 失败: {e}")
                # raise e # 可选

        # 记录原始位置规范
        if location_key and location_key in params:
            initial_location_spec = params[location_key]
            if isinstance(initial_location_spec, tuple) and len(initial_location_spec) == 2 and initial_location_spec[0] in ('+=','-=','+','-'):
                 op, initial_location_spec = initial_location_spec
                 logging.warning(f"Create ({entity_id}): 位置属性 '{location_key}' 含无效操作符 '{op}'，已忽略。")
            if isinstance(initial_location_spec, DiceRollRequest):
                 logging.error(f"Create ({entity_id}): 位置属性 '{location_key}' 不支持随机值 '{initial_location_spec.expression}'")
                 raise ValueError(f"位置属性 '{location_key}' 不支持随机值")

        # 添加实体到世界
        self._add_entity(new_entity)

        # 处理位置
        if location_key and initial_location_spec:
             context = f"Create '{entity_id}' ({entity_type}) 设置位置"
             expected_type: Optional[Union[Type, Tuple[Type, ...]]] = None
             if entity_type == 'Item': expected_type = (Character, Place)
             elif entity_type == 'Character': expected_type = Place

             final_location_id = self._ensure_entity_exists(initial_location_spec, expected_type, context)

             if final_location_id:
                 try:
                     new_entity.set_attribute(location_key, final_location_id)
                     self._add_to_container(new_entity.entity_id, final_location_id)
                     logging.info(f"实体 '{entity_id}' 已放置在 '{final_location_id}'")
                 except Exception as e:
                     logging.error(f"创建 '{entity_id}' 后设置位置 '{final_location_id}' 或加入容器失败: {e}", exc_info=True)
                     raise e
             else:
                 raise ValueError(f"未能为 '{entity_id}' 确定有效位置")

        # 自动聚焦新地点
        if isinstance(new_entity, Place) and entity_id not in self.user_focus:
             logging.info(f"新地点 '{entity_id}' 已创建，自动添加到用户焦点。")
             self.add_focus(entity_id)


    def execute_modify(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str,
                       updates: Dict[str, Any]):
        """执行 Modify 指令 (解析随机值, 处理位置变更)"""
        entity = self.find_entity(entity_id)
        if not entity: raise ValueError(f"Modify: 找不到实体 '{entity_id}'")
        if entity.entity_type != entity_type: raise TypeError(f"Modify: 实体类型不匹配")

        logging.info(f"开始修改实体 '{entity_id}' ({entity.name})。 更新内容: {updates}")

        old_location_or_place: Optional[str] = None
        location_key_being_modified: Optional[str] = None
        new_location_spec: Any = None

        try:
            if isinstance(entity, Item): old_location_or_place = entity.location
            elif isinstance(entity, Character): old_location_or_place = entity.current_place
        except AttributeError: pass

        for key, raw_update_value in updates.items():
            op: Optional[str] = None
            value_to_process: Any = None

            if isinstance(raw_update_value, tuple) and len(raw_update_value) == 2 and raw_update_value[0] in ('+=', '-=', '+', '-'):
                op, value_to_process = raw_update_value
            else:
                op = None
                value_to_process = raw_update_value

            # --- 在处理前解析随机值 ---
            resolved_value = self._resolve_value(value_to_process)
            logging.debug(f"  Modify: Key='{key}', Op='{op}', Raw='{repr(value_to_process)}', Resolved='{repr(resolved_value)}'")

            is_location_key = (key == 'location' and isinstance(entity, Item)) or \
                              (key == 'current_place' and isinstance(entity, Character))

            if is_location_key:
                location_key_being_modified = key
                if isinstance(value_to_process, DiceRollRequest): # 检查原始值
                     raise ValueError(f"Modify ({entity_id}): 位置属性 '{key}' 不支持随机值")
                new_location_spec = resolved_value # 使用解析后的值 (str or (Type, ID))
                if op and op != '-':
                    logging.warning(f"Modify ({entity_id}): 位置属性 '{key}' 不支持操作符 '{op}'。尝试赋值。")
                    op = None
                if op == '-': new_location_spec = None
                logging.debug(f"  计划修改位置属性 '{key}' 为 '{repr(new_location_spec)}'。")
                continue

            # --- 处理非位置属性 ---
            try:
                # 将解析后的值传递给实体的方法
                # 特别处理 quantity，确保修改后 >= 0
                final_value_for_modify = resolved_value
                if key == 'quantity' and isinstance(entity, Item) and isinstance(resolved_value, (int, float)):
                     # 需要考虑操作符
                     current_qty = entity.quantity
                     temp_qty = current_qty
                     if op == '+=' or op == '+': temp_qty += resolved_value
                     elif op == '-=' or op == '-': temp_qty -= resolved_value
                     elif op is None: temp_qty = resolved_value # 直接赋值

                     final_value_for_modify = max(0, int(temp_qty))
                     logging.debug(f"  Modify quantity: current={current_qty}, op='{op}', resolved_val={resolved_value}. Tentative new quantity={temp_qty}. Final non-negative value={final_value_for_modify}")
                     # 如果是赋值操作，直接使用最终值；如果是增减操作，需要传递 *增减量*
                     if op in ('+=', '+'):
                          # 传递增量，但要考虑当前值，确保结果不为负
                          # 这逻辑有点复杂，modify_attribute 内部处理更好？
                          # 暂时简化：modify_attribute 接收最终计算好的值进行增减
                          # 注意：这意味着 += 1d6 可能导致负数，需要约束
                          # 更好的方法：在 modify_attribute 内部处理 DiceRollRequest?
                          # --- 妥协：在调用前确保 resolved_value 不会导致负数 ---
                          if op == '-=' or op == '-':
                              if current_qty - resolved_value < 0:
                                   logging.warning(f"Modify({entity_id}): quantity 操作会导致负数 ({current_qty} {op} {resolved_value})。将调整操作数使得结果为 0。")
                                   resolved_value = current_qty # 操作数等于当前值，结果为0
                          final_value_for_modify = resolved_value # 传递解析后的值给 modify_attribute
                     elif op is None:
                          # 赋值操作，直接使用 max(0, int(temp_qty))
                           final_value_for_modify = max(0, int(resolved_value))


                entity.modify_attribute(key, (op, final_value_for_modify)) # 传递计算好的值
                logging.debug(f"  已调用 entity.modify_attribute: key='{key}', op='{op}', value='{repr(final_value_for_modify)}'")

            except Exception as e:
                logging.error(f"执行 Modify 操作失败: 实体='{entity_id}', Key='{key}', Op='{op}', Value='{repr(resolved_value)}'. 错误: {e}", exc_info=True)
                raise e

        # --- 统一处理位置变更 ---
        if location_key_being_modified:
            context = f"Modify '{entity_id}' 更新位置 '{location_key_being_modified}'"
            final_new_location_id: Optional[str] = None
            if new_location_spec is not None: # 如果不是移除位置
                 expected_type: Optional[Union[Type, Tuple[Type, ...]]] = None
                 if isinstance(entity, Item): expected_type = (Character, Place)
                 elif isinstance(entity, Character): expected_type = Place
                 final_new_location_id = self._ensure_entity_exists(new_location_spec, expected_type, context)

            if old_location_or_place != final_new_location_id:
                 logging.info(f"位置变更检测: '{entity_id}' 从 '{old_location_or_place}' 移动到 '{final_new_location_id}'")
                 try:
                     entity.set_attribute(location_key_being_modified, final_new_location_id)
                     self._update_container_lists(entity_id, old_location_or_place, new_container_id=final_new_location_id)
                 except Exception as e:
                     logging.error(f"应用位置变更从 '{old_location_or_place}' 到 '{final_new_location_id}' 失败: {e}", exc_info=True)
                     raise e
            else:
                 logging.debug(f"实体 '{entity_id}' 位置属性修改，但最终 ID ('{final_new_location_id}') 未变。")

        logging.info(f"实体 '{entity_id}' 修改完成。")

    def execute_destroy(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str):
        """执行 Destroy 指令"""
        entity = self.find_entity(entity_id, include_destroyed=True)
        if not entity:
            logging.warning(f"Destroy 指令警告: 找不到实体 ID '{entity_id}'")
            return
        if entity.is_destroyed:
            logging.info(f"实体 '{entity_id}' 已被销毁，无需操作。")
            return
        if entity.entity_type != entity_type:
            raise TypeError(f"Destroy 指令错误: 实体 '{entity_id}' 类型为 {entity.entity_type}, 但指令指定为 {entity_type}")

        logging.info(f"销毁实体: {entity.entity_type} ID='{entity_id}', Name='{entity.name}'")
        entity.is_destroyed = True

        container_id: Optional[str] = None
        items_to_handle: List[str] = []
        contents_to_handle: List[str] = []

        if isinstance(entity, Item):
            container_id = entity.location
            entity.location = None
        elif isinstance(entity, Character):
            container_id = entity.current_place
            entity.current_place = None
            items_to_handle = list(entity.has_items)
            entity.has_items = []
        elif isinstance(entity, Place):
            contents_to_handle = list(entity.contents)
            entity.contents = []
            # 地点销毁时，还需要处理指向它的出口 (可选，较复杂)
            # for place in self.world.places.values():
            #    if not place.is_destroyed and place.exits:
            #       exits_to_remove = [k for k, v in place.exits.items() if v == entity_id]
            #       for k in exits_to_remove: del place.exits[k]

        # 从旧容器移除
        if container_id:
             try: self._remove_from_container(entity_id, container_id)
             except Exception as e: logging.error(f"销毁实体 '{entity_id}' 时从容器 '{container_id}' 移除失败: {e}")

        # 处理内部物品/角色
        if items_to_handle:
            logging.info(f"角色 '{entity_id}' 销毁，处理其 {len(items_to_handle)} 个物品...")
            for item_id in items_to_handle:
                item = self.find_entity(item_id)
                if item and isinstance(item, Item) and not item.is_destroyed:
                    item.location = None # 物品掉落 (无主)
                    logging.debug(f"  物品 '{item_id}' ({item.name}) 位置清除。")
        if contents_to_handle:
             logging.info(f"地点 '{entity_id}' 销毁，处理其 {len(contents_to_handle)} 个内容物...")
             for content_id in contents_to_handle:
                 content = self.find_entity(content_id)
                 if content and not content.is_destroyed:
                     if isinstance(content, Item): content.location = None
                     elif isinstance(content, Character): content.current_place = None
                     logging.debug(f"  内容物 '{content_id}' ({content.name}) 位置清除。")

        # 移除焦点
        self.remove_focus(entity_id)


    def execute_transfer(self, entity_type: Literal["Item", "Character"], entity_id: str, target_spec: Union[str, Tuple[str, str]]):
        """执行 Transfer 指令"""
        entity = self.find_entity(entity_id)
        if not entity: raise ValueError(f"Transfer: 找不到要转移的实体 '{entity_id}'")
        if entity.entity_type != entity_type: raise TypeError(f"Transfer: 实体类型不匹配")

        # 检查目标是否是随机值 (不允许)
        if isinstance(target_spec, DiceRollRequest):
             raise ValueError(f"Transfer 指令的目标不能是随机值: {target_spec.expression}")

        context = f"Transfer '{entity_id}' ({entity_type})"
        expected_target_type: Optional[Union[Type, Tuple[Type, ...]]] = None
        current_location_attr: Optional[str] = None

        if isinstance(entity, Item):
            expected_target_type = (Character, Place)
            current_location_attr = 'location'
        elif isinstance(entity, Character):
            expected_target_type = Place
            current_location_attr = 'current_place'
        else: raise TypeError(f"Transfer 不支持转移类型 {entity_type}")

        final_target_id = self._ensure_entity_exists(target_spec, expected_target_type, context)
        if not final_target_id: raise ValueError(f"{context}: 未能确定或创建有效转移目标。")

        old_container_id: Optional[str] = None
        if current_location_attr:
            try: old_container_id = getattr(entity, current_location_attr)
            except AttributeError: pass

        if old_container_id == final_target_id:
            logging.info(f"Transfer: 实体 '{entity_id}' 已在目标 '{final_target_id}' 中，无需转移。")
            return

        logging.info(f"开始转移实体 '{entity_id}' 从 '{old_container_id}' 到 '{final_target_id}'")
        try:
            if current_location_attr: entity.set_attribute(current_location_attr, final_target_id)
            self._update_container_lists(entity_id, old_container_id, new_container_id=final_target_id)
            logging.info(f"实体 '{entity_id}' 成功转移到 '{final_target_id}'")
        except Exception as e:
            logging.error(f"执行 Transfer 从 '{old_container_id}' 到 '{final_target_id}' 失败: {e}", exc_info=True)
            raise e

    def _update_container_lists(self, entity_id: str, old_container_id: Optional[str], new_container_id: Optional[str]):
        """原子地更新旧容器和新容器的内容列表"""
        if old_container_id == new_container_id: return
        # 从旧容器移除
        if old_container_id:
            try: self._remove_from_container(entity_id, old_container_id)
            except Exception as e: logging.error(f"从旧容器 '{old_container_id}' 移除 '{entity_id}' 时出错: {e}")
        # 添加到新容器
        if new_container_id:
            try: self._add_to_container(entity_id, new_container_id)
            except Exception as e: logging.error(f"将实体 '{entity_id}' 添加到新容器 '{new_container_id}' 时出错: {e}")


    # --- 焦点管理 ---
    def set_focus(self, entity_ids: List[str]):
        valid_ids = []
        for entity_id in entity_ids:
            if self.find_entity(entity_id):
                valid_ids.append(entity_id)
            else:
                logging.warning(f"设置焦点失败: 找不到实体 ID '{entity_id}'")
        self.user_focus = valid_ids
        logging.info(f"用户焦点设置为: {', '.join(valid_ids) if valid_ids else '无'}")

    def add_focus(self, entity_id: str) -> bool:
        if entity_id in self.user_focus:
            logging.info(f"实体 '{entity_id}' 已在焦点列表中。")
            return True
        if self.find_entity(entity_id):
            self.user_focus.append(entity_id)
            logging.info(f"添加焦点: {entity_id}")
            return True
        else:
            logging.warning(f"添加焦点失败: 找不到实体 ID '{entity_id}'")
            return False

    def remove_focus(self, entity_id: str) -> bool:
        if entity_id in self.user_focus:
            self.user_focus.remove(entity_id)
            logging.info(f"移除焦点: {entity_id}")
            return True
        else:
            # 移除不存在的焦点不是错误，只是无操作
            # logging.warning(f"移除焦点失败: 实体 ID '{entity_id}' 不在焦点列表中。")
            return False

    def clear_focus(self):
        logging.info("清除所有用户焦点。")
        self.user_focus = []

    def get_current_focus(self) -> List[str]:
        return self.user_focus

    # --- 问题实体报告 ---
    def get_problematic_entities(self) -> List[Dict[str, Any]]:
        """查找所有由系统自动创建且尚未修复的占位符实体"""
        problematic = []
        warning_prefix = "Warning: Missing"
        entity_dicts = [self.world.items, self.world.characters, self.world.places]
        count = 0
        for entity_dict in entity_dicts:
            for entity_id, entity in entity_dict.items():
                if not entity.is_destroyed and entity.name.startswith(warning_prefix):
                    problematic.append({
                        "entity_id": entity.entity_id,
                        "entity_type": entity.entity_type,
                        "current_name": entity.name
                    })
                    count += 1
        if count > 0:
             logging.debug(f"找到 {count} 个问题实体。")
        return problematic

    # --- 状态摘要 ---
    def get_state_summary(self) -> str:
        """生成基于焦点的 YAML 摘要"""
        summary_data: Dict[str, Any] = {"focused_entities": [], "world_slice": {"places": {}}}
        relevant_place_ids: Set[str] = set()
        focused_chars_no_place: Set[str] = set()
        active_focus_ids = []

        current_focus = self.get_current_focus() # 使用方法获取当前焦点
        for fid in current_focus:
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
                    # 如果角色没有位置，或者位置无效，将角色本身加入待处理
                    focused_chars_no_place.add(fid)
            elif isinstance(entity, Item):
                loc = entity.location
                if loc:
                    container = self.find_entity(loc)
                    if container:
                        if isinstance(container, Place):
                            relevant_place_ids.add(loc)
                        elif isinstance(container, Character):
                            # 如果物品在角色身上，检查角色位置
                            char_place_id = container.current_place
                            if char_place_id and self.find_entity(char_place_id):
                                 relevant_place_ids.add(char_place_id)
                            else:
                                 # 如果物品在无位置的角色身上，将该角色加入待处理
                                 focused_chars_no_place.add(loc) # loc 是角色ID

        summary_data["focused_entities"] = active_focus_ids
        places_slice = summary_data["world_slice"]["places"]
        processed_chars: Set[str] = set()

        # 处理相关地点及其内容
        for place_id in relevant_place_ids:
            place = self.find_entity(place_id)
            if not place or not isinstance(place, Place): continue
            place_data = place.get_all_attributes()
            place_data["characters"] = {}
            place_data["items"] = {}
            # 添加出口信息到摘要
            if place.exits:
                 place_data["exits"] = place.exits # 直接添加字典

            for content_id in place.contents:
                content = self.find_entity(content_id)
                if not content: continue
                if isinstance(content, Character):
                    char_data = content.get_all_attributes()
                    char_data["items"] = {} # 存储角色持有的物品
                    for item_id in content.has_items:
                        item = self.find_entity(item_id)
                        if item and isinstance(item, Item):
                            char_data["items"][item_id] = item.get_all_attributes()
                    place_data["characters"][content_id] = char_data
                    processed_chars.add(content_id)
                elif isinstance(content, Item):
                    place_data["items"][content_id] = content.get_all_attributes()
            places_slice[place_id] = place_data

        # 处理不在相关地点内的、但被聚焦的角色
        unplaced_chars_to_show = focused_chars_no_place - processed_chars
        if unplaced_chars_to_show:
            unplaced_data = {}
            for char_id in unplaced_chars_to_show:
                char = self.find_entity(char_id)
                if char and isinstance(char, Character):
                    char_data = char.get_all_attributes()
                    char_data["items"] = {}
                    for item_id in char.has_items:
                        item = self.find_entity(item_id)
                        if item and isinstance(item, Item):
                            char_data["items"][item_id] = item.get_all_attributes()
                    unplaced_data[char_id] = char_data
            if unplaced_data:
                summary_data["world_slice"]["unplaced_focused_characters"] = unplaced_data

        # 清理空字典/列表
        if "places" in summary_data["world_slice"] and not summary_data["world_slice"]["places"]:
            del summary_data["world_slice"]["places"]
        if "unplaced_focused_characters" in summary_data["world_slice"] and not summary_data["world_slice"]["unplaced_focused_characters"]:
            del summary_data["world_slice"]["unplaced_focused_characters"]
        if not summary_data["world_slice"]:
            del summary_data["world_slice"]
        if not summary_data["focused_entities"]:
            del summary_data["focused_entities"]

        if not summary_data:
            return "当前焦点区域无可见内容或无焦点。"

        try:
            # 使用 yaml.dump 输出，确保 unicode 和顺序
            yaml_string = yaml.dump(
                summary_data, Dumper=yaml.Dumper, default_flow_style=False,
                allow_unicode=True, sort_keys=False, indent=2
            )
            # 移除 YAML 类型标签
            yaml_string = re.sub(r'!!python/object:[^\s]+', '', yaml_string)
            return yaml_string.strip()
        except Exception as e:
            logging.error(f"生成 YAML 摘要时出错: {e}")
            return f"错误: 生成摘要失败: {e}"


    # --- 快照与回滚 ---
    def save_history_point(self):
        """保存当前世界状态的快照"""
        # 检查 max_history 设置
        max_hist = self.history.maxlen if self.history.maxlen is not None else float('inf')

        if len(self.history) < max_hist:
            logging.info(f"保存历史状态点 (当前历史数: {len(self.history)} / {max_hist})...")
        else:
            logging.warning(f"历史记录已满 ({max_hist})，最旧的状态点将被覆盖。")

        try:
            # 只需拷贝 WorldState
            snapshot = self.world.model_copy(deep=True)
            self.history.appendleft(snapshot) # 新的放左边 (索引0)
            logging.debug("状态快照已创建并存入历史。")
        except Exception as e:
            logging.error(f"创建状态快照失败: {e}", exc_info=True)


    def rollback_state(self) -> bool:
        """回滚到上一个保存的状态点"""
        if not self.history:
            logging.warning("无法回滚：没有可用的历史状态。")
            return False
        logging.info("执行状态回滚...")
        try:
            previous_world_state = self.history.popleft() # 移除并获取最近的快照
            self.world = previous_world_state # 恢复世界状态
            logging.info("状态已成功回滚到上一个保存点。")
            # 注意：焦点信息 (self.user_focus) 在这里没有被回滚
            # 如果需要回滚焦点，需要在快照中也保存它，或者在 SaveData 中处理
            return True
        except IndexError:
            logging.warning("无法回滚：历史记录为空 (并发问题?)。")
            return False
        except Exception as e:
            logging.error(f"状态回滚时发生意外错误: {e}", exc_info=True)
            return False

    def commit_state(self):
        """清除所有历史记录，使当前状态成为新的基线"""
        logging.info("固化当前状态，清除历史记录...")
        self.history.clear()
        logging.info("历史记录已清除。")

    # --- 保存游戏 ---
    def save_game(self, filepath: Union[str, Path], conversation_log: List[Dict[str, Any]]):
        """保存当前游戏状态和历史到文件"""
        filepath = Path(filepath)
        logging.info(f"准备保存游戏到: {filepath}")
        try:
            # 将 deque 转换为 list 进行序列化
            world_history_list = list(self.history)
            save_data = SaveData(
                current_world=self.world,
                conversation_log=conversation_log,
                world_history=world_history_list,
                # 保存 GameState 的元数据，如焦点和 max_history
                game_state_metadata={
                    "max_history": self.max_history,
                    "user_focus": self.user_focus # 保存当前焦点
                 },
                metadata={
                    "save_time": datetime.now().isoformat(),
                    "game_version": "0.3.0" # 版本号示例
                }
            )
            # 使用 Pydantic 的 model_dump_json
            json_data = save_data.model_dump_json(indent=2)

            filepath.parent.mkdir(parents=True, exist_ok=True) # 确保目录存在
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(json_data)
            logging.info(f"游戏成功保存到: {filepath}")
            return True
        except Exception as e:
            logging.error(f"保存游戏到 '{filepath}' 失败: {e}", exc_info=True)
            return False

# --- 加载游戏 (放在 GameState 类外部或作为静态方法) ---
def load_game(filepath: Union[str, Path]) -> Optional[Tuple[GameState, List[Dict[str, Any]]]]:
    """从文件加载游戏状态和历史，返回新的 GameState 实例和对话记录"""
    filepath = Path(filepath)
    logging.info(f"尝试从文件加载游戏: {filepath}")
    if not filepath.is_file(): # 更准确的检查
        logging.error(f"加载失败：存档文件不存在或不是文件 '{filepath}'")
        return None
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            json_data = f.read()
        # 使用 Pydantic 的 model_validate_json
        save_data = SaveData.model_validate_json(json_data)

        # 从存档恢复 GameState 设置
        gs_metadata = save_data.game_state_metadata
        max_history = gs_metadata.get("max_history", 10) # 使用存档值或默认值
        user_focus = gs_metadata.get("user_focus", [])   # 恢复焦点

        # 创建新的 GameState 实例
        new_game_state = GameState(max_history=max_history)
        new_game_state.world = save_data.current_world
        new_game_state.user_focus = user_focus # 设置恢复的焦点

        # 恢复历史记录 deque
        history_deque = deque(maxlen=max_history)
        # 存档中 world_history[0] 是最近的快照
        for state in save_data.world_history:
             history_deque.appendleft(state) # 按存档顺序添加到左侧
        new_game_state.history = history_deque

        logging.info(f"游戏成功从 '{filepath}' 加载。历史记录 {len(history_deque)} 条，焦点: {user_focus}")
        return new_game_state, save_data.conversation_log
    except (json.JSONDecodeError, ValidationError) as e:
         logging.error(f"加载游戏失败：文件格式无效或损坏 '{filepath}'. 错误: {e}")
         return None
    except Exception as e:
         logging.error(f"加载游戏时发生意外错误 '{filepath}': {e}", exc_info=True)
         return None