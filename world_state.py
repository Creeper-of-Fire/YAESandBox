# world_state.py
import logging
from typing import List, Optional, Dict, Any, Literal, Union, Set, Tuple, ClassVar, TYPE_CHECKING

from pydantic import BaseModel, Field, ValidationError, PrivateAttr

# --- 类型提示循环导入处理 ---
# 如果 WorldState 需要被实体类直接引用（现在需要了），我们需要这个
if TYPE_CHECKING:
    pass


# --- 核心数据模型 ---
class BaseEntity(BaseModel):
    """
    所有游戏世界实体的极简基类。
    只包含核心标识字段和动态属性字典。
    所有属性访问和修改通过方法进行。
    """
    entity_id: str = Field(...)
    entity_type: Literal["Item", "Character", "Place"] = Field(...)
    is_destroyed: bool = Field(False)

    # 使用 PrivateAttr 存储所有非核心字段
    _dynamic_attributes: Dict[str, Any] = PrivateAttr(default_factory=dict)

    model_config = {"validate_assignment": True, "extra": "forbid"}
    # 核心字段列表，这些字段直接存在于模型实例上
    _CORE_FIELDS: ClassVar[Set[str]] = {'entity_id', 'entity_type', 'is_destroyed'}

    # --- 获取/设置/修改/删除 属性的核心方法 (简化版) ---
    def get_attribute(self, key: str, default: Any = None) -> Any:
        """
        获取核心属性或动态属性。
        如果属性不存在，返回 default 值 (默认为 None)。
        """
        if key in self._CORE_FIELDS:
            return getattr(self, key)
        else:
            # 直接从动态属性获取，不存在则返回 default
            return self._dynamic_attributes.get(key, default)

    def has_attribute(self, key: str) -> bool:
        """检查实体是否具有某个属性（核心或动态）"""
        return key in self._CORE_FIELDS or key in self._dynamic_attributes

    def set_attribute(self, key: str, value: Any, world: Optional['WorldState'] = None):
        """
        设置核心属性（带基础验证）或动态属性。
        子类应覆盖此方法以添加特定验证和关系维护。
        接收可选的 world 参数，子类实现关系维护时需要。
        """
        if key in self._CORE_FIELDS:
            try:
                # 基础验证 is_destroyed
                if key == 'is_destroyed' and not isinstance(value, bool):
                    raise ValidationError.from_exception_data("Value must be a boolean",
                                                              [{'type': 'bool_type', 'loc': (key,), 'input': value}])
                # entity_id 和 entity_type 不建议修改，但允许以支持恢复
                setattr(self, key, value)
                logging.debug(f"实体 '{self.entity_id}': 设置核心属性 {key} = {repr(value)}")
            except ValidationError as e:
                logging.error(f"设置核心属性 '{key}' (值: {repr(value)}) 时验证失败: {e}")
                raise e
            except Exception as e:
                logging.error(f"设置核心属性 '{key}' 时发生未知错误: {e}", exc_info=True)
                raise e
        else:
            # 基类不再进行 quantity 验证，直接设置动态属性
            self._dynamic_attributes[key] = value
            logging.debug(f"实体 '{self.entity_id}': 设置动态属性 {key} = {repr(value)}")

    def delete_attribute(self, key: str) -> bool:
        """
        删除动态属性，核心属性不可删除。
        移除恢复默认值的逻辑。
        """
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

    def get_all_attributes(self, exclude_internal: Set[str] = {'_dynamic_attributes', 'model_config'}) -> Dict[str, Any]:
        """
        获取实体所有用户可见属性的字典（核心字段 + 动态属性）。
        移除对 _STRUCTURE_FIELDS 的特殊处理。创建列表/字典的副本。
        """
        all_attrs = {}
        # 1. 添加核心字段 (带副本)
        for key in self._CORE_FIELDS:
            if key not in exclude_internal:
                value = getattr(self, key)
                if isinstance(value, list):
                    all_attrs[key] = list(value)
                elif isinstance(value, dict):
                    all_attrs[key] = dict(value)
                else:
                    all_attrs[key] = value

        # 2. 添加动态属性 (带副本)
        for key, value in self._dynamic_attributes.items():
            if isinstance(value, list):
                all_attrs[key] = list(value)
            elif isinstance(value, dict):
                all_attrs[key] = dict(value)
            else:
                all_attrs[key] = value

        # 3. 清理 Pydantic 特有字段
        for field in exclude_internal:
            all_attrs.pop(field, None)

        return all_attrs

    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any], world: Optional['WorldState'] = None):
        """
        修改实体属性，处理操作符。子类应覆盖以添加验证和关系维护。
        基类处理基本操作和删除。列表/字典操作委托给子类。
        接收可选的 world 参数。
        """
        op, value_to_process = opAndValue
        logging.debug(
            f"实体 '{self.entity_id}': 基类 modify_attribute: Key='{key}', Op='{op}', Value='{repr(value_to_process)}'")

        try:
            if op == '-':  # 删除操作优先处理
                deleted = self.delete_attribute(key)
                if not deleted and key not in self._CORE_FIELDS:
                    logging.warning(f"Modify({self.entity_id}): 尝试删除不存在的动态属性 '{key}'")
                return  # 删除操作完成

            # 获取当前值
            current_value = self.get_attribute(key)
            new_value: Any = None

            if op == '=':
                # 赋值操作直接调用 set_attribute (子类会处理验证和关系)
                self.set_attribute(key, value_to_process, world)
                return

            elif op in ('+=', '-='):
                # 基类只处理数值和字符串的 +=/-=
                if isinstance(current_value, (int, float)) and isinstance(value_to_process, (int, float)):
                    new_value = current_value + value_to_process if op == '+=' else current_value - value_to_process
                elif isinstance(current_value, str) and isinstance(value_to_process, str) and op == '+=':
                    new_value = current_value + value_to_process
                else:
                    # 对于列表、字典或其他类型，+=/-= 委托给子类的 set_attribute? 不，modify 更合适
                    # 或者引发错误，强制子类处理？原型阶段倾向于让子类处理
                    logging.debug(f"基类 modify_attribute 跳过非数值/字符串的 '{op}' 操作 for key '{key}' (类型: {type(current_value)})。子类应处理。")
                    # 这里不应该直接返回，而是让子类处理。但基类不知道怎么调用子类特定逻辑。
                    # 修改：如果子类没有覆盖，这里会报错或静默失败。子类必须覆盖 modify_attribute 来处理列表/字典等。
                    raise TypeError(f"基类不支持对类型 {type(current_value)} 执行 '{op}' 操作 for key '{key}'。子类需覆盖 modify_attribute。")

                # 如果计算出新值，调用 set_attribute (包含验证)
                self.set_attribute(key, new_value, world)

            elif op in ('+', '-'):  # +/- 主要用于列表合并/移除等，基类不处理
                logging.debug(f"基类 modify_attribute 跳过 '{op}' 操作 for key '{key}'。子类应处理。")
                raise TypeError(f"基类不支持 '{op}' 操作 for key '{key}'。子类需覆盖 modify_attribute。")

            else:
                raise ValueError(f"未知的修改操作符 '{op}' for key '{key}'")

        except (TypeError, ValueError, ValidationError) as e:
            logging.error(f"基类 Modify({self.entity_id}): 处理属性 '{key}' 失败: {e}", exc_info=True)
            raise e
        except Exception as e:
            logging.error(f"基类 Modify({self.entity_id}): 处理属性 '{key}' 时发生未知错误: {e}", exc_info=True)
            raise e

    # --- 内部辅助方法：更新容器关系 ---
    def _update_location_relationship(self, old_container_id: Optional[str], new_container_id: Optional[str], world: 'WorldState'):
        """
        内部辅助方法，用于在实体位置改变后，更新旧容器和新容器的状态。
        由子类的 set_attribute 或 modify_attribute 在位置改变时调用。
        """
        if old_container_id == new_container_id:
            return  # 位置未变

        logging.debug(f"实体 '{self.entity_id}': 更新容器关系: 从 '{old_container_id}' 到 '{new_container_id}'")

        # 从旧容器移除
        if old_container_id:
            old_container = world.find_entity(old_container_id)
            if old_container:
                content_key = None
                if isinstance(old_container, Character):
                    content_key = 'has_items'
                elif isinstance(old_container, Place):
                    content_key = 'contents'

                if content_key:
                    try:
                        # 使用 modify_attribute 从容器列表中移除自身 ID
                        logging.debug(f"尝试从旧容器 '{old_container_id}' ({content_key}) 移除 '{self.entity_id}'")
                        # 注意：这里传递 world=None 给容器的 modify，防止无限递归或意外副作用
                        old_container.modify_attribute(content_key, ('-=', self.entity_id), world=None)
                    except Exception as e:
                        # 即使移除失败（例如列表已不包含该ID），也继续尝试添加到新容器
                        logging.warning(f"从旧容器 '{old_container_id}' ({content_key}) 移除 '{self.entity_id}' 时出错 (可能已不在其中): {e}")
            else:
                logging.warning(f"旧容器 '{old_container_id}' 未找到，无法从中移除 '{self.entity_id}'。")

        # 添加到新容器
        if new_container_id:
            new_container = world.find_entity(new_container_id)
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
                        # 使用 modify_attribute 向容器列表添加自身 ID
                        logging.debug(f"尝试向新容器 '{new_container_id}' ({content_key}) 添加 '{self.entity_id}'")
                        # 传递 world=None
                        new_container.modify_attribute(content_key, ('+=', self.entity_id), world=None)
                    except Exception as e:
                        logging.error(f"向新容器 '{new_container_id}' ({content_key}) 添加 '{self.entity_id}' 时出错: {e}", exc_info=True)
                        # 如果添加失败，可能需要回滚位置设置？原型阶段暂不处理。
                        raise RuntimeError(f"未能将实体 '{self.entity_id}' 添加到容器 '{new_container_id}'") from e
                elif not target_type_ok:
                    logging.error(
                        f"类型不匹配：不能将 '{self.entity_type}' ({self.entity_id}) 添加到容器类型 '{new_container.entity_type}' ({new_container_id})")
                    raise TypeError(f"类型不匹配：不能将 {self.entity_type} 添加到 {new_container.entity_type}")

            else:
                logging.error(f"新容器 '{new_container_id}' 未找到，无法添加 '{self.entity_id}'。")
                # 这个错误比较严重，因为位置已经被设置了，但无法更新容器状态
                raise ValueError(f"新容器 '{new_container_id}' 不存在")


# --- 子类定义 (覆盖方法，实现验证和关系维护) ---

class Item(BaseEntity):
    entity_type: Literal["Item"] = "Item"

    # 移除属性包装器

    def set_attribute(self, key: str, value: Any, world: Optional['WorldState'] = None):
        """覆盖基类方法，添加 Item 特定验证和位置关系维护。"""
        logging.debug(f"Item '{self.entity_id}': set_attribute: Key='{key}', Value='{repr(value)}'")

        # --- 特定属性验证 ---
        if key == 'quantity':
            if not isinstance(value, int) or value < 0:
                logging.error(f"Item '{self.entity_id}': 无效的 quantity 值 {repr(value)}。必须是非负整数。")
                raise ValueError("Quantity 必须是非负整数")
            # 验证通过，继续

        elif key == 'location':
            # 验证 location 值格式 (可选，但推荐)
            # 允许 None, str("Place:id" 或 "Character:id") 或 Tuple('Place'|'Character', id)
            if value is not None:
                if isinstance(value, str):
                    if not re.fullmatch(r"(Place|Character):[\w\-]+", value, re.IGNORECASE):
                        raise ValueError(f"无效的 location 字符串格式: '{value}'")
                elif isinstance(value, tuple):
                    if not (len(value) == 2 and value[0] in ["Place", "Character"] and isinstance(value[1], str)):
                        raise ValueError(f"无效的 location 元组格式: {repr(value)}")
                else:
                    raise TypeError(f"无效的 location 类型: {type(value)}")

            # --- 位置关系维护 ---
            if world is None:
                logging.warning(f"Item '{self.entity_id}': 尝试设置 location 但缺少 world 对象，无法更新容器关系。")
                # 仅设置属性，不更新关系
                super().set_attribute(key, value, world)
            else:
                old_location = self.get_attribute('location', None)
                # 先调用基类设置属性
                super().set_attribute(key, value, world)
                # 再更新关系 (如果 location 真的改变了)
                new_location = self.get_attribute('location', None)  # 获取设置后的值
                if old_location != new_location:
                    try:
                        self._update_location_relationship(old_location, new_location, world)
                    except Exception as e:
                        # 如果更新关系失败，是否回滚 location 的设置？
                        logging.error(f"Item '{self.entity_id}': 更新位置关系失败，但 location 属性已设置为 '{new_location}'。错误: {e}", exc_info=True)
                        # 原型阶段让其失败
                        raise e

            return  # 位置处理完毕，直接返回

        # --- 对于其他非位置属性，调用基类方法 ---
        super().set_attribute(key, value, world)

    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any], world: Optional['WorldState'] = None):
        """覆盖基类方法，处理 Item 特定修改，尤其是 quantity 和列表/字典。"""
        op, value = opAndValue
        logging.debug(f"Item '{self.entity_id}': modify_attribute: Key='{key}', Op='{op}', Value='{repr(value)}'")

        # --- 处理 quantity ---
        if key == 'quantity':
            current_quantity = self.get_attribute('quantity', 1)  # 获取当前值，默认为 1
            new_quantity = None
            if op == '=':
                new_quantity = value
            elif op == '+=' and isinstance(value, int):
                new_quantity = current_quantity + value
            elif op == '-=' and isinstance(value, int):
                new_quantity = current_quantity - value
            elif op == '+' and isinstance(value, int):  # op '+' 意义不明，当作 +=
                logging.warning(f"Item '{self.entity_id}': quantity 使用 '+' 操作符，视为 '+='")
                new_quantity = current_quantity + value
            elif op == '-' and isinstance(value, int):  # op '-' 意义不明，当作 -=
                logging.warning(f"Item '{self.entity_id}': quantity 使用 '-' 操作符，视为 '-='")
                new_quantity = current_quantity - value
            # 删除操作 op == '-' 由基类处理

            if new_quantity is not None:
                # 验证新数量
                if not isinstance(new_quantity, int) or new_quantity < 0:
                    raise ValueError(f"Modify: 无效的 quantity 结果 {repr(new_quantity)} (来自 {current_quantity} {op} {value})")
                # 调用 set_attribute (它会进行最终的验证和设置)
                self.set_attribute(key, new_quantity, world)
                return  # quantity 处理完毕
            # 如果是删除操作，会由基类处理

        # --- 处理 location ---
        elif key == 'location':
            # 位置修改只有 = 和 - (删除)
            if op == '=':
                self.set_attribute(key, value, world)  # set_attribute 会处理关系
                return
            elif op == '-':  # 删除由基类处理
                pass  # 让基类处理 delete_attribute
            else:
                raise ValueError(f"Item location 不支持操作符 '{op}'")

        # --- 处理其他列表/字典属性 (示例) ---
        # 假设 Item 可能有 'ingredients': List[str] 属性
        elif key == 'ingredients' and isinstance(self.get_attribute(key), list):
            current_list = self.get_attribute(key, [])  # 获取当前列表，默认为空
            if op == '+=':
                # 直接修改列表
                current_list.append(value)
                logging.debug(f"Item '{self.entity_id}': 直接向 ingredients 添加: {repr(value)}")
                # 注意：没有调用 set_attribute，因为列表是可变的
                # 如果需要触发 pydantic 的验证或钩子，可能需要 super().set_attribute(key, current_list, world)
                return
            elif op == '-=':
                try:
                    current_list.remove(value)
                    logging.debug(f"Item '{self.entity_id}': 直接从 ingredients 移除: {repr(value)}")
                except ValueError:
                    logging.warning(f"Item '{self.entity_id}': 尝试从 ingredients 移除不存在元素 '{repr(value)}'")
                return
            elif op == '=':  # 赋值由 set_attribute 处理
                self.set_attribute(key, value, world)
                return
            # 其他操作符 +/- 由基类决定是否支持或报错

        # --- 对于基类能处理的操作或未知属性，调用基类方法 ---
        logging.debug(f"Item '{self.entity_id}': 将 modify_attribute 委托给基类 for key '{key}'")
        super().modify_attribute(key, opAndValue, world)


class Character(BaseEntity):
    entity_type: Literal["Character"] = "Character"

    # 移除属性包装器

    def set_attribute(self, key: str, value: Any, world: Optional['WorldState'] = None):
        """覆盖基类，添加 Character 特定验证和位置关系维护。"""
        logging.debug(f"Character '{self.entity_id}': set_attribute: Key='{key}', Value='{repr(value)}'")

        # --- 特定属性验证 ---
        if key == 'current_place':
            # 验证 current_place 格式
            if value is not None:
                if isinstance(value, str):
                    if not re.fullmatch(r"Place:[\w\-]+", value, re.IGNORECASE):
                        raise ValueError(f"无效的 current_place 字符串格式: '{value}'")
                elif isinstance(value, tuple):
                    if not (len(value) == 2 and value[0] == "Place" and isinstance(value[1], str)):
                        raise ValueError(f"无效的 current_place 元组格式: {repr(value)}")
                else:
                    raise TypeError(f"无效的 current_place 类型: {type(value)}")

            # --- 位置关系维护 ---
            if world is None:
                logging.warning(f"Character '{self.entity_id}': 尝试设置 current_place 但缺少 world 对象，无法更新容器关系。")
                super().set_attribute(key, value, world)
            else:
                old_place = self.get_attribute('current_place', None)
                super().set_attribute(key, value, world)
                new_place = self.get_attribute('current_place', None)
                if old_place != new_place:
                    try:
                        self._update_location_relationship(old_place, new_place, world)
                    except Exception as e:
                        logging.error(f"Character '{self.entity_id}': 更新位置关系失败，但 current_place 已设置为 '{new_place}'。错误: {e}", exc_info=True)
                        raise e
            return  # 位置处理完毕

        elif key == 'has_items':
            # 验证 has_items 必须是列表
            if not isinstance(value, list):
                raise TypeError("has_items 必须是一个列表")
            # 验证列表内元素是否为字符串 (可选)
            # for item_id in value:
            #     if not isinstance(item_id, str):
            #         raise TypeError(f"has_items 列表中的元素必须是字符串 ID: {repr(item_id)}")
            # 注意：直接设置 has_items 不会更新物品的 location，这符合“位置驱动”原则

        # --- 对于其他属性，调用基类方法 ---
        super().set_attribute(key, value, world)

    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any], world: Optional['WorldState'] = None):
        """覆盖基类，处理 Character 特定修改，尤其是 has_items。"""
        op, value = opAndValue
        logging.debug(f"Character '{self.entity_id}': modify_attribute: Key='{key}', Op='{op}', Value='{repr(value)}'")

        # --- 处理 current_place ---
        if key == 'current_place':
            if op == '=':
                self.set_attribute(key, value, world)  # set_attribute 会处理关系
                return
            elif op == '-':  # 删除由基类处理
                pass
            else:
                raise ValueError(f"Character current_place 不支持操作符 '{op}'")

        # --- 处理 has_items (列表操作) ---
        elif key == 'has_items':
            # 获取当前列表，确保是列表
            current_list = self.get_attribute(key, [])
            if not isinstance(current_list, list):
                logging.error(f"Character '{self.entity_id}': has_items 属性不是列表 (类型: {type(current_list)})，无法修改。")
                # 可以尝试强制设置为空列表？ super().set_attribute(key, [], world)
                raise TypeError("has_items 属性不是列表，无法修改")

            if op == '+=':  # 添加物品 ID
                # 验证 value 是否为字符串 ID (可选)
                if not isinstance(value, str):
                    raise TypeError(f"尝试向 has_items 添加非字符串值: {repr(value)}")
                if value not in current_list:  # 避免重复添加
                    current_list.append(value)
                    logging.debug(f"Character '{self.entity_id}': 直接向 has_items 添加: '{value}'")
                else:
                    logging.debug(f"Character '{self.entity_id}': 物品 '{value}' 已在 has_items 中，忽略 '+=' 操作。")
                # 注意：直接修改列表，不触发物品 location 更新
                return
            elif op == '-=':  # 移除物品 ID
                if not isinstance(value, str):
                    raise TypeError(f"尝试从 has_items 移除非字符串值: {repr(value)}")
                try:
                    current_list.remove(value)
                    logging.debug(f"Character '{self.entity_id}': 直接从 has_items 移除: '{value}'")
                except ValueError:
                    logging.warning(f"Character '{self.entity_id}': 尝试从 has_items 移除不存在元素 '{value}'")
                # 注意：直接修改列表，不触发物品 location 更新
                return
            elif op == '=':  # 覆盖整个列表
                self.set_attribute(key, value, world)  # set_attribute 会验证类型
                return
            # 其他操作符 +/- 由基类决定是否支持或报错

        # --- 对于基类能处理的操作或未知属性，调用基类方法 ---
        logging.debug(f"Character '{self.entity_id}': 将 modify_attribute 委托给基类 for key '{key}'")
        super().modify_attribute(key, opAndValue, world)


class Place(BaseEntity):
    entity_type: Literal["Place"] = "Place"

    # 移除属性包装器

    def set_attribute(self, key: str, value: Any, world: Optional['WorldState'] = None):
        """覆盖基类，添加 Place 特定验证。"""
        logging.debug(f"Place '{self.entity_id}': set_attribute: Key='{key}', Value='{repr(value)}'")

        # --- 特定属性验证 ---
        if key == 'contents':
            if not isinstance(value, list):
                raise TypeError("contents 必须是一个列表")
            # 验证列表内元素是否为字符串 (可选)
            # 注意：直接设置 contents 不会更新内部实体的 location

        elif key == 'exits':
            if not isinstance(value, dict):
                raise TypeError("exits 必须是一个字典")
            # 验证字典的键和值是否为字符串 (可选)
            # for direction, target_id in value.items():
            #     if not isinstance(direction, str) or not isinstance(target_id, str):
            #         raise TypeError(f"exits 字典的键和值必须是字符串: {repr({direction: target_id})}")
            # 进一步验证 target_id 格式 (可选)
            # if not re.fullmatch(r"Place:[\w\-]+", target_id, re.IGNORECASE):
            #     raise ValueError(f"exits 中的目标 ID 格式无效: '{target_id}'")

        # --- 对于其他属性，调用基类方法 ---
        super().set_attribute(key, value, world)

    def modify_attribute(self, key: str, opAndValue: Tuple[str, Any], world: Optional['WorldState'] = None):
        """覆盖基类，处理 Place 特定修改，尤其是 contents 和 exits。"""
        op, value = opAndValue
        logging.debug(f"Place '{self.entity_id}': modify_attribute: Key='{key}', Op='{op}', Value='{repr(value)}'")

        # --- 处理 contents (列表操作) ---
        if key == 'contents':
            current_list = self.get_attribute(key, [])
            if not isinstance(current_list, list):
                raise TypeError("contents 属性不是列表，无法修改")

            if op == '+=':  # 添加实体 ID
                if not isinstance(value, str): raise TypeError(f"尝试向 contents 添加非字符串值: {repr(value)}")
                if value not in current_list:
                    current_list.append(value)
                    logging.debug(f"Place '{self.entity_id}': 直接向 contents 添加: '{value}'")
                else:
                    logging.debug(f"Place '{self.entity_id}': 实体 '{value}' 已在 contents 中，忽略 '+=' 操作。")
                # 不触发内部实体 location 更新
                return
            elif op == '-=':  # 移除实体 ID
                if not isinstance(value, str): raise TypeError(f"尝试从 contents 移除非字符串值: {repr(value)}")
                try:
                    current_list.remove(value)
                    logging.debug(f"Place '{self.entity_id}': 直接从 contents 移除: '{value}'")
                except ValueError:
                    logging.warning(f"Place '{self.entity_id}': 尝试从 contents 移除不存在元素 '{value}'")
                # 不触发内部实体 location 更新
                return
            elif op == '=':  # 覆盖整个列表
                self.set_attribute(key, value, world)
                return

        # --- 处理 exits (字典操作) ---
        elif key == 'exits':
            current_dict = self.get_attribute(key, {})
            if not isinstance(current_dict, dict):
                raise TypeError("exits 属性不是字典，无法修改")

            if op == '=':  # 覆盖整个字典
                self.set_attribute(key, value, world)
                return
            elif op == '+=':  # 更新/添加键值对 (类似 dict.update)
                if not isinstance(value, dict):
                    raise TypeError("使用 '+=' 修改 exits 时，值必须是字典")
                current_dict.update(value)
                logging.debug(f"Place '{self.entity_id}': 直接更新 exits: {repr(value)}")
                # 可能需要调用 set_attribute 来触发验证？
                # self.set_attribute(key, current_dict, world) # 触发验证
                return
            elif op == '-=':  # 按键移除
                if not isinstance(value, str):  # 假设 -= 操作符后面跟的是要移除的键 (方向)
                    raise TypeError("使用 '-=' 修改 exits 时，值必须是字符串 (要移除的键)")
                if value in current_dict:
                    del current_dict[value]
                    logging.debug(f"Place '{self.entity_id}': 直接从 exits 移除键: '{value}'")
                else:
                    logging.warning(f"Place '{self.entity_id}': 尝试从 exits 移除不存在的键 '{value}'")
                return
            # 删除整个 exits 属性由基类的 delete_attribute 处理 (op == '-')

        # --- 对于基类能处理的操作或未知属性，调用基类方法 ---
        logging.debug(f"Place '{self.entity_id}': 将 modify_attribute 委托给基类 for key '{key}'")
        super().modify_attribute(key, opAndValue, world)


AnyEntity = Union[Item, Character, Place]

# --- WorldState (添加 re 导入，其他保持不变) ---
import re  # 需要 re 用于 Item/Character 的 location/current_place 字符串格式验证


class WorldState(BaseModel):
    items: Dict[str, Item] = Field(default_factory=dict)
    characters: Dict[str, Character] = Field(default_factory=dict)
    places: Dict[str, Place] = Field(default_factory=dict)
    model_config = {'validate_assignment': True}

    def find_entity(self, entity_id: str, include_destroyed: bool = False) -> Optional[AnyEntity]:
        """通过 ID 查找任何类型的实体"""
        entity = self.items.get(entity_id) or self.characters.get(entity_id) or self.places.get(entity_id)
        if entity and (not entity.is_destroyed or include_destroyed):
            return entity
        return None

    def find_entity_by_name(self, name: str, entity_type: Optional[Literal["Item", "Character", "Place"]] = None,
                            include_destroyed: bool = False) -> Optional[AnyEntity]:
        """按名称查找实体（效率较低），使用 entity.get_attribute('name')"""
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
                # 使用 get_attribute 获取名称
                entity_name = entity.get_attribute('name')
                if entity_name == name and (not entity.is_destroyed or include_destroyed):
                    return entity
        return None

    def get_entity_dict(self, entity_type: Literal["Item", "Character", "Place"]) -> Dict[str, AnyEntity]:
        """获取对应实体类型的字典"""
        if entity_type == "Item":
            return self.items  # type: ignore
        elif entity_type == "Character":
            return self.characters  # type: ignore
        elif entity_type == "Place":
            return self.places  # type: ignore
        else:
            raise ValueError(f"未知的实体类型: {entity_type}")

    def add_entity(self, entity: AnyEntity):
        """添加实体到世界状态，会覆盖同 ID 的实体"""
        entity_dict = self.get_entity_dict(entity.entity_type)
        if entity.entity_id in entity_dict and not entity_dict[entity.entity_id].is_destroyed:
            logging.warning(f"覆盖已存在且未销毁的实体: {entity.entity_type} ID='{entity.entity_id}'")
        entity_dict[entity.entity_id] = entity
        logging.debug(f"实体 '{entity.entity_id}' ({entity.entity_type}) 已添加到 WorldState。")
