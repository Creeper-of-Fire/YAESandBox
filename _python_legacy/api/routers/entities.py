# api/routers/entities.py
import logging
from fastapi import APIRouter, Depends, HTTPException, Query, status, Path as FastApiPath # 导入 Path 用于路径参数
from typing import List, Optional, Literal, Tuple

# 导入 core 组件和 DTOs/依赖项
from core.game_state import GameState
from core.world_state import WorldState, BaseEntity, Item, Character, Place
from core.types import TypedID, EntityType # 导入 TypedID 和 EntityType
from ..dtos import FlatEntityDTO, entity_to_dto, entities_to_dtos
from ..dependencies import get_game_state

router = APIRouter()

# --- 获取单个实体 (保持不变，因为 find_entity 接口未变) ---
@router.get(
    "/entities/{entity_type}/{entity_id}",
    response_model=FlatEntityDTO,
    summary="获取单个实体信息",
    description="通过实体类型和 ID 查询实体的详细信息（不包括已销毁的实体）。"
)
async def get_entity(
    entity_type: EntityType = FastApiPath(..., description="要获取的实体类型"), # 使用 EntityType
    entity_id: str = FastApiPath(..., description="要获取的实体ID"),
    gs: GameState = Depends(get_game_state)
):
    """根据类型和 ID 获取单个实体信息。"""
    logging.info(f"API 请求：获取实体 Type='{entity_type}', ID='{entity_id}'")
    entity = gs.find_entity(entity_id, entity_type, include_destroyed=False)
    dto = entity_to_dto(entity)
    if dto is None:
        logging.warning(f"API 响应：实体 '{entity_type}:{entity_id}' 未找到或已销毁。")
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"实体 '{entity_type}:{entity_id}' 未找到或已被销毁"
        )
    logging.info(f"API 响应：成功返回实体 '{entity_type}:{entity_id}' 的 DTO。")
    return dto

# --- 获取实体列表 (保持不变) ---
@router.get(
    "/entities",
    response_model=List[FlatEntityDTO],
    summary="获取实体列表",
    description="获取指定类型或所有类型的实体列表（不包括已销毁的实体）。"
)
async def get_entities(
    entity_type: Optional[EntityType] = Query(None, description="按实体类型过滤"), # 使用 EntityType
    gs: GameState = Depends(get_game_state)
):
    """获取实体列表，可选按类型过滤。"""
    logging.info(f"API 请求：获取实体列表，类型过滤='{entity_type}'")
    entities_to_convert: List[BaseEntity] = []
    world = gs.world

    if entity_type:
        entity_dict = world.get_entity_dict(entity_type)
        entities_to_convert.extend(list(entity_dict.values()))
    else:
        entities_to_convert.extend(list(world.items.values()))
        entities_to_convert.extend(list(world.characters.values()))
        entities_to_convert.extend(list(world.places.values()))

    dtos = entities_to_dtos(entities_to_convert)
    logging.info(f"API 响应：返回 {len(dtos)} 个实体的 DTO 列表 (类型过滤='{entity_type}')。")
    return dtos

# --- 获取地点内容物 (修改：处理 TypedID 列表，使用 find_entity_by_typed_id) ---
@router.get(
    "/places/{place_id}/contents",
    response_model=List[FlatEntityDTO],
    summary="获取地点的内容物",
    description="获取指定地点内的所有实体（物品和角色）列表（不包括已销毁的）。"
)
async def get_place_contents(
    place_id: str,
    gs: GameState = Depends(get_game_state)
):
    """获取地点内容物。"""
    logging.info(f"API 请求：获取地点 ID='{place_id}' 的内容物。")
    place = gs.find_entity(place_id, "Place", include_destroyed=False)
    if not place or not isinstance(place, Place):
        logging.warning(f"API 响应：地点 Place:{place_id}' 未找到或类型错误。")
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"地点 'Place:{place_id}' 未找到或不是一个 Place"
        )

    # get_attribute 现在返回 List[TypedID]
    content_refs: List[TypedID] = place.get_attribute('contents', [])
    if not isinstance(content_refs, list):
         logging.error(f"地点 'Place:{place_id}' 的 contents 属性不是列表: {type(content_refs)}")
         return []

    # --- 修改：使用 find_entity_by_typed_id 查找每个引用 ---
    content_entities: List[Optional[BaseEntity]] = []
    for ref in content_refs:
        if isinstance(ref, TypedID):
            content_entities.append(gs.find_entity_by_typed_id(ref, include_destroyed=False))
        else:
            # 如果 get_attribute 返回了非 TypedID，记录警告
            logging.warning(f"在地点 'Place:{place_id}' 的 contents 中发现非 TypedID 项: {ref} ({type(ref)})")

    dtos = entities_to_dtos(content_entities) # 过滤掉 None 和已销毁的
    logging.info(f"API 响应：返回地点 'Place:{place_id}' 的 {len(dtos)} 个内容物 DTO。")
    return dtos

# --- 获取角色物品栏 (修改：处理 TypedID 列表，使用 find_entity_by_typed_id) ---
@router.get(
    "/characters/{character_id}/items",
    response_model=List[FlatEntityDTO],
    summary="获取角色的物品栏",
    description="获取指定角色携带的所有物品列表（不包括已销毁的）。"
)
async def get_character_items(
    character_id: str,
    gs: GameState = Depends(get_game_state)
):
    """获取角色物品栏。"""
    logging.info(f"API 请求：获取角色 ID='{character_id}' 的物品栏。")
    character = gs.find_entity(character_id, "Character", include_destroyed=False)
    if not character or not isinstance(character, Character):
        logging.warning(f"API 响应：角色 'Character:{character_id}' 未找到或类型错误。")
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"角色 'Character:{character_id}' 未找到或不是一个 Character"
        )

    # get_attribute 现在返回 List[TypedID]
    item_refs: List[TypedID] = character.get_attribute('has_items', [])
    if not isinstance(item_refs, list):
         logging.error(f"角色 'Character:{character_id}' 的 has_items 属性不是列表: {type(item_refs)}")
         return []

    # --- 修改：使用 find_entity_by_typed_id 查找每个引用 ---
    item_entities: List[Optional[BaseEntity]] = []
    for ref in item_refs:
        if isinstance(ref, TypedID):
             # 确保只查找 Item 类型
            if ref.type == "Item":
                item_entities.append(gs.find_entity_by_typed_id(ref, include_destroyed=False))
            else:
                logging.warning(f"角色 '{character.typed_id}' 的 has_items 中发现非 Item 类型的 TypedID: {ref}")
        else:
            logging.warning(f"在角色 'Character:{character_id}' 的 has_items 中发现非 TypedID 项: {ref} ({type(ref)})")

    item_dtos = entities_to_dtos(item_entities) # 过滤掉 None 和已销毁的
    # 确保只返回物品 DTO (双重保险)
    item_dtos = [dto for dto in item_dtos if dto.entity_type == 'Item']
    logging.info(f"API 响应：返回角色 'Character:{character_id}' 的 {len(item_dtos)} 个物品 DTO。")
    return item_dtos