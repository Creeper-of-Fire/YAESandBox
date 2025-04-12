# workflows/processing/translator.py
import logging
from typing import List, Dict, Any, Tuple, Literal, Optional, cast, Set

from core.types import TypedID, EntityType
from core.world_state import WorldState, BaseEntity, Item, Character, Place, AnyEntity # 需要实体类型用于检查

# 导入 DTO 模型用于构建请求体 (虽然不直接实例化，但结构要匹配)
# from api.dtos import AttributeOperation # 可以不导入，直接构建字典

# --- 辅助函数：获取容器的内容属性键 ---
def _get_container_content_key(container: Optional[AnyEntity]) -> Optional[str]:
    """根据容器实体类型获取其存储内容的属性名。"""
    if isinstance(container, Place):
        return 'contents'
    elif isinstance(container, Character):
        return 'has_items'
    else:
        return None

# --- 辅助函数：生成占位符创建 API 调用 ---
def _generate_placeholder_creation_call(ref: TypedID, context: str) -> Dict[str, Any]:
    """为不存在的引用生成创建占位符的 API 调用描述。"""
    entity_type = ref.type
    entity_id = ref.id
    warning_prefix = "Warning: Missing"
    placeholder_name = f"{warning_prefix} {entity_type} [{entity_id}] (Auto-created by: {context})"
    logging.info(f"[Translator] 生成占位符创建调用 for '{ref}' from context '{context}'")
    return {
        "method": "POST",
        "path": "/api/entities",
        "json_body": {
            "entity_type": entity_type,
            "entity_id": entity_id,
            "initial_attributes": {
                "name": {"op": "=", "value": placeholder_name},
                # 占位符默认没有位置等信息
            }
        }
    }

# --- 辅助函数：递归查找值中的 TypedID 并生成占位符（如果需要） ---
def _find_and_generate_placeholders(
        value: Any,
        world: WorldState,
        placeholders_to_create: Set[TypedID], # 使用集合跟踪需要创建的占位符
        context: str
) -> None:
    """
    递归检查值中的 TypedID 引用，如果实体不存在，则将其添加到待创建占位符集合中。
    """
    if isinstance(value, TypedID):
        # 检查实体是否存在 (包括已销毁的，因为我们可能引用一个刚销毁的实体?)
        # 暂时只检查未销毁的，如果需要引用已销毁的再说
        # 注意：这里使用 world.find_entity_by_id
        if not world.find_entity_by_id(value.id, value.type, include_destroyed=False):
            if value not in placeholders_to_create:
                 logging.warning(f"[Translator] 引用实体 '{value}' 不存在 (Context: {context})，将计划创建占位符。")
                 placeholders_to_create.add(value)
    elif isinstance(value, list):
        for i, item in enumerate(value):
            _find_and_generate_placeholders(item, world, placeholders_to_create, f"{context}[{i}]")
    elif isinstance(value, dict):
        for k, v in value.items():
            # 假设键不是引用，只检查值
            _find_and_generate_placeholders(v, world, placeholders_to_create, f"{context}['{k}']")
    # 其他类型 (str, int, bool, None, etc.) 不包含引用，直接忽略

# --- 修改：translate_parsed_command ---
def translate_parsed_command(
        parsed_cmd: Dict[str, Any],
        world: WorldState, # <--- 接收 WorldState
        placeholders_to_create: Set[TypedID] # <--- 接收并修改待创建占位符集合
) -> List[Dict[str, Any]]:
    """
    将单个解析后的命令字典翻译成一个或多个原子 API 调用描述字典。
    包含占位符创建和关系维护逻辑。
    每个描述字典包含: 'method', 'path', 'json_body' (可选)。
    """
    command_name = parsed_cmd.get("command", "").lower()
    # 使用 TypedID 创建实体引用
    try:
        source_ref = TypedID(type=cast(EntityType, parsed_cmd.get("entity_type")), id=parsed_cmd.get("entity_id"))
    except Exception as e:
         logging.error(f"翻译命令失败：无法从命令构建源 TypedID。Cmd: {parsed_cmd}, Error: {e}")
         return []

    # params 是 Dict[str, Tuple[str, Any]]
    params = cast(Dict[str, Tuple[str, Any]], parsed_cmd.get("params", {}))

    api_calls: List[Dict[str, Any]] = []
    context_prefix = f"Translate {command_name.capitalize()} {source_ref}"

    # --- 1. 检查所有参数值中的引用，收集需要创建的占位符 ---
    for key, (op, value) in params.items():
        _find_and_generate_placeholders(value, world, placeholders_to_create, f"{context_prefix} param '{key}'")

    # --- 2. 根据命令类型生成主要 API 调用和关系维护调用 ---
    if command_name == "create":
        # 翻译成: POST /entities
        # 检查源实体本身是否已存在 (可能是一个占位符或已被销毁)
        existing_entity = world.find_entity(source_ref, include_destroyed=True)
        if existing_entity and not existing_entity.is_destroyed:
             logging.warning(f"[{context_prefix}] 尝试创建已存在的实体，将翻译为覆盖 (PATCH)。")
             # --- 转为 Modify 逻辑 ---
             attributes_to_patch: Dict[str, Dict[str, Any]] = {}
             for key, (op, value) in params.items():
                 # Create 命令理论上只包含 '='，但我们兼容处理所有操作符用于覆盖
                 attributes_to_patch[key] = {"op": op, "value": value}
             if attributes_to_patch:
                 api_calls.append({
                     "method": "PATCH",
                     "path": f"/api/entities/{source_ref.type}/{source_ref.id}",
                     "json_body": {"attributes": attributes_to_patch}
                 })
             # --- 需要处理位置变更的关系维护 (如果 params 里有 location/current_place) ---
             location_change_params = {k: v for k, v in params.items() if k in ('location', 'current_place')}
             if location_change_params and existing_entity: # 只有覆盖时才需要获取旧位置
                 old_location_ref: Optional[TypedID] = None
                 new_location_ref: Optional[TypedID] = None
                 location_key = 'location' if source_ref.type == "Item" else 'current_place' if source_ref.type == "Character" else None

                 if location_key and location_key in location_change_params:
                     op, new_loc_val = location_change_params[location_key]
                     if op == '=' and isinstance(new_loc_val, TypedID):
                         new_location_ref = new_loc_val
                         # 从现有实体获取旧位置
                         old_location_ref = existing_entity.get_attribute(location_key) # 应该是 TypedID 或 None

                 # 如果位置真的改变了，生成关系维护调用
                 if location_key and old_location_ref != new_location_ref:
                     relationship_calls = _generate_relationship_update_calls(
                         source_ref, location_key, old_location_ref, new_location_ref, world, placeholders_to_create, context_prefix
                     )
                     api_calls.extend(relationship_calls)
             # --- 结束 Modify 转换 ---

        else: # 实体不存在或已销毁，执行创建
             initial_attrs_ops: Dict[str, Dict[str, Any]] = {}
             for key, (op, value) in params.items():
                 initial_attrs_ops[key] = {"op": op, "value": value}

             api_calls.append({
                 "method": "POST",
                 "path": "/api/entities",
                 "json_body": {
                     "entity_type": source_ref.type,
                     "entity_id": source_ref.id,
                     "initial_attributes": initial_attrs_ops if initial_attrs_ops else None
                 }
             })
             # --- 创建时也需要处理位置关系 (从无到有) ---
             location_change_params = {k: v for k, v in params.items() if k in ('location', 'current_place')}
             if location_change_params:
                 new_location_ref: Optional[TypedID] = None
                 location_key = 'location' if source_ref.type == "Item" else 'current_place' if source_ref.type == "Character" else None

                 if location_key and location_key in location_change_params:
                     op, new_loc_val = location_change_params[location_key]
                     if op == '=' and isinstance(new_loc_val, TypedID):
                         new_location_ref = new_loc_val

                 # 如果设置了新位置，生成关系维护调用 (old_location 为 None)
                 if location_key and new_location_ref:
                     relationship_calls = _generate_relationship_update_calls(
                         source_ref, location_key, None, new_location_ref, world, placeholders_to_create, context_prefix
                     )
                     api_calls.extend(relationship_calls)


    elif command_name == "modify":
        # 翻译成: PATCH /entities/{entity_type}/{entity_id}
        # 先获取实体以确定旧状态
        source_entity = world.find_entity(source_ref, include_destroyed=False)
        if not source_entity:
            logging.error(f"[{context_prefix}] 翻译 Modify 命令失败：源实体不存在或已销毁。")
            # 是否应该尝试创建占位符？Modify 通常作用于已知实体，这里报错更合适
            return []

        attributes_to_patch: Dict[str, Dict[str, Any]] = {}
        location_key_modified: Optional[str] = None
        old_location_ref: Optional[TypedID] = None
        new_location_ref: Optional[TypedID] = None

        for key, (op, value) in params.items():
            attributes_to_patch[key] = {"op": op, "value": value}
            # --- 检查是否修改了位置属性 ---
            if key in ('location', 'current_place'):
                 # 确保类型匹配
                 is_item_loc = key == 'location' and source_entity.entity_type == "Item"
                 is_char_place = key == 'current_place' and source_entity.entity_type == "Character"
                 if is_item_loc or is_char_place:
                    if op == '=' and isinstance(value, TypedID):
                         location_key_modified = key
                         new_location_ref = value
                         # 从当前实体状态获取旧位置
                         old_location_ref = source_entity.get_attribute(key)
                    elif op == '=' and value is None: # 允许设置位置为 None
                        location_key_modified = key
                        new_location_ref = None
                        old_location_ref = source_entity.get_attribute(key)
                    elif op != '=':
                        logging.error(f"[{context_prefix}] 位置属性 '{key}' 只支持 '=' 操作符。")
                        return [] # 出错，停止翻译

        if attributes_to_patch: # 只有在有属性修改时才发送主 PATCH 请求
            api_calls.append({
                "method": "PATCH",
                "path": f"/api/entities/{source_ref.type}/{source_ref.id}",
                "json_body": {"attributes": attributes_to_patch}
            })

            # --- 如果位置改变，生成关系维护调用 ---
            if location_key_modified and old_location_ref != new_location_ref:
                relationship_calls = _generate_relationship_update_calls(
                    source_ref, location_key_modified, old_location_ref, new_location_ref, world, placeholders_to_create, context_prefix
                )
                api_calls.extend(relationship_calls)
        else:
             logging.warning(f"[{context_prefix}] Modify 命令没有找到任何参数，不生成 API 调用。")


    elif command_name == "transfer":
        # 翻译成修改 location/current_place 的 PATCH，并进行关系维护
        source_entity = world.find_entity(source_ref, include_destroyed=False)
        if not source_entity:
            logging.error(f"[{context_prefix}] 翻译 Transfer 命令失败：源实体不存在或已销毁。")
            return []

        target_op_value = params.get('target')
        if not target_op_value or target_op_value[0] != '=':
             logging.error(f"[{context_prefix}] 翻译 Transfer 命令失败：缺少有效的 'target' 参数 (op='=')。")
             return []

        target_value = target_op_value[1] # 解析后的值，应该是 TypedID
        if not isinstance(target_value, TypedID):
            logging.error(f"[{context_prefix}] 翻译 Transfer 命令失败：目标值格式错误，需要 TypedID 对象，得到 {type(target_value)}。")
            return []

        # 确定要修改的源实体属性名 和 旧位置
        location_key: Optional[str] = None
        old_location_ref: Optional[TypedID] = None
        if source_entity.entity_type == "Item":
            location_key = "location"
            old_location_ref = source_entity.get_attribute(location_key)
            # 验证目标类型
            if target_value.type not in ["Place", "Character"]:
                 logging.error(f"[{context_prefix}] Transfer Item 失败：目标类型必须是 Place 或 Character，得到 {target_value.type}")
                 return []
        elif source_entity.entity_type == "Character":
            location_key = "current_place"
            old_location_ref = source_entity.get_attribute(location_key)
             # 验证目标类型
            if target_value.type != "Place":
                 logging.error(f"[{context_prefix}] Transfer Character 失败：目标类型必须是 Place，得到 {target_value.type}")
                 return []
        else: # Place 不能被 Transfer
             logging.error(f"[{context_prefix}] Transfer 命令不支持实体类型 '{source_entity.entity_type}'。")
             return []

        # 检查目标引用是否存在，如果不存在，添加到占位符列表
        _find_and_generate_placeholders(target_value, world, placeholders_to_create, f"{context_prefix} target")

        new_location_ref = target_value

        # --- 生成主 PATCH 调用 ---
        attributes_to_patch = {
            location_key: {"op": "=", "value": new_location_ref}
        }
        api_calls.append({
            "method": "PATCH",
            "path": f"/api/entities/{source_ref.type}/{source_ref.id}",
            "json_body": {"attributes": attributes_to_patch}
        })

        # --- 生成关系维护调用 ---
        if old_location_ref != new_location_ref:
            relationship_calls = _generate_relationship_update_calls(
                source_ref, location_key, old_location_ref, new_location_ref, world, placeholders_to_create, context_prefix
            )
            api_calls.extend(relationship_calls)

    elif command_name == "destroy":
        # 翻译成: DELETE /entities/{entity_type}/{entity_id}
        # --- 在销毁前，需要先从其所在容器中移除 ---
        source_entity = world.find_entity(source_ref, include_destroyed=False) # 查找未销毁的
        if source_entity:
            location_key = None
            if isinstance(source_entity, Item): location_key = 'location'
            elif isinstance(source_entity, Character): location_key = 'current_place'

            if location_key:
                old_location_ref: Optional[TypedID] = source_entity.get_attribute(location_key)
                if old_location_ref:
                    # 生成从旧容器移除的调用 (新位置为 None)
                    # 注意：这里 new_location_ref 设为 None，但我们只关心旧容器的移除
                    relationship_calls = _generate_relationship_update_calls(
                        source_ref, location_key, old_location_ref, None, world, placeholders_to_create, context_prefix
                    )
                    api_calls.extend(relationship_calls)
                    # 理论上还应该更新源实体的 location 为 None，但既然要销毁了，似乎可以省略？
                    # 为了干净，还是加一个吧
                    # api_calls.append({
                    #     "method": "PATCH",
                    #     "path": f"/api/entities/{source_ref.type}/{source_ref.id}",
                    #     "json_body": {"attributes": {location_key: {"op": "=", "value": None}}}
                    # })

        # --- 然后生成 DELETE 调用 ---
        api_calls.append({
            "method": "DELETE",
            "path": f"/api/entities/{source_ref.type}/{source_ref.id}"
            # DELETE 请求通常没有 body
        })
    else:
        logging.warning(f"[{context_prefix}] 未知的命令类型 '{command_name}' 无法翻译。Cmd: {parsed_cmd}")

    return api_calls

# --- 新增辅助函数：生成关系维护 API 调用 ---
def _generate_relationship_update_calls(
    moving_entity_ref: TypedID,
    location_key: str, # 'location' or 'current_place'
    old_container_ref: Optional[TypedID],
    new_container_ref: Optional[TypedID],
    world: WorldState,
    placeholders_to_create: Set[TypedID],
    context: str
) -> List[Dict[str, Any]]:
    """
    生成用于更新旧容器和新容器内容列表的 PATCH API 调用描述。
    """
    calls: List[Dict[str, Any]] = []
    logging.debug(f"[{context}] Generating relationship updates: {moving_entity_ref} moved from {old_container_ref} to {new_container_ref}")

    # --- 1. 从旧容器移除 ---
    if old_container_ref:
        old_container = world.find_entity(old_container_ref, include_destroyed=False)
        if old_container:
            content_key = _get_container_content_key(old_container)
            if content_key:
                logging.debug(f"[{context}] Adding call to remove {moving_entity_ref} from {old_container_ref}'s '{content_key}'")
                calls.append({
                    "method": "PATCH",
                    "path": f"/api/entities/{old_container_ref.type}/{old_container_ref.id}",
                    "json_body": {
                        "attributes": {
                            content_key: {"op": "-", "value": moving_entity_ref}
                        }
                    }
                })
            else:
                logging.warning(f"[{context}] 旧容器 {old_container_ref} 类型不支持内容列表，无法移除。")
        else:
            # 如果旧容器在当前 world 状态找不到 (可能刚被销毁?)，记录警告但继续
            logging.warning(f"[{context}] 旧容器 {old_container_ref} 在查找时未找到，无法生成移除调用。")

    # --- 2. 添加到新容器 ---
    if new_container_ref:
        # 检查新容器是否存在，如果不存在，确保已计划创建占位符
        _find_and_generate_placeholders(new_container_ref, world, placeholders_to_create, f"{context} target container")

        # 获取新容器实体（可能是占位符，没关系，API会处理）
        # 注意：这里我们 *不* 直接从 world 获取 new_container 实体，因为可能还没创建
        # 我们只基于 new_container_ref 的类型来判断 content_key
        content_key = None
        target_type_ok = False
        if new_container_ref.type == "Place":
            content_key = 'contents'
            if moving_entity_ref.type in ["Item", "Character"]: target_type_ok = True
        elif new_container_ref.type == "Character":
            content_key = 'has_items'
            if moving_entity_ref.type == "Item": target_type_ok = True

        if content_key and target_type_ok:
            logging.debug(f"[{context}] Adding call to add {moving_entity_ref} to {new_container_ref}'s '{content_key}'")
            calls.append({
                "method": "PATCH",
                "path": f"/api/entities/{new_container_ref.type}/{new_container_ref.id}",
                "json_body": {
                    "attributes": {
                        content_key: {"op": "+", "value": moving_entity_ref}
                    }
                }
            })
        elif not target_type_ok:
            # 这个错误应该在更早的阶段（如 Transfer 命令解析）被捕获，但这里再加一层保险
            logging.error(f"[{context}] 类型不匹配：不能将 {moving_entity_ref.type} 移动到 {new_container_ref.type}")
            # 也许应该抛出异常？或者只记录错误并返回空列表？
            # return [] # 返回空列表表示关系更新失败
        else: # 目标类型不是 Place 或 Character
             logging.warning(f"[{context}] 目标容器 {new_container_ref} 类型不支持内容列表，无法添加。")


    return calls


# --- 修改：_sort_api_calls_for_execution ---
# 当前排序逻辑 POST(0) > PATCH(1) > DELETE(2) 仍然适用，因为：
# - 占位符创建 (POST) 需要在引用它的 PATCH 之前。
# - 关系清理 (PATCH) 需要在实体销毁 (DELETE) 之前。
# - 主要的 PATCH 操作（如修改属性、设置位置）与关系维护 PATCH 之间的顺序由 translator 内部决定（通常主操作在前）。
def _sort_api_calls_for_execution(api_calls: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    """
    对原子 API 调用进行排序，确保大致的依赖关系。
    POST (Create Placeholder) > PATCH (Modify/Transfer/Update Relationships) > DELETE (Destroy)。
    """
    def get_priority(call_desc: Dict[str, Any]) -> int:
        method = call_desc.get("method", "").upper()
        path = call_desc.get("path", "")
        # 提高占位符创建的优先级? 但 POST 本身优先级最高，应该够了
        # json_body = call_desc.get("json_body", {})
        # if method == "POST" and "initial_attributes" in json_body and "name" in json_body["initial_attributes"] and json_body["initial_attributes"]["name"]["value"].startswith("Warning: Missing"):
        #    return -1 # 最高优先级

        if method == "POST": return 0 # Create / Create Placeholder
        if method == "PATCH": return 1 # Modify/Transfer/Update Relationships
        if method == "DELETE": return 2 # Destroy
        return 99 # 其他未知方法排最后

    # 使用稳定排序可能更好，以保留由 translate_parsed_command 内部生成的顺序
    # 但 Python 的 sort 默认是稳定的
    return sorted(api_calls, key=get_priority)


# --- 修改：translate_all_commands ---
def translate_all_commands(
        parsed_commands: List[Dict[str, Any]],
        world: WorldState # <--- 接收 WorldState
) -> List[Dict[str, Any]]:
    """
    将所有解析后的命令翻译成原子 API 调用描述列表，并进行排序。
    管理占位符创建。
    """
    all_api_calls: List[Dict[str, Any]] = []
    placeholders_to_create: Set[TypedID] = set() # 跟踪本轮需要创建的占位符

    # 第一次遍历：翻译命令，收集占位符需求和主要/关系调用
    temp_api_calls: List[Dict[str, Any]] = []
    for cmd in parsed_commands:
        # 传递 world 和 placeholders_to_create 集合
        translated = translate_parsed_command(cmd, world, placeholders_to_create)
        temp_api_calls.extend(translated)

    # 第二步：为收集到的所有占位符生成创建调用
    placeholder_creation_calls: List[Dict[str, Any]] = []
    if placeholders_to_create:
         logging.info(f"[Translator] 需要创建 {len(placeholders_to_create)} 个占位符: {placeholders_to_create}")
         for ref in placeholders_to_create:
             # 使用更通用的上下文
             placeholder_call = _generate_placeholder_creation_call(ref, context="Referenced Entity")
             placeholder_creation_calls.append(placeholder_call)

    # 合并占位符创建调用和翻译后的调用
    all_api_calls = placeholder_creation_calls + temp_api_calls

    # 对所有生成的 API 调用进行最终排序
    sorted_api_calls = _sort_api_calls_for_execution(all_api_calls)
    logging.info(f"已将 {len(parsed_commands)} 条指令翻译为 {len(sorted_api_calls)} 个原子 API 调用 (包括 {len(placeholder_creation_calls)} 个占位符创建)。")
    # Debug: 打印排序后的调用顺序
    # for i, call in enumerate(sorted_api_calls):
    #    logging.debug(f"Sorted Call #{i+1}: {call['method']} {call['path']}")
    return sorted_api_calls