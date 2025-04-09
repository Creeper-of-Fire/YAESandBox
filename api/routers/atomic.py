# api/routers/atomic.py
import logging
from fastapi import APIRouter, Depends, HTTPException, status, Response, Path as FastApiPath
from typing import Literal, Dict, Tuple, Any

# 导入 core 组件和 DTOs/依赖项
from core.game_state import GameState
from core.world_state import WorldState, BaseEntity, Item, Character, Place, AnyEntity
from ..dtos import (
    FlatEntityDTO, entity_to_dto,
    AtomicCreateEntityRequest, AtomicModifyEntityRequest, AttributeOperation,
    DeleteResponse
)
from ..dependencies import get_game_state

# --- 辅助函数 _apply_modifications_to_entity (保持不变) ---
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
            entity.modify_attribute(key, op_value_tuple, world)
        except (ValueError, TypeError) as e:
            logging.error(f"[{command_context}] '{entity_type}:{entity_id}': 应用 '{key}={op_value_tuple}' 时失败: {e}", exc_info=True)
            raise e
        except Exception as e:
            logging.exception(f"[{command_context}] '{entity_type}:{entity_id}': 应用 '{key}={op_value_tuple}' 时发生意外错误:")
            raise e
    logging.info(f"[{command_context}] '{entity_type}:{entity_id}' 应用修改完毕。")

router = APIRouter()

@router.post(
    "/entities",
    response_model=FlatEntityDTO,
    status_code=status.HTTP_201_CREATED, # 成功创建或覆盖后都返回 201 或 200？维持 201 也可以
    summary="创建或覆盖实体", # <--- 修改描述
    description="创建新实体，或覆盖同类型同 ID 的现有实体（包括占位符或已销毁的）的属性。" # <--- 修改描述
)
async def create_entity(
    request: AtomicCreateEntityRequest,
    gs: GameState = Depends(get_game_state)
):
    """原子化创建或覆盖实体接口。"""
    world = gs.world
    entity_type = request.entity_type
    entity_id = request.entity_id
    context = "Atomic Create/Overwrite" # <--- 修改上下文描述

    logging.info(f"[{context}] 请求处理实体: {entity_type}:{entity_id}")

    # --- 修改：查找实体，包括已销毁的 ---
    entity = world.find_entity(entity_id, entity_type, include_destroyed=True)

    if entity:
        # 实体已存在 (可能是正常的，也可能是占位符，也可能是已销毁的)
        if entity.is_destroyed:
            logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 已销毁，将恢复并覆盖属性。")
            entity.set_attribute('is_destroyed', False, world=None) # 恢复
            # 清理旧动态属性以模拟覆盖
            entity._dynamic_attributes.clear()
            status_to_return = status.HTTP_200_OK # 恢复并覆盖，返回 200 OK 可能更合适？或保持 201？我们暂定 201
        else:
            # 实体存在且未销毁（覆盖操作）
            logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 已存在，将覆盖其属性。")
            # 不需要清除，modify 会覆盖
            status_to_return = status.HTTP_200_OK # 覆盖现有资源，返回 200 OK
    else:
        # 实体完全不存在，创建新实例
        logging.info(f"[{context}] 创建新的实体实例: {entity_type}:{entity_id}")
        entity_class: type[BaseEntity]
        if entity_type == "Item": entity_class = Item
        elif entity_type == "Character": entity_class = Character
        elif entity_type == "Place": entity_class = Place
        else: raise HTTPException(status_code=500, detail="内部错误：无效的实体类型")
        entity = entity_class(entity_id=entity_id, entity_type=entity_type)
        world.add_entity(entity)
        status_to_return = status.HTTP_201_CREATED # 明确是新创建的

    # 应用初始属性 (如果提供)
    if request.initial_attributes:
        attributes_to_apply: Dict[str, Tuple[str, Any]] = {
            key: (op.op, op.value) for key, op in request.initial_attributes.items()
        }
        try:
            _apply_modifications_to_entity(entity, attributes_to_apply, world, context)
        except Exception as e:
            logging.error(f"[{context}] 应用属性到 '{entity_type}:{entity_id}' 时失败: {e}", exc_info=True)
            # 这里保持不变，应用属性失败还是内部错误
            raise HTTPException(status_code=500, detail=f"处理实体成功但应用属性失败: {e}")

    # 转换为 DTO 返回
    dto = entity_to_dto(entity)
    if dto is None:
        raise HTTPException(status_code=500, detail="内部错误：处理后无法转换实体为 DTO")

    logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 处理成功。")
    # --- 修改：根据情况返回不同状态码 ---
    # 注意：FastAPI 装饰器中的 status_code 是默认值，实际返回可以在函数内决定
    # 但为了简单，我们可以统一返回 200 OK 表示成功处理（无论是创建还是覆盖）
    # 或者维持装饰器的 201，因为调用者可能期望创建操作返回 201
    # 我们先统一返回 200 OK 试试
    from fastapi import Response # 确保导入 Response
    return Response(content=dto.model_dump_json(), status_code=status.HTTP_200_OK, media_type="application/json")
    # 或者，维持 201
    # return dto # FastAPI 会使用装饰器的 201


@router.patch(
    "/entities/{entity_type}/{entity_id}",
    response_model=FlatEntityDTO,
    summary="修改实体属性",
    description="修改现有实体的属性。只允许修改未销毁的实体。"
)
async def modify_entity(
    entity_type: Literal["Item", "Character", "Place"] = FastApiPath(..., description="要修改的实体类型"),
    entity_id: str = FastApiPath(..., description="要修改的实体ID"),
    request: AtomicModifyEntityRequest = ..., # 从请求体获取
    gs: GameState = Depends(get_game_state)
):
    """原子化修改实体属性接口。"""
    world = gs.world
    context = "Atomic Modify"
    logging.info(f"[{context}] 请求修改实体: {entity_type}:{entity_id}")

    # 查找实体 (必须存在且未销毁)
    entity = world.find_entity(entity_id, entity_type, include_destroyed=False)
    if not entity:
        logging.warning(f"[{context}] 修改失败：实体 '{entity_type}:{entity_id}' 未找到或已销毁。")
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"实体 '{entity_type}:{entity_id}' 未找到或已销毁。"
        )

    # 转换 DTO 的 AttributeOperation 到 (op, value) 元组
    attributes_to_apply: Dict[str, Tuple[str, Any]] = {
        key: (op.op, op.value) for key, op in request.attributes.items()
    }

    try:
        # 调用修改函数应用属性
        _apply_modifications_to_entity(entity, attributes_to_apply, world, context)
    except Exception as e:
        logging.error(f"[{context}] 应用属性修改到 '{entity_type}:{entity_id}' 时失败: {e}", exc_info=True)
        # 决定是返回 400 (请求数据问题) 还是 500 (内部逻辑错误)
        # 如果是 ValueError/TypeError，可能是请求数据问题 -> 400
        if isinstance(e, (ValueError, TypeError)):
             raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=f"修改失败: {e}")
        else: # 其他错误 -> 500
             raise HTTPException(status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=f"内部错误: {e}")

    # 转换更新后的实体为 DTO 返回
    dto = entity_to_dto(entity)
    if dto is None: # 理论上不应发生
         raise HTTPException(status_code=500, detail="内部错误：修改后无法转换实体为 DTO")

    logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 修改成功。")
    return dto


@router.delete(
    "/entities/{entity_type}/{entity_id}",
    status_code=status.HTTP_204_NO_CONTENT, # 通常 DELETE 成功返回 204
    # response_model=DeleteResponse, # 或者返回一个消息体
    summary="销毁实体",
    description="销毁一个游戏实体（标记为 is_destroyed=True）。"
)
async def delete_entity(
    entity_type: Literal["Item", "Character", "Place"] = FastApiPath(..., description="要销毁的实体类型"),
    entity_id: str = FastApiPath(..., description="要销毁的实体ID"),
    gs: GameState = Depends(get_game_state)
):
    """原子化销毁实体接口。"""
    world = gs.world
    context = "Atomic Delete"
    logging.info(f"[{context}] 请求销毁实体: {entity_type}:{entity_id}")

    # 查找实体 (包括已销毁的，因为可能重复删除)
    entity = world.find_entity(entity_id, entity_type, include_destroyed=True)
    if not entity:
        logging.warning(f"[{context}] 销毁失败：实体 '{entity_type}:{entity_id}' 未找到。")
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"实体 '{entity_type}:{entity_id}' 未找到。"
        )

    if entity.is_destroyed:
        logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 已经被销毁，无需操作。")
        # 返回成功 (204) 或许更符合幂等性
        # return DeleteResponse(message=f"实体 '{entity_type}:{entity_id}' 已被销毁。")
        return Response(status_code=status.HTTP_204_NO_CONTENT)

    try:
        # 调用 set_attribute 标记销毁，触发内部清理逻辑 (如果实现完整)
        entity.set_attribute('is_destroyed', True, world=world)
        logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 已成功标记为销毁。")
        # return DeleteResponse(message=f"实体 '{entity_type}:{entity_id}' 已成功销毁。")
        return Response(status_code=status.HTTP_204_NO_CONTENT)
    except Exception as e:
        logging.error(f"[{context}] 销毁实体 '{entity_type}:{entity_id}' 时发生错误: {e}", exc_info=True)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"销毁实体时发生内部错误: {e}"
        )