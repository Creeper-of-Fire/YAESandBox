/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 用于 API 请求，表示要更新的 GameState 设置。
 */
export type UpdateGameStateRequestDto = {
    /**
     * 一个字典，包含要更新或添加的 GameState 设置。
     * 键是要修改的设置名称，值是新的设置值。
     * 如果值为 null，通常表示移除该设置或将其设置为空。
     */
    settingsToUpdate: Record<string, any>;
};

