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

# 从 command_processor 导入辅助函数 (或将其移到更合适的位置)
# 暂时假设它还在 processing 包
try:
    from processing.command_processor import _apply_modifications_to_entity
except ImportError:
    logging.error("无法导入 _apply_modifications_to_entity，原子化修改将失败。请确保该函数可用。")
    # 定义一个假的，避免启动时崩溃，但运行时会出错
    def _apply_modifications_to_entity(*args, **kwargs):
        raise NotImplementedError("_apply_modifications_to_entity 未找到")

router = APIRouter()

@router.post(
    "/entities",
    response_model=FlatEntityDTO,
    status_code=status.HTTP_201_CREATED,
    summary="创建新实体",
    description="创建一个新的游戏实体。如果同类型同 ID 实体已存在且未销毁，将返回冲突错误。"
)
async def create_entity(
    request: AtomicCreateEntityRequest,
    gs: GameState = Depends(get_game_state)
):
    """原子化创建实体接口。"""
    world = gs.world
    entity_type = request.entity_type
    entity_id = request.entity_id
    context = "Atomic Create"

    logging.info(f"[{context}] 请求创建实体: {entity_type}:{entity_id}")

    # 检查实体是否已存在 (未销毁)
    existing_entity = world.find_entity(entity_id, entity_type, include_destroyed=False)
    if existing_entity:
        logging.warning(f"[{context}] 创建失败：实体 '{entity_type}:{entity_id}' 已存在且未销毁。")
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail=f"实体 '{entity_type}:{entity_id}' 已存在且未销毁。"
        )

    # 检查实体是否已存在 (已销毁) -> 决定是恢复还是报错
    destroyed_entity = world.find_entity(entity_id, entity_type, include_destroyed=True)
    entity: AnyEntity # 类型提示

    if destroyed_entity:
        # 选项 A: 恢复并应用属性
        logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 已销毁，将恢复并应用初始属性。")
        destroyed_entity.set_attribute('is_destroyed', False, world=None) # 恢复
        entity = destroyed_entity
        # 清理旧属性？ CreateCommand 是清理的，这里也清理以保持一致
        # 注意：直接访问 PrivateAttr 可能不是最佳实践，但 CreateCommand 这样做了
        entity._dynamic_attributes.clear()
        # 选项 B: 报错，要求先 DELETE
        # raise HTTPException(
        #     status_code=status.HTTP_409_CONFLICT,
        #     detail=f"实体 '{entity_type}:{entity_id}' 已存在但被销毁，请先使用 DELETE /entities/{entity_type}/{entity_id} 彻底移除或直接 PATCH 修改。"
        # )
    else:
        # 创建新实例
        logging.info(f"[{context}] 创建新的实体实例: {entity_type}:{entity_id}")
        entity_class: type[BaseEntity]
        if entity_type == "Item": entity_class = Item
        elif entity_type == "Character": entity_class = Character
        elif entity_type == "Place": entity_class = Place
        else: # Should be caught by Pydantic validation
             raise HTTPException(status_code=500, detail="内部错误：无效的实体类型")
        entity = entity_class(entity_id=entity_id, entity_type=entity_type)
        world.add_entity(entity) # 添加到世界

    # 应用初始属性 (如果提供)
    if request.initial_attributes:
        # 转换 DTO 的 AttributeOperation 到 (op, value) 元组
        attributes_to_apply: Dict[str, Tuple[str, Any]] = {
            key: (op.op, op.value) for key, op in request.initial_attributes.items()
        }
        try:
            # 调用修改函数应用属性
            _apply_modifications_to_entity(entity, attributes_to_apply, world, context)
        except Exception as e:
            # 如果应用属性失败，是否应该删除刚刚创建的实体？
            # 暂时记录错误并返回 500
            logging.error(f"[{context}] 应用初始属性到 '{entity_type}:{entity_id}' 时失败: {e}", exc_info=True)
            # 可以在这里尝试删除 entity
            # world.get_entity_dict(entity_type).pop(entity_id, None)
            raise HTTPException(status_code=500, detail=f"创建实体成功但应用初始属性失败: {e}")

    # 转换为 DTO 返回
    dto = entity_to_dto(entity)
    if dto is None: # 理论上不应发生，除非刚创建就立刻被标记销毁了
        raise HTTPException(status_code=500, detail="内部错误：创建后无法转换实体为 DTO")

    logging.info(f"[{context}] 实体 '{entity_type}:{entity_id}' 创建成功。")
    return dto


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