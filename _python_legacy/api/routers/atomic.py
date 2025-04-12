# api/routers/atomic.py
# -*- coding: utf-8 -*-
"""
提供原子化的 RESTful API 用于创建、更新、删除 (CUD) 游戏实体。
这是修改游戏状态的唯一入口。
修改成功后会触发 Notifier 服务。
"""
import logging
from fastapi import APIRouter, Depends, HTTPException, status, Response, Path as FastApiPath
from typing import Dict, Tuple, Any

# --- 核心模块导入 ---
from core.game_state import GameState
from core.world_state import WorldState, BaseEntity, Item, Character, Place
from core.types import TypedID, EntityType
# --- DTOs 和依赖项 ---
from ..dtos import (
    FlatEntityDTO, entity_to_dto,
    AtomicCreateEntityRequest, AtomicModifyEntityRequest,
    DeleteResponse # 虽然 DELETE 返回 204，但保持导入可能有用
)
from ..dependencies import get_game_state, get_notifier # <--- 导入 get_notifier
from ..notifier import Notifier # <--- 导入 Notifier 类型提示

router = APIRouter()

# --- 辅助函数 _apply_modifications_to_entity (保持不变) ---
def _apply_modifications_to_entity(entity: BaseEntity, updates: Dict[str, Tuple[str, Any]], world: WorldState, command_context: str):
    """将一系列修改应用到实体实例。"""
    entity_id = entity.entity_id
    entity_type = entity.entity_type
    logging.debug(f"[{command_context}] '{entity_type}:{entity_id}': 开始应用修改: {updates}")
    for key, op_value_tuple in updates.items():
        try:
            logging.debug(f"[{command_context}] '{entity_type}:{entity_id}': 应用 -> {key} {op_value_tuple}")
            entity.modify_attribute(key, op_value_tuple)
        except (ValueError, TypeError) as e:
            logging.error(f"[{command_context}] '{entity_type}:{entity_id}': 应用 '{key}={op_value_tuple}' 时失败: {e}", exc_info=False)
            raise e
        except Exception as e:
            logging.exception(f"[{command_context}] '{entity_type}:{entity_id}': 应用 '{key}={op_value_tuple}' 时发生意外错误:")
            raise e
    logging.info(f"[{command_context}] '{entity_type}:{entity_id}' 应用修改完毕。")


@router.post(
    "/entities",
    response_model=FlatEntityDTO,
    # status_code 动态决定 (201 or 200)
    summary="原子化创建或覆盖实体",
    description="创建新实体，或覆盖同类型同 ID 的现有实体（包括已销毁的）。成功后将触发状态更新通知。"
)
async def create_entity(
    request: AtomicCreateEntityRequest,
    gs: GameState = Depends(get_game_state),
    notifier: Notifier = Depends(get_notifier) # <--- 注入 Notifier
):
    """原子化创建或覆盖实体接口。"""
    world = gs.world
    entity_type = request.entity_type
    entity_id = request.entity_id
    context = "Atomic Create/Overwrite"
    logging.info(f"[{context}] 请求处理实体: {entity_type}:{entity_id}")

    source_ref = TypedID(type=entity_type, id=entity_id)
    entity = world.find_entity(source_ref, include_destroyed=True)
    status_to_return = status.HTTP_201_CREATED # 默认创建
    operation_occurred = False # 标记是否有实际状态变更

    if entity:
        if entity.is_destroyed:
            logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 已销毁，将恢复并覆盖属性。")
            entity.set_attribute('is_destroyed', False)
            entity._dynamic_attributes.clear() # 清空旧属性
            status_to_return = status.HTTP_200_OK
            operation_occurred = True # 恢复是状态变更
        else:
            logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 已存在，将覆盖其属性。")
            status_to_return = status.HTTP_200_OK
            # 覆盖本身可能不改变 is_destroyed，但后续属性应用会改变状态
            # 我们可以在属性应用后检查是否真的改变了状态，但为了简单起见，
            # 假设覆盖（如果提供了属性）总是状态变更
            if request.initial_attributes:
                 operation_occurred = True
    else:
        logging.info(f"[{context}] 创建新的实体实例: {entity_type}:{entity_id}")
        entity_class: type[BaseEntity]
        if entity_type == "Item": entity_class = Item
        elif entity_type == "Character": entity_class = Character
        elif entity_type == "Place": entity_class = Place
        else: raise HTTPException(status_code=500, detail="内部错误：无效的实体类型")
        entity = entity_class(entity_id=entity_id, entity_type=entity_type)
        world.add_entity(entity)
        operation_occurred = True # 创建是状态变更

    # 应用初始属性
    if request.initial_attributes:
        attributes_to_apply: Dict[str, Tuple[str, Any]] = {
            key: (op.op, op.value) for key, op in request.initial_attributes.items()
        }
        try:
            _apply_modifications_to_entity(entity, attributes_to_apply, world, context)
            # 假设应用属性总是状态变更，即使值可能没变 (简化处理)
            operation_occurred = True
        except Exception as e:
            logging.error(f"[{context}] 应用属性到 '{entity_type}:{entity_id}' 时失败: {e}", exc_info=True)
            # 如果实体是新创建的，但属性应用失败，可能需要回滚或返回错误
            # 这里选择抛出异常，让调用者知道操作未完全成功
            raise HTTPException(status_code=500, detail=f"处理实体成功但应用属性失败: {e}")

    dto = entity_to_dto(entity)
    if dto is None:
        logging.error(f"[{context}] 内部错误：处理后无法转换实体 {source_ref} 为 DTO")
        raise HTTPException(status_code=500, detail="内部错误：处理后无法转换实体为 DTO")

    # --- 触发通知 ---
    if operation_occurred:
        await notifier.notify_state_update() # <--- 调用通知

    logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 处理成功。")
    return Response(content=dto.model_dump_json(), status_code=status_to_return, media_type="application/json")


@router.patch(
    "/entities/{entity_type}/{entity_id}",
    response_model=FlatEntityDTO,
    summary="原子化修改实体属性",
    description="修改现有实体的属性。只允许修改未销毁的实体。成功后将触发状态更新通知。"
)
async def modify_entity(
    entity_type: EntityType = FastApiPath(..., description="要修改的实体类型"),
    entity_id: str = FastApiPath(..., description="要修改的实体ID"),
    request: AtomicModifyEntityRequest = ...,
    gs: GameState = Depends(get_game_state),
    notifier: Notifier = Depends(get_notifier) # <--- 注入 Notifier
):
    """原子化修改实体属性接口。"""
    world = gs.world
    context = "Atomic Modify"
    logging.info(f"[{context}] 请求修改实体: {entity_type}:{entity_id}")

    source_ref = TypedID(type=entity_type, id=entity_id)
    entity = world.find_entity(source_ref, include_destroyed=False)

    if not entity:
        logging.warning(f"[{context}] 修改失败：实体 '{entity_type}:{entity_id}' 未找到或已销毁。")
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=f"实体 '{entity_type}:{entity_id}' 未找到或已销毁。")

    attributes_to_apply: Dict[str, Tuple[str, Any]] = {
        key: (op.op, op.value) for key, op in request.attributes.items()
    }

    # --- 可以在应用前记录旧状态，应用后比较来判断是否有实际变更 ---
    # old_state_snapshot = entity.get_all_attributes() # 或者只记录要修改的属性
    operation_occurred = False # 默认未变更

    try:
        _apply_modifications_to_entity(entity, attributes_to_apply, world, context)
        # --- 应用后判断是否变更 (简化：假设任何 PATCH 调用都可能导致变更) ---
        operation_occurred = True # 暂时假设总是有变更，以简化逻辑
        # new_state_snapshot = entity.get_all_attributes()
        # if old_state_snapshot != new_state_snapshot:
        #     operation_occurred = True

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

    # --- 触发通知 ---
    if operation_occurred:
        await notifier.notify_state_update() # <--- 调用通知

    logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 修改成功。")
    return dto


@router.delete(
    "/entities/{entity_type}/{entity_id}",
    status_code=status.HTTP_204_NO_CONTENT,
    summary="原子化销毁实体",
    description="销毁一个游戏实体（标记为 is_destroyed=True）。成功销毁将触发状态更新通知。"
)
async def delete_entity(
    entity_type: EntityType = FastApiPath(..., description="要销毁的实体类型"),
    entity_id: str = FastApiPath(..., description="要销毁的实体ID"),
    gs: GameState = Depends(get_game_state),
    notifier: Notifier = Depends(get_notifier) # <--- 注入 Notifier
):
    """原子化销毁实体接口。"""
    world = gs.world
    context = "Atomic Delete"
    logging.info(f"[{context}] 请求销毁实体: {entity_type}:{entity_id}")

    source_ref = TypedID(type=entity_type, id=entity_id)
    entity = world.find_entity(source_ref, include_destroyed=True)

    if not entity:
        logging.warning(f"[{context}] 销毁失败：实体 '{entity_type}:{entity_id}' 未找到。")
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=f"实体 '{entity_type}:{entity_id}' 未找到。")

    operation_occurred = False # 默认未变更
    if not entity.is_destroyed:
        try:
            entity.set_attribute('is_destroyed', True)
            operation_occurred = True # 成功销毁是状态变更
            logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 已成功标记为销毁。")
        except Exception as e:
            logging.error(f"[{context}] 销毁实体 '{entity_type}:{entity_id}' 时发生错误: {e}", exc_info=True)
            raise HTTPException(status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=f"销毁实体时发生内部错误: {e}")
    else:
        logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 已经被销毁，无需操作。")

    # --- 触发通知 ---
    if operation_occurred:
        await notifier.notify_state_update() # <--- 调用通知

    return Response(status_code=status.HTTP_204_NO_CONTENT)