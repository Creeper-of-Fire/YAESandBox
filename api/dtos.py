# api/dtos.py
import logging
from pydantic import BaseModel, Field
from typing import Dict, Any, Literal, Optional, List, Union, Tuple # 添加 Tuple

# 导入 core 中的基类用于类型提示和访问 _CORE_FIELDS
from core.world_state import BaseEntity

# --- 数据传输对象 (DTO) ---
class FlatEntityDTO(BaseModel):
    """
    用于 API 传输的实体数据结构 (扁平化)。
    核心字段 + 所有动态属性都在顶层。
    不包含 is_destroyed 或内部实现细节 (_dynamic_attributes)。
    """
    entity_id: str = Field(..., description="实体的唯一ID")
    entity_type: Literal["Item", "Character", "Place"] = Field(..., description="实体类型")

    # 使用 Pydantic V2 的 model_config 来允许额外的字段
    # 这些额外的字段将代表实体的动态属性
    model_config = {"extra": "allow"}

# --- 转换函数 (保持不变) ---
def entity_to_dto(entity: Optional[BaseEntity]) -> Optional[FlatEntityDTO]:
    if not entity or entity.is_destroyed: return None
    all_visible_attrs = entity.get_all_attributes()
    dto_data: Dict[str, Any] = {"entity_id": entity.entity_id, "entity_type": entity.entity_type}
    core_fields_to_exclude = BaseEntity._CORE_FIELDS
    for key, value in all_visible_attrs.items():
        if key not in core_fields_to_exclude: dto_data[key] = value
    try: return FlatEntityDTO(**dto_data)
    except Exception as e:
        logging.error(f"从实体 {entity.entity_id} 创建 DTO 时出错: {e}", exc_info=True)
        raise RuntimeError(f"创建 DTO 失败 for entity {entity.entity_id}") from e

def entities_to_dtos(entities: List[Optional[BaseEntity]]) -> List[FlatEntityDTO]:
    dtos = []
    for entity in entities:
        dto = entity_to_dto(entity)
        if dto: dtos.append(dto)
    return dtos

# --- 命令执行请求/响应 (旧) ---
class CommandExecutionRequest(BaseModel):
    text: str = Field(..., description="包含 @Command 指令的文本")

class CommandExecutionResponse(BaseModel):
    message: str
    executed_commands: int
    total_commands: int
    errors: Optional[str] = None

# --- 原子化 API 请求体 DTOs ---

class AttributeOperation(BaseModel):
    """定义单个属性的操作和值，用于原子化修改。"""
    op: Literal["=", "+=", "-=", "+", "-"] = Field(..., description="操作符")
    value: Any = Field(..., description="操作的值") # Pydantic 会自动处理嵌套类型

class AtomicCreateEntityRequest(BaseModel):
    """用于 POST /entities 的请求体。"""
    entity_type: Literal["Item", "Character", "Place"] = Field(..., description="要创建的实体类型")
    entity_id: str = Field(..., description="要创建的实体的 ID")
    initial_attributes: Optional[Dict[str, AttributeOperation]] = Field(None, description="可选的初始属性及操作")

class AtomicModifyEntityRequest(BaseModel):
    """用于 PATCH /entities/{type}/{id} 的请求体。"""
    attributes: Dict[str, AttributeOperation] = Field(..., description="要修改的属性及其操作")

# --- 原子化 API 响应体 (如果需要特定响应) ---
# 对于 POST 和 PATCH，可以直接返回 FlatEntityDTO
# 对于 DELETE，可以返回 204 No Content，或一个简单的消息
class DeleteResponse(BaseModel):
     message: str