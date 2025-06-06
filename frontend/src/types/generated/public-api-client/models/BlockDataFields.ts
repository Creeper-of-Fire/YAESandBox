/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 用于标识 Block 中可能过时的字段。
 * 临时举措：看到ParentBlockId和ChildrenInfo时，进行一次拓扑更新。以后这个逻辑可能会迁移到专门的通知。
 */
export enum BlockDataFields {
    PARENT_BLOCK_ID = 'ParentBlockId',
    BLOCK_CONTENT = 'BlockContent',
    METADATA = 'Metadata',
    CHILDREN_INFO = 'ChildrenInfo',
    WORLD_STATE = 'WorldState',
    GAME_STATE = 'GameState',
}
