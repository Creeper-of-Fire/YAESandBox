/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 用于创建新存档槽的请求体。
 * 它同时用于“从零创建”和“从副本创建”两种场景。
 */
export type CreateSaveSlotRequest = {
    /**
     * 新存档的名称。
     */
    name: string;
    /**
     * 新存档的类型 (例如 'autosave', 'snapshot')。后端只负责存储，不关心其含义。
     */
    type: string;
};

