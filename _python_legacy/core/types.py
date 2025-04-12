# core/types.py
from pydantic import BaseModel, Field, field_validator, model_serializer, ValidationError  # <--- 只导入 field_validator
from typing import Literal, Any, cast, Dict
import re # <--- 在顶部导入 re
import logging # <--- 在顶部导入 logging

# 定义允许的实体类型字面量
EntityType = Literal["Item", "Character", "Place"]

# --- 添加：递归转换字典到 TypedID 的辅助函数 ---
def _try_convert_dict_to_typedid(data: Any) -> Any:
    """递归地尝试将 {'type': ..., 'id': ...} 字典转换为 TypedID 对象。"""
    if isinstance(data, dict):
        # 检查是否看起来像 TypedID 的序列化形式
        if "type" in data and "id" in data and len(data) == 2:
            try:
                # 尝试创建 TypedID 实例
                return TypedID(**data)
            except ValidationError:
                pass # 如果不是有效的 TypedID，保持为字典
        # 递归处理字典的值
        return {k: _try_convert_dict_to_typedid(v) for k, v in data.items()}
    elif isinstance(data, list):
        # 递归处理列表的元素
        return [_try_convert_dict_to_typedid(item) for item in data]
    else:
        return data

class TypedID(BaseModel):
    """
    用于表示带有类型的实体 ID 的专用类。
    提供了类型安全和验证。
    """
    type: EntityType = Field(..., description="实体类型")
    id: str = Field(..., min_length=1, description="实体 ID (非空字符串)")
    model_config = {"frozen": True}  # <--- 推荐：使 TypedID 不可变，更适合做标识符和字典键

    # --- 正确的 JSON 序列化准备 (Pydantic V2) ---
    # 当 TypedID 需要被转换为可序列化的 Python 对象时调用
    # (默认 mode='plain', when_used='always')
    @model_serializer
    def serialize_to_dict(self) -> Dict[str, str]:
        """将 TypedID 转换为标准的 Python 字典，以便后续序列化 (如转为 JSON)。"""
        # 这个字典会被外部的 JSON 编码器处理
        return {"type": self.type, "id": self.id}

    # --- Pydantic V2 验证器语法 ---
    @field_validator('id')
    @classmethod # <--- 确保有 @classmethod
    def id_must_not_contain_special_chars(cls, value: str):
        # 可以添加更严格的 ID 格式验证，例如只允许字母、数字、连字符
        if not re.match(r'^[\w\-]+$', value):
             raise ValueError("ID 只能包含字母、数字和连字符 '-'")
        return value

    # 为了方便使用，可以重写 __hash__ 和 __eq__
    def __hash__(self):
        return hash((self.type, self.id))

    def __eq__(self, other: Any):
        if isinstance(other, TypedID):
            return self.type == other.type and self.id == other.id
        # 允许与元组比较 (可选，用于过渡)
        if isinstance(other, tuple) and len(other) == 2:
             logging.warning(f"比较 TypedID 与元组: {self} == {other}") # 添加警告，鼓励使用 TypedID
             return self.type == other[0] and self.id == other[1]
        return NotImplemented

    # 提供一个方便的字符串表示
    def __str__(self):
        return f"{self.type}:{self.id}"

    def __repr__(self):
        return f"TypedID(type='{self.type}', id='{self.id}')"

    # 添加一个方便从字符串解析的方法 (可选)
    @classmethod
    def from_string(cls, value: str) -> 'TypedID':
        ENTITY_REF_REGEX_LOCAL = re.compile(r"^(Item|Character|Place):([\w\-]+)$", re.IGNORECASE)
        match = ENTITY_REF_REGEX_LOCAL.match(value)
        if not match:
            raise ValueError(f"无效的 TypedID 字符串格式: '{value}'")
        entity_type = match.group(1).capitalize()
        entity_id = match.group(2)
        return cls(type=cast(EntityType, entity_type), id=entity_id) # 使用 cast