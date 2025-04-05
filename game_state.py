# game_state.py
import logging
from typing import List, Optional, Dict, Any, Literal, Union, Set

import yaml  # 导入 YAML 库
from pydantic import BaseModel, Field, ValidationError


# --- 核心数据模型 ---

class BaseEntity(BaseModel):
    """所有游戏世界实体的基类"""
    entity_id: str = Field(..., description="实体的唯一英文ID，由AI指定或系统生成。格式建议：'kebab-case'")
    name: str = Field(..., description="实体的中文名称，用于向用户和AI展示")
    status: Dict[str, Any] = Field(default_factory=dict, description="实体的自定义状态字典 (描述, 效果, 属性等)")
    is_destroyed: bool = Field(False, description="标记实体是否已被销毁（软删除）")
    entity_type: Literal["Item", "Character", "Place"] = Field(..., description="实体类型标识")

    # 允许在模型实例化后访问计算出的字段，即使它们不是模型字段
    model_config = {"extra": "allow"}


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
        """将实体 ID 添加到新容器的列表中"""
        if not container_id:
            return
        container = self.find_entity(container_id)
        if not container:
            # 目标容器必须存在且未被销毁
            raise ValueError(f"无法添加到目标容器: 容器 '{container_id}' 不存在或已被销毁。")

        entity_to_add = self.find_entity(entity_id)
        if not entity_to_add:
            raise ValueError(f"无法添加不存在或已销毁的实体 '{entity_id}' 到容器 '{container_id}'。")

        if isinstance(container, Character):
            if entity_to_add.entity_type != "Item":
                raise TypeError(
                    f"不能将非物品实体 '{entity_id}' ({entity_to_add.entity_type}) 添加到角色 '{container_id}' 的 has_items。")
            if entity_id not in container.has_items:
                container.has_items.append(entity_id)
                logging.debug(f"物品 '{entity_id}' 添加到角色 '{container_id}'")
        elif isinstance(container, Place):
            # 允许添加 Item 或 Character
            if entity_id not in container.contents:
                container.contents.append(entity_id)
                logging.debug(f"实体 '{entity_id}' ({entity_to_add.entity_type}) 添加到地点 '{container_id}'")
        else:  # Item 不能作为容器
            raise TypeError(f"目标实体 '{container_id}' 类型为 Item，不能作为容器。")

    def execute_create(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str,
                       params: Dict[str, Any]):
        """执行 Create 指令"""
        existing_entity = self.find_entity(entity_id, include_destroyed=True)
        if existing_entity:
            logging.warning(
                f"Create 指令警告: 实体 ID '{entity_id}' 已存在 ({'已销毁' if existing_entity.is_destroyed else '活动'}). 将覆盖。")

        # 分离核心属性和 status 属性
        core_params = {}
        # --- 处理 status 可能不是字典的情况 ---
        status_input = params.pop("status", {})  # 从解析结果中提取 status
        status_params: Dict[str, Any] = {}
        if isinstance(status_input, dict):
            status_params = status_input
        elif status_input is not None:  # 如果 status 不是字典但有值 (例如 "hungry")
            # 将其包装成 {"status": value}
            status_params = {"status": status_input}
            logging.info(
                f"Create 指令 (ID: {entity_id}): 检测到非字典 status 输入 '{status_input}'，已转换为 {{'status': '{status_input}'}}")
        # --- 结束 status 处理 ---

        # 根据类型确定核心字段并处理 Item 堆叠
        model_class: type[BaseEntity]
        if entity_type == "Item":
            model_class = Item
            location = params.get("location")
            name = params.get("name")
            if location and name:
                target_container = self.find_entity(location)
                if target_container:
                    existing_item_id = None
                    container_items = []
                    if isinstance(target_container, Character):
                        container_items = target_container.has_items
                    elif isinstance(target_container, Place):
                        container_items = target_container.contents

                    for item_or_content_id in container_items:
                        item = self.find_entity(item_or_content_id)
                        if item and isinstance(item, Item) and item.name == name:
                            existing_item_id = item_or_content_id
                            break

                    if existing_item_id:
                        quantity_to_add = params.get("quantity", 1)
                        logging.info(
                            f"物品堆叠: 在 '{location}' 找到同名物品 '{name}' (ID: {existing_item_id})。增加数量 {quantity_to_add} 而不是创建新物品 '{entity_id}'。")
                        self.execute_modify("Item", existing_item_id, {"quantity": f"+{quantity_to_add}"})
                        return  # 阻止创建新物品

        elif entity_type == "Character":
            model_class = Character
        elif entity_type == "Place":
            model_class = Place
            # 原型阶段，测试代码——Place生成后自动聚焦
            # TODO 移除测试代码
            self.world.user_focus.append(entity_id)
        else:
            raise ValueError(f"无效的实体类型: {entity_type}")

        # 提取核心字段，剩余放入 status (现在 status_params 肯定是字典了)
        model_fields = model_class.model_fields.keys()
        for key, value in params.items():
            if key in model_fields and key != 'status':  # 确保不覆盖处理过的 status
                core_params[key] = value
            else:
                status_params[key] = value  # 其他未知参数放入 status

        # 创建实体对象
        try:
            entity_data = {
                "entity_id": entity_id,
                "entity_type": entity_type,
                **core_params,
                "status": status_params,  # 使用处理/转换后的 status_params
            }
            new_entity = model_class(**entity_data)
        except ValidationError as e:
            logging.error(f"创建实体 '{entity_id}' 失败: Pydantic 验证错误: {e}")
            raise e

        self._add_entity(new_entity)

        initial_container_id = None
        if isinstance(new_entity, Item):
            initial_container_id = new_entity.location
        elif isinstance(new_entity, Character):
            initial_container_id = new_entity.current_place

        if initial_container_id:
            try:
                self._add_to_container(new_entity.entity_id, initial_container_id)
            except (ValueError, TypeError) as e:
                logging.warning(f"创建实体 '{entity_id}' 时未能添加到初始容器 '{initial_container_id}': {e}")

    def execute_modify(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str,
                       updates: Dict[str, Any]):
        """
        执行 Modify 指令。
        - 根据值的格式判断替换或调整。
        - 特殊处理 key="status" 的情况，将其操作应用到 entity.status['status']。
        """
        entity = self.find_entity(entity_id)
        if not entity:
            raise ValueError(f"Modify 指令错误: 找不到实体 ID '{entity_id}'")
        if entity.entity_type != entity_type:
            raise TypeError(
                f"Modify 指令错误: 实体 '{entity_id}' 类型为 {entity.entity_type}, 但指令指定为 {entity_type}")

        logging.info(f"修改实体 '{entity_id}': {updates}")

        model_fields = entity.model_fields.keys()
        old_location_or_place: Optional[str] = None
        if isinstance(entity, Item):
            old_location_or_place = entity.location
        elif isinstance(entity, Character):
            old_location_or_place = entity.current_place

        for key, raw_value in updates.items():
            op: Optional[str] = None
            value_to_process: Any = raw_value

            # 1. 解析操作符和基础值
            if isinstance(raw_value, str):
                potential_ops = ['+=', '-=', '*=', '/=']
                found_op = None
                for p_op in potential_ops:
                    if raw_value.startswith(p_op): found_op = p_op; break
                if not found_op and raw_value.startswith('+'):
                    found_op = '+'
                elif not found_op and raw_value.startswith('-'):
                    found_op = '-'

                if found_op:
                    op = found_op
                    value_str = raw_value[len(op):].strip()
                    try:
                        # 尝试转换数值部分
                        if op in ['+', '-']:
                            # 如果是 '+' 或 '-'，它们本身可能构成数字，尝试整体转换
                            value_to_process = float(f"{op}{value_str}")
                            op = None  # 值已带符号，后续按替换处理（加法）
                        else:  # 对于 +=, -= 等，转换操作符后的部分
                            value_to_process = float(value_str)
                    except ValueError:
                        # 转换数值失败，视为字符串处理
                        if op == '+=':
                            value_to_process = value_str  # 字符串追加
                        elif op == '+':
                            op = '+='; value_to_process = value_str  # '+' 视为字符串追加
                        else:
                            logging.warning(
                                f"Modify 指令警告: 操作符 '{op}' 不能用于字符串值 '{value_str}' (键: '{key}')。跳过此修改。"); continue
            # else: raw_value 不是字符串，op 保持 None, value_to_process 是原始值

            # 2. 特殊处理 key == "status"
            if key == "status":
                if isinstance(value_to_process, dict) and op is None:
                    # a) 如果值是字典且无操作符: 替换整个 entity.status
                    entity.status = value_to_process
                    logging.debug(f"  整个 status 字典被替换为: {value_to_process}")
                else:
                    # b) 否则 (值非字典 或 有操作符): 操作 entity.status 字典内的 "status" 键
                    target_status_key = "status"  # 目标键名固定为 "status"
                    current_inner_status_value = entity.status.get(target_status_key)  # 获取 status['status'] 的当前值

                    if op:  # --- 调整 status['status'] ---
                        # (这里的逻辑与下面处理普通键的调整逻辑基本一致)
                        new_inner_value = None
                        if isinstance(current_inner_status_value, (int, float)) and isinstance(value_to_process,
                                                                                               (int, float)):
                            effective_op = op  # op 此时不会是 None，因为带符号数字已处理
                            if effective_op == '+=':
                                new_inner_value = current_inner_status_value + value_to_process
                            elif effective_op == '-=':
                                new_inner_value = current_inner_status_value - value_to_process
                            elif effective_op == '*=':
                                new_inner_value = current_inner_status_value * value_to_process
                            elif effective_op == '/=':
                                if value_to_process == 0: logging.warning(
                                    f"Modify: 尝试对 status['{target_status_key}'] 除以零。"); continue
                                new_inner_value = current_inner_status_value / value_to_process
                            logging.debug(
                                f"  调整 status['{target_status_key}'] {effective_op} {value_to_process} -> {new_inner_value}")

                        elif isinstance(current_inner_status_value, str) and op == '+=' and isinstance(value_to_process,
                                                                                                       str):
                            new_inner_value = current_inner_status_value + value_to_process
                            logging.debug(
                                f"  追加 status['{target_status_key}'] += '{value_to_process}' -> '{new_inner_value}'")

                        elif current_inner_status_value is None:  # status['status'] 原本不存在
                            if isinstance(value_to_process, (int, float)) or (
                                    op == '+=' and isinstance(value_to_process, str)):
                                new_inner_value = value_to_process  # 直接使用调整值作为初始值
                                logging.debug(
                                    f"  新增 status['{target_status_key}'] 通过调整设置为 '{new_inner_value}'")
                            else:
                                logging.warning(
                                    f"Modify: 无法对不存在的 status['{target_status_key}'] 应用操作 '{op}' 与值 '{value_to_process}'。");
                                continue
                        else:  # 类型不匹配
                            logging.warning(
                                f"Modify: 无法对类型为 {type(current_inner_status_value)} 的 status['{target_status_key}'] 执行操作 '{op}' 使用值 '{value_to_process}'。");
                            continue

                        entity.status[target_status_key] = new_inner_value  # 更新 status['status']

                    else:  # --- 替换 status['status'] (op is None) ---
                        entity.status[target_status_key] = value_to_process  # value_to_process 可能是解析后的数字或原始值
                        logging.debug(f"  状态属性 status['{target_status_key}'] 设置为 '{value_to_process}'")

                continue  # 处理完 key="status"，跳到下一个 key

            # 3. 处理其他 key (核心属性或其他 status 字典内的键)
            target_is_core = key in model_fields and key != "status"  # 明确排除 status

            if op:  # --- 调整/追加其他属性 ---
                current_value = None
                target_dict_or_obj = None
                actual_key = key

                if target_is_core:
                    current_value = getattr(entity, key, None)
                    target_dict_or_obj = entity
                else:  # 目标是 status 字典内的其他键
                    current_value = entity.status.get(key, None)
                    target_dict_or_obj = entity.status

                new_value = None  # 计算或追加后的新值
                if isinstance(current_value, (int, float)) and isinstance(value_to_process, (int, float)):
                    effective_op = op  # op 此时不为 None
                    if effective_op == '+=':
                        new_value = current_value + value_to_process
                    elif effective_op == '-=':
                        new_value = current_value - value_to_process
                    elif effective_op == '*=':
                        new_value = current_value * value_to_process
                    elif effective_op == '/=':
                        if value_to_process == 0: logging.warning(f"Modify: 尝试对 '{key}' 除以零。"); continue
                        new_value = current_value / value_to_process
                    logging.debug(
                        f"  调整 {'核心' if target_is_core else '状态'} 属性 '{key}' {effective_op} {value_to_process} -> {new_value}")

                elif isinstance(current_value, str) and op == '+=' and isinstance(value_to_process, str):
                    new_value = current_value + value_to_process
                    logging.debug(
                        f"  追加 {'核心' if target_is_core else '状态'} 字符串属性 '{key}' += '{value_to_process}' -> '{new_value}'")

                elif current_value is None and not target_is_core:  # 新增 status 键
                    if isinstance(value_to_process, (int, float)) or (op == '+=' and isinstance(value_to_process, str)):
                        new_value = value_to_process
                        logging.debug(f"  新增状态属性 '{key}' 通过调整设置为 '{new_value}'")
                    else:
                        logging.warning(
                            f"Modify: 无法对不存在的 status 属性 '{key}' 应用操作 '{op}' 与值 '{value_to_process}'。");
                        continue
                else:  # 类型不匹配
                    logging.warning(
                        f"Modify: 无法对类型为 {type(current_value)} 的属性 '{key}' 执行操作 '{op}' 使用值 '{value_to_process}'。");
                    continue

                # 应用调整后的值
                if target_is_core:
                    try:
                        expected_type = entity.model_fields[key].annotation
                        if isinstance(expected_type, type) and not isinstance(new_value,
                                                                              expected_type): new_value = expected_type(
                            new_value)
                        setattr(target_dict_or_obj, actual_key, new_value)
                    except (TypeError, ValueError):
                        logging.warning(f"Modify: 调整后 '{key}' 的值类型转换失败。"); continue
                else:
                    target_dict_or_obj[actual_key] = new_value

            else:  # --- 替换其他属性 (op is None) ---
                value_to_set = value_to_process  # 可能是带符号数字或原始值
                if target_is_core:
                    try:
                        expected_type = entity.model_fields[key].annotation
                        if isinstance(expected_type, type) and not isinstance(value_to_set,
                                                                              expected_type): value_to_set = expected_type(
                            value_to_set)
                        setattr(entity, key, value_to_set)
                        logging.debug(f"  核心属性 '{key}' 设置为 '{value_to_set}'")
                    except (TypeError, ValueError):
                        logging.warning(f"Modify: 替换时 '{key}' 的值类型转换失败。"); continue
                else:  # 替换 status 字典内的键
                    entity.status[key] = value_to_set
                    logging.debug(f"  状态属性 '{key}' 设置为 '{value_to_set}'")

        # --- 处理位置变更 (循环外处理，逻辑不变) ---
        new_location_or_place: Optional[str] = None
        if isinstance(entity, Item) and "location" in updates:
            new_location_or_place = getattr(entity, "location")
            if old_location_or_place != new_location_or_place:
                self._update_container_lists(entity_id, old_location_or_place, new_location_or_place)
        elif isinstance(entity, Character) and "current_place" in updates:
            new_location_or_place = getattr(entity, "current_place")
            if old_location_or_place != new_location_or_place:
                self._update_container_lists(entity_id, old_location_or_place, new_location_or_place)

    def execute_destroy(self, entity_type: Literal["Item", "Character", "Place"], entity_id: str):
        """执行 Destroy 指令"""
        entity = self.find_entity(entity_id, include_destroyed=True)  # 查找包括已销毁的，避免重复操作
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
        container_id = None
        if isinstance(entity, Item):
            container_id = entity.location
            entity.location = None  # 清除位置信息
        elif isinstance(entity, Character):
            container_id = entity.current_place
            entity.current_place = None  # 清除位置信息
            # TODO: 角色销毁时，其背包里的物品何去何从？原型阶段可以先不管，让物品的 location 保持指向已销毁的角色ID，或者也设为 None
            # for item_id in entity.has_items:
            #     item = self.find_entity(item_id)
            #     if item: item.location = None # 或者转移到角色所在地点？
            # entity.has_items = []

        elif isinstance(entity, Place):
            # TODO: 地点销毁时，其内容物何去何从？原型阶段可以先不管
            # for content_id in entity.contents:
            #    content = self.find_entity(content_id)
            #    if content and isinstance(content, Item): content.location = None
            #    if content and isinstance(content, Character): content.current_place = None
            # entity.contents = []
            pass  # 暂时不处理地点内容物

        if container_id:
            self._remove_from_container(entity_id, container_id)

    def execute_transfer(self, entity_type: Literal["Item", "Character"], entity_id: str, target_id: str):
        """执行 Transfer 指令"""
        entity = self.find_entity(entity_id)
        if not entity:
            raise ValueError(f"Transfer 指令错误: 找不到要转移的实体 ID '{entity_id}'")
        if entity.entity_type != entity_type:
            raise TypeError(
                f"Transfer 指令错误: 实体 '{entity_id}' 类型为 {entity.entity_type}, 但指令指定为 {entity_type}")

        target_container = self.find_entity(target_id)
        if not target_container:
            raise ValueError(f"Transfer 指令错误: 找不到目标容器 ID '{target_id}'")

        logging.info(f"转移实体 '{entity_id}' ({entity.entity_type}) 到 '{target_id}' ({target_container.entity_type})")

        old_container_id: Optional[str] = None

        # 检查类型匹配并更新实体的位置属性
        if isinstance(entity, Item):
            if not isinstance(target_container, (Character, Place)):
                raise TypeError(
                    f"Transfer 指令错误: 物品 '{entity_id}' 不能转移到类型为 {target_container.entity_type} 的目标 '{target_id}'")
            old_container_id = entity.location
            entity.location = target_id
        elif isinstance(entity, Character):
            if not isinstance(target_container, Place):
                raise TypeError(
                    f"Transfer 指令错误: 角色 '{entity_id}' 只能转移到地点 (Place)，目标 '{target_id}' 类型为 {target_container.entity_type}")
            old_container_id = entity.current_place
            entity.current_place = target_id

        # 更新旧容器和新容器的列表
        self._update_container_lists(entity_id, old_container_id, target_id)

    def _update_container_lists(self, entity_id: str, old_container_id: Optional[str], new_container_id: Optional[str]):
        """原子地更新旧容器和新容器的内容列表"""
        if old_container_id == new_container_id:
            return  # 没有移动

        # 从旧容器移除
        if old_container_id:
            try:
                self._remove_from_container(entity_id, old_container_id)
            except Exception as e:
                logging.error(f"从旧容器 '{old_container_id}' 移除 '{entity_id}' 时出错: {e}")
                # 原型阶段继续执行

        # 添加到新容器
        if new_container_id:
            try:
                self._add_to_container(entity_id, new_container_id)
            except (ValueError, TypeError) as e:
                logging.error(f"将实体 '{entity_id}' 添加到新容器 '{new_container_id}' 时出错: {e}")
                # 移动失败，是否需要回滚？原型阶段不处理回滚，记录错误。
                # 实体的位置属性可能已更新，但容器列表可能不一致
                # 也许应该先尝试添加到新容器，成功后再从旧容器移除？
                # 或者，应该在 GameState 方法中处理这种事务性？暂时保持简单。

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

    def get_state_summary(self) -> str:
        """
        生成基于用户焦点的 YAML 格式的世界状态摘要。
        - 摘要中的实体属性是扁平化的。
        - **过滤掉 is_destroyed=True 的实体。**
        """
        summary_data: Dict[str, Any] = {
            "focused_entities": [],
            "world_slice": {"places": {}}
        }
        relevant_place_ids: Set[str] = set()
        focused_chars_not_in_places: Set[str] = set()

        # --- 1. 处理显式焦点 ---
        current_focus = self.get_current_focus()
        # 先过滤掉已销毁的焦点实体本身
        active_focus_ids = [fid for fid in current_focus if not (entity := self.find_entity(fid)) or not entity.is_destroyed]
        summary_data["focused_entities"] = active_focus_ids

        for entity_id in active_focus_ids: # 只处理活跃的焦点
            entity = self.find_entity(entity_id) # find_entity 默认不返回 destroyed
            if not entity: continue # 如果 find_entity 返回 None (即使在 active_focus_ids 中也可能因竞争条件等发生)

            if isinstance(entity, Place):
                # 确保地点本身未被销毁 (find_entity 已保证，但双重检查无妨)
                if not entity.is_destroyed:
                    relevant_place_ids.add(entity_id)
            elif isinstance(entity, Character):
                 # 确保角色本身未被销毁
                if not entity.is_destroyed:
                    if entity.current_place:
                        place = self.find_entity(entity.current_place) # 检查地点是否存在且未销毁
                        if place and not place.is_destroyed:
                            relevant_place_ids.add(entity.current_place)
                        else: # 角色在一个无效或已销毁的地点
                            focused_chars_not_in_places.add(entity_id)
                    else: # 角色没有地点信息
                        focused_chars_not_in_places.add(entity_id)
            elif isinstance(entity, Item):
                 # 确保物品本身未被销毁
                 if not entity.is_destroyed and entity.location:
                     container = self.find_entity(entity.location) # 检查容器是否存在且未销毁
                     if container and not container.is_destroyed:
                         if isinstance(container, Place):
                             relevant_place_ids.add(container.entity_id)
                         elif isinstance(container, Character):
                             # 检查拥有物品的角色是否未销毁 (已由 container and not container.is_destroyed 保证)
                             if container.current_place:
                                 char_place = self.find_entity(container.current_place) # 检查角色地点是否有效
                                 if char_place and not char_place.is_destroyed:
                                     relevant_place_ids.add(container.current_place)

        # --- 2. 构建地点为根的 world_slice (扁平化 + 过滤) ---
        places_slice = summary_data["world_slice"]["places"]
        for place_id in relevant_place_ids: # relevant_place_ids 已包含活跃地点
            place = self.find_entity(place_id)
            # 双重检查，确保地点有效且未销毁
            if not place or not isinstance(place, Place) or place.is_destroyed: continue

            place_data: Dict[str, Any] = {"name": place.name}
            if place.status: place_data.update(place.status)
            place_data["characters"] = {}
            place_data["items"] = {}

            for content_id in place.contents:
                content_entity = self.find_entity(content_id) # 获取内容实体
                # **关键过滤**: 确保内容实体存在且未被销毁
                if not content_entity or content_entity.is_destroyed: continue

                if isinstance(content_entity, Character):
                    char_data: Dict[str, Any] = {"name": content_entity.name}
                    if content_entity.status: char_data.update(content_entity.status)
                    char_data["items"] = {}

                    for item_id in content_entity.has_items:
                        item = self.find_entity(item_id) # 获取物品
                        # **关键过滤**: 确保物品存在且未被销毁
                        if not item or not isinstance(item, Item) or item.is_destroyed: continue

                        item_summary: Dict[str, Any] = {"name": item.name, "quantity": item.quantity}
                        if item.status: item_summary.update(item.status)
                        char_data["items"][item_id] = item_summary

                    place_data["characters"][content_id] = char_data
                    if content_id in focused_chars_not_in_places:
                        focused_chars_not_in_places.remove(content_id)

                elif isinstance(content_entity, Item):
                    # 物品已在循环开始时通过 find_entity 和 is_destroyed 检查过滤
                    item_summary: Dict[str, Any] = {"name": content_entity.name, "quantity": content_entity.quantity}
                    if content_entity.status: item_summary.update(content_entity.status)
                    place_data["items"][content_id] = item_summary

            # 只添加有实际内容的地点（可选，如果地点为空也想显示，则移除此判断）
            # if place_data["characters"] or place_data["items"]:
            places_slice[place_id] = place_data


        # --- 3. 处理没有确定地点的聚焦角色 (过滤) ---
        active_unplaced_chars = [cid for cid in focused_chars_not_in_places if not (char := self.find_entity(cid)) or not char.is_destroyed]

        if active_unplaced_chars:
            unplaced_chars_data = {}
            for char_id in active_unplaced_chars: # 只处理活跃的无地点角色
                 char = self.find_entity(char_id)
                 if not char or not isinstance(char, Character) or char.is_destroyed: continue # 双重检查

                 char_data: Dict[str, Any] = {"name": char.name}
                 if char.status: char_data.update(char.status)
                 char_data["items"] = {}
                 for item_id in char.has_items:
                     item = self.find_entity(item_id) # 获取物品
                     # **关键过滤**: 确保物品存在且未被销毁
                     if not item or not isinstance(item, Item) or item.is_destroyed: continue

                     item_summary: Dict[str, Any] = {"name": item.name, "quantity": item.quantity}
                     if item.status: item_summary.update(item.status)
                     char_data["items"][item_id] = item_summary
                 unplaced_chars_data[char_id] = char_data

            if unplaced_chars_data:
                 summary_data["world_slice"]["unplaced_focused_characters"] = unplaced_chars_data

        # --- 4. 转换为 YAML 字符串 (逻辑不变) ---
        try:
            # 移除空的顶层键，让 YAML 更干净
            if not summary_data["world_slice"]["places"]:
                del summary_data["world_slice"]["places"]
            if "unplaced_focused_characters" in summary_data["world_slice"] and not summary_data["world_slice"]["unplaced_focused_characters"]:
                del summary_data["world_slice"]["unplaced_focused_characters"]
            if not summary_data["world_slice"]:
                 del summary_data["world_slice"]
            if not summary_data["focused_entities"]:
                 del summary_data["focused_entities"]

            if not summary_data: # 如果最终摘要为空
                 return "当前焦点区域无可见内容。" # 或者返回空字符串或特定消息

            yaml_string = yaml.dump(summary_data, Dumper=yaml.Dumper, default_flow_style=False, allow_unicode=True, sort_keys=False)
            return yaml_string
        except Exception as e:
            logging.error(f"生成 YAML 摘要时出错: {e}")
            return f"Error generating YAML summary: {e}"
