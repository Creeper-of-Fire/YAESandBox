# processing/translator.py
import logging
from typing import List, Dict, Any, Tuple, Literal, Optional, cast

from core.types import TypedID

# 导入实体类型用于提示
EntityType = Literal["Item", "Character", "Place"]

# 导入 DTO 模型用于构建请求体 (虽然不直接实例化，但结构要匹配)
# from api.dtos import AttributeOperation # 可以不导入，直接构建字典

def _get_target_attribute_for_transfer(entity_type: EntityType) -> Optional[str]:
    """根据源实体类型获取转移时要修改的目标属性名。"""
    if entity_type == "Item": return "location"
    if entity_type == "Character": return "current_place"
    return None

def translate_parsed_command(parsed_cmd: Dict[str, Any]) -> List[Dict[str, Any]]:
    """
    将单个解析后的命令字典翻译成一个或多个原子 API 调用描述字典。
    每个描述字典包含: 'method', 'path', 'json_body' (可选)。
    """
    command_name = parsed_cmd.get("command", "").lower()
    entity_type = cast(EntityType, parsed_cmd.get("entity_type"))
    entity_id = parsed_cmd.get("entity_id")
    # params 是 Dict[str, Tuple[str, Any]]
    params = cast(Dict[str, Tuple[str, Any]], parsed_cmd.get("params", {}))

    api_calls: List[Dict[str, Any]] = []

    if not entity_type or not entity_id:
        logging.error(f"翻译命令失败：缺少 entity_type 或 entity_id。Cmd: {parsed_cmd}")
        return [] # 返回空列表表示无法翻译

    if command_name == "create":
        # 翻译成: POST /entities
        initial_attrs_ops: Dict[str, Dict[str, Any]] = {}
        for key, (op, value) in params.items():
            # Create 命令理论上只包含 '=' 操作，但我们兼容处理
            initial_attrs_ops[key] = {"op": op, "value": value}

        api_calls.append({
            "method": "POST",
            "path": "/api/entities",
            "json_body": {
                "entity_type": entity_type,
                "entity_id": entity_id,
                "initial_attributes": initial_attrs_ops if initial_attrs_ops else None
            }
        })

    elif command_name == "modify":
        # 翻译成: PATCH /entities/{entity_type}/{entity_id}
        attributes_to_patch: Dict[str, Dict[str, Any]] = {}
        for key, (op, value) in params.items():
            attributes_to_patch[key] = {"op": op, "value": value}

        if attributes_to_patch: # 只有在有属性修改时才发送请求
            api_calls.append({
                "method": "PATCH",
                "path": f"/api/entities/{entity_type}/{entity_id}",
                "json_body": {"attributes": attributes_to_patch}
            })
        else:
             logging.warning(f"翻译 Modify 命令 '{entity_type}:{entity_id}' 时没有找到任何参数，不生成 API 调用。")


    elif command_name == "transfer":
        # 翻译成: PATCH /entities/{entity_type}/{entity_id} 来修改 location/current_place
        target_op_value = params.get('target')
        if not target_op_value or target_op_value[0] != '=':
             logging.error(f"翻译 Transfer 命令 '{entity_type}:{entity_id}' 失败：缺少有效的 'target' 参数。")
             return []

        target_value = target_op_value[1] # 解析后的值，应该是 (Type, ID) 元组
        target_attr_name = _get_target_attribute_for_transfer(entity_type)

        if not target_attr_name:
             logging.error(f"翻译 Transfer 命令 '{entity_type}:{entity_id}' 失败：无法确定源类型的目标属性。")
             return []
        if not isinstance(target_value, TypedID):  # 确保是 TypedID 对象
            logging.error(f"翻译 Transfer 命令 '{entity_type}:{entity_id}' 失败：目标值格式错误，需要 TypedID 对象，得到 {type(target_value)}。")
            return []


        attributes_to_patch = {
            target_attr_name: {"op": "=", "value": target_value}
        }
        api_calls.append({
            "method": "PATCH",
            "path": f"/api/entities/{entity_type}/{entity_id}",
            "json_body": {"attributes": attributes_to_patch}
        })

    elif command_name == "destroy":
        # 翻译成: DELETE /entities/{entity_type}/{entity_id}
        api_calls.append({
            "method": "DELETE",
            "path": f"/api/entities/{entity_type}/{entity_id}"
            # DELETE 请求通常没有 body
        })
    else:
        logging.warning(f"未知的命令类型 '{command_name}' 无法翻译。Cmd: {parsed_cmd}")

    return api_calls

def _sort_api_calls_for_execution(api_calls: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    """
    对原子 API 调用进行排序，确保大致的依赖关系。
    例如：POST (Create) > PATCH (Modify/Transfer) > DELETE (Destroy)。
    同一类型内部可能不需要严格排序，因为原子操作依赖性降低了。
    """
    def get_priority(call_desc: Dict[str, Any]) -> int:
        method = call_desc.get("method", "").upper()
        if method == "POST": return 0 # Create
        if method == "PATCH": return 1 # Modify/Transfer
        if method == "DELETE": return 2 # Destroy
        return 99

    return sorted(api_calls, key=get_priority)


def translate_all_commands(parsed_commands: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    """
    将所有解析后的命令翻译成原子 API 调用描述列表，并进行排序。
    """
    all_api_calls: List[Dict[str, Any]] = []
    for cmd in parsed_commands:
        all_api_calls.extend(translate_parsed_command(cmd))

    # 对所有生成的 API 调用进行排序
    sorted_api_calls = _sort_api_calls_for_execution(all_api_calls)
    logging.info(f"已将 {len(parsed_commands)} 条指令翻译为 {len(sorted_api_calls)} 个原子 API 调用。")
    return sorted_api_calls