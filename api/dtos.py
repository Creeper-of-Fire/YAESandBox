# api/dtos.py
import logging
from pydantic import BaseModel, Field, ValidationError, TypeAdapter, model_validator  # 导入 TypeAdapter (Pydantic V2)
from typing import Dict, Any, Literal, Optional, List, Union, Tuple

# 从新的类型模块导入 TypedID
from core.types import TypedID, EntityType, _try_convert_dict_to_typedid

# 导入 core 中的基类用于类型提示和访问 _CORE_FIELDS
from core.world_state import BaseEntity

# --- 数据传输对象 (DTO) ---
class FlatEntityDTO(BaseModel):
    """
    用于 API 传输的实体数据结构 (扁平化)。
    核心字段 + 所有动态属性都在顶层。
    不包含 is_destroyed 或内部实现细节 (_dynamic_attributes)。
    值可以是基本类型、列表、字典，或 TypedID 实例。
    """
    entity_id: str = Field(..., description="实体的唯一ID")
    entity_type: EntityType = Field(..., description="实体类型") # 直接使用 EntityType

    # 使用 Pydantic V2 的 model_config 来允许额外的字段
    # Pydantic 在序列化时会自动将 TypedID 转为字典，反序列化时会保留字典
    model_config = {"extra": "allow"}

# --- 转换函数 ---
# 注意：这个函数现在返回的 DTO 的动态属性值可能是 TypedID 实例
# FastAPI/Pydantic 在返回响应时会自动将 TypedID 序列化为 JSON 字典
def entity_to_dto(entity: Optional[BaseEntity]) -> Optional[FlatEntityDTO]:
    if not entity or entity.is_destroyed: return None
    all_visible_attrs = entity.get_all_attributes()
    # dto_data 现在直接从 get_all_attributes 获取，它包含了核心字段
    dto_data: Dict[str, Any] = all_visible_attrs
    # 移除不需要的字段
    core_fields_to_exclude = {'is_destroyed'} # 只排除这个
    for key in core_fields_to_exclude: dto_data.pop(key, None)

    try:
        # 注意：这里创建 DTO 时，动态属性值可能是 TypedID 对象
        return FlatEntityDTO(**dto_data)
    except Exception as e:
        logging.error(f"从实体 {entity.typed_id} 创建 DTO 时出错: {e}", exc_info=True)
        raise RuntimeError(f"创建 DTO 失败 for entity {entity.typed_id}") from e

def entities_to_dtos(entities: List[Optional[BaseEntity]]) -> List[FlatEntityDTO]:
    dtos = []
    for entity in entities:
        dto = entity_to_dto(entity)
        if dto: dtos.append(dto)
    return dtos

# --- 原子化 API 请求体 DTOs ---

# --- 定义基本类型和 TypedID ---
PrimitiveValueType = Union[int, float, str, bool, None]
ReferenceValueType = TypedID

# --- 定义嵌套结构允许的类型 (不直接递归引用 AttributeValueType) ---
# 列表元素可以是基本类型、TypedID 或字典
NestedListValueType = Union[PrimitiveValueType, ReferenceValueType, Dict[str, Any]] # 字典的值设为 Any 避免深层递归
# 字典值可以是基本类型、TypedID 或列表
NestedDictValueType = Union[PrimitiveValueType, ReferenceValueType, List[Any]] # 列表的元素设为 Any 避免深层递归

# --- 最终的 AttributeValueType 定义 ---
AttributeValueType = Union[
    PrimitiveValueType,
    ReferenceValueType,
    List[NestedListValueType],  # 列表包含上面定义的类型
    Dict[str, NestedDictValueType] # 字典包含上面定义的类型
]

class AttributeOperation(BaseModel):
    """定义单个属性的操作和值，用于原子化修改。Value 可以是 TypedID 或包含 TypedID 的嵌套结构。"""
    op: Literal["=", "+=", "-=", "+", "-"] = Field(..., description="操作符")
    # value: Any = Field(..., description="操作的值") # 使用 Any 依赖后续验证
    # 或者使用更具体的类型，让 Pydantic 尝试自动反序列化 TypedID
    value: AttributeValueType = Field(..., description="操作的值 (可能包含 TypedID)")

    # --- 恢复：模型验证器 ---
    @model_validator(mode='after')
    def convert_value_dicts_to_typedids(self) -> 'AttributeOperation':
        if hasattr(self, 'value'):
            self.value = _try_convert_dict_to_typedid(self.value)
        return self

class AtomicCreateEntityRequest(BaseModel):
    """用于 POST /entities 的请求体。"""
    entity_type: EntityType = Field(..., description="要创建的实体类型")
    entity_id: str = Field(..., description="要创建的实体的 ID")
    # initial_attributes 的 value 现在可以是 TypedID
    initial_attributes: Optional[Dict[str, AttributeOperation]] = Field(None, description="可选的初始属性及操作")

class AtomicModifyEntityRequest(BaseModel):
    """用于 PATCH /entities/{type}/{id} 的请求体。"""
    # attributes 的 value 现在可以是 TypedID
    attributes: Dict[str, AttributeOperation] = Field(..., description="要修改的属性及其操作")

# --- 原子化 API 响应体 (如果需要特定响应) ---
# 对于 POST 和 PATCH，可以直接返回 FlatEntityDTO
# 对于 DELETE，可以返回 204 No Content，或一个简单的消息
class DeleteResponse(BaseModel):
     message: str