/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 用于 API 响应，表示一个 Block 的 GameState。
 */
export type GameStateDto = {
    /**
     * 包含 GameState 所有设置的字典。
     * 键是设置的名称 (string)，值是设置的值 (object?)。
     * 值的实际类型取决于具体的游戏状态设置。
     */
    settings: Record<string, any>;
};

