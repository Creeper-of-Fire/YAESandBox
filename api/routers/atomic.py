# api/routers/atomic.py
import logging
from fastapi import APIRouter, Depends, HTTPException, status, Response, Path as FastApiPath
from typing import Literal, Dict, Tuple, Any

# 导入 core 组件和 DTOs/依赖项
from core.game_state import GameState
from core.world_state import WorldState, BaseEntity, Item, Character, Place, AnyEntity
# --- 添加 TypedID 导入 ---
from core.types import TypedID, EntityType
# ---
from ..dtos import (
    FlatEntityDTO, entity_to_dto,
    AtomicCreateEntityRequest, AtomicModifyEntityRequest, AttributeOperation,
    DeleteResponse
)
from ..dependencies import get_game_state

# --- 辅助函数 _apply_modifications_to_entity (保持不变) ---
# ... (代码不变) ...
def _apply_modifications_to_entity(entity: BaseEntity, updates: Dict[str, Tuple[str, Any]], world: WorldState, command_context: str):
    """
    将一系列修改应用到 *给定的* 实体实例上。
    直接调用 entity.modify_attribute。
    """
    entity_id = entity.entity_id
    entity_type = entity.entity_type # 获取类型用于日志
    logging.debug(f"[{command_context}] '{entity_type}:{entity_id}': 开始应用修改: {updates}")
    for key, op_value_tuple in updates.items():
        try:
            logging.debug(f"[{command_context}] '{entity_type}:{entity_id}': 应用 -> {key} {op_value_tuple}")
            # modify_attribute 不再需要 world 参数
            entity.modify_attribute(key, op_value_tuple)
        except (ValueError, TypeError) as e:
            logging.error(f"[{command_context}] '{entity_type}:{entity_id}': 应用 '{key}={op_value_tuple}' 时失败: {e}", exc_info=False) # 减少堆栈噪音
            raise e
        except Exception as e:
            logging.exception(f"[{command_context}] '{entity_type}:{entity_id}': 应用 '{key}={op_value_tuple}' 时发生意外错误:")
            raise e
    logging.info(f"[{command_context}] '{entity_type}:{entity_id}' 应用修改完毕。")

router = APIRouter()

@router.post(
    "/entities",
    response_model=FlatEntityDTO,
    status_code=status.HTTP_201_CREATED,
    summary="创建或覆盖实体",
    description="创建新实体，或覆盖同类型同 ID 的现有实体（包括占位符或已销毁的）的属性。"
)
async def create_entity(
    request: AtomicCreateEntityRequest,
    gs: GameState = Depends(get_game_state)
):
    """原子化创建或覆盖实体接口。"""
    world = gs.world
    entity_type = request.entity_type
    entity_id = request.entity_id
    context = "Atomic Create/Overwrite"

    logging.info(f"[{context}] 请求处理实体: {entity_type}:{entity_id}")

    # --- 修改：使用新的 find_entity 调用方式 ---
    source_ref = TypedID(type=entity_type, id=entity_id)
    entity = world.find_entity(source_ref, include_destroyed=True)
    # --- 结束修改 ---

    status_to_return = status.HTTP_201_CREATED # 默认创建成功

    if entity:
        if entity.is_destroyed:
            logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 已销毁，将恢复并覆盖属性。")
            # set_attribute 不再需要 world
            entity.set_attribute('is_destroyed', False)
            entity._dynamic_attributes.clear()
            status_to_return = status.HTTP_200_OK # 恢复并覆盖返回 OK
        else:
            logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 已存在，将覆盖其属性。")
            status_to_return = status.HTTP_200_OK # 覆盖返回 OK
    else:
        logging.info(f"[{context}] 创建新的实体实例: {entity_type}:{entity_id}")
        entity_class: type[BaseEntity]
        if entity_type == "Item": entity_class = Item
        elif entity_type == "Character": entity_class = Character
        elif entity_type == "Place": entity_class = Place
        else: raise HTTPException(status_code=500, detail="内部错误：无效的实体类型")
        entity = entity_class(entity_id=entity_id, entity_type=entity_type)
        world.add_entity(entity)
        # status_to_return 保持 201 CREATED

    # 应用初始属性 (如果提供)
    if request.initial_attributes:
        attributes_to_apply: Dict[str, Tuple[str, Any]] = {
            key: (op.op, op.value) for key, op in request.initial_attributes.items()
        }
        try:
            # _apply_modifications_to_entity 现在也不需要 world
            _apply_modifications_to_entity(entity, attributes_to_apply, world, context) # world 仍需传给 helper 用于日志等？Helper内部不需要了
        except Exception as e:
            logging.error(f"[{context}] 应用属性到 '{entity_type}:{entity_id}' 时失败: {e}", exc_info=True)
            raise HTTPException(status_code=500, detail=f"处理实体成功但应用属性失败: {e}")

    dto = entity_to_dto(entity)
    if dto is None:
        # 如果实体刚被恢复，dto 不应为 None，除非 get_all_attributes 出错
        logging.error(f"[{context}] 内部错误：处理后无法转换实体 {source_ref} 为 DTO")
        raise HTTPException(status_code=500, detail="内部错误：处理后无法转换实体为 DTO")

    logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 处理成功。")
    # 返回合适的status code
    return Response(content=dto.model_dump_json(), status_code=status_to_return, media_type="application/json")


@router.patch(
    "/entities/{entity_type}/{entity_id}",
    response_model=FlatEntityDTO,
    summary="修改实体属性",
    description="修改现有实体的属性。只允许修改未销毁的实体。"
)
async def modify_entity(
    entity_type: EntityType = FastApiPath(..., description="要修改的实体类型"), # 使用 EntityType
    entity_id: str = FastApiPath(..., description="要修改的实体ID"),
    request: AtomicModifyEntityRequest = ...,
    gs: GameState = Depends(get_game_state)
):
    """原子化修改实体属性接口。"""
    world = gs.world
    context = "Atomic Modify"
    logging.info(f"[{context}] 请求修改实体: {entity_type}:{entity_id}")

    # --- 修改：使用新的 find_entity 调用方式 ---
    source_ref = TypedID(type=entity_type, id=entity_id)
    entity = world.find_entity(source_ref, include_destroyed=False) # 修改通常不包括已销毁的
    # --- 结束修改 ---

    if not entity:
        logging.warning(f"[{context}] 修改失败：实体 '{entity_type}:{entity_id}' 未找到或已销毁。")
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"实体 '{entity_type}:{entity_id}' 未找到或已销毁。"
        )

    attributes_to_apply: Dict[str, Tuple[str, Any]] = {
        key: (op.op, op.value) for key, op in request.attributes.items()
    }

    try:
        # _apply_modifications_to_entity 现在也不需要 world
        _apply_modifications_to_entity(entity, attributes_to_apply, world, context) # world 仍需传给 helper 用于日志等？Helper内部不需要了
    except Exception as e:
        logging.error(f"[{context}] 应用属性修改到 '{entity_type}:{entity_id}' 时失败: {e}", exc_info=True)
        if isinstance(e, (ValueError, TypeError)):
             raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=f"修改失败: {e}")
        else:
             raise HTTPException(status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=f"内部错误: {e}")

    dto = entity_to_dto(entity)
    if dto is None:
         logging.error(f"[{context}] 内部错误：修改后无法转换实体 {source_ref} 为 DTO")
         raise HTTPException(status_code=500, detail="内部错误：修改后无法转换实体为 DTO")

    logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 修改成功。")
    return dto


@router.delete(
    "/entities/{entity_type}/{entity_id}",
    status_code=status.HTTP_204_NO_CONTENT,
    summary="销毁实体",
    description="销毁一个游戏实体（标记为 is_destroyed=True）。"
)
async def delete_entity(
    entity_type: EntityType = FastApiPath(..., description="要销毁的实体类型"), # 使用 EntityType
    entity_id: str = FastApiPath(..., description="要销毁的实体ID"),
    gs: GameState = Depends(get_game_state)
):
    """原子化销毁实体接口。"""
    world = gs.world
    context = "Atomic Delete"
    logging.info(f"[{context}] 请求销毁实体: {entity_type}:{entity_id}")

    # --- 修改：使用新的 find_entity 调用方式 ---
    source_ref = TypedID(type=entity_type, id=entity_id)
    entity = world.find_entity(source_ref, include_destroyed=True) # 查找包括已销毁的
    # --- 结束修改 ---

    if not entity:
        logging.warning(f"[{context}] 销毁失败：实体 '{entity_type}:{entity_id}' 未找到。")
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"实体 '{entity_type}:{entity_id}' 未找到。"
        )

    if entity.is_destroyed:
        logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 已经被销毁，无需操作。")
        return Response(status_code=status.HTTP_204_NO_CONTENT)

    try:
        # 调用简化后的 set_attribute
        entity.set_attribute('is_destroyed', True)
        logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 已成功标记为销毁。")
        return Response(status_code=status.HTTP_204_NO_CONTENT)
    except Exception as e:
        logging.error(f"[{context}] 销毁实体 '{entity_type}:{entity_id}' 时发生错误: {e}", exc_info=True)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"销毁实体时发生内部错误: {e}"
        )