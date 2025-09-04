/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 表示一个存档槽的完整信息。这是高层API的主要数据结构。
 */
export type SaveSlot = {
    /**
     * 存档槽的唯一标识符，使用其对目录进行操作。
     * 这个ID用于在高层API中进行引用，例如删除或复制操作 (`DELETE /saves/{Id}`)。
     */
    id: string;
    /**
     * 一个不透明的访问令牌。前端应将其视为一个必须保存的句柄。
     */
    token: string;
    /**
     * 用户定义的存档名称，来自meta.json。
     */
    name: string;
    /**
     * 存档类型，来自meta.json。后端对此字段不作任何解释。
     */
    type: string;
    /**
     * 存档创建时间，来自meta.json。
     */
    createdAt: string;
};

