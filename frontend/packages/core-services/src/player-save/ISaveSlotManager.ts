import type {SaveSlot as ApiSaveSlot} from "../types/generated/player-save-api-client";


// 我们可以重新定义一个前端模型，或者直接复用API客户端的模型
export type SaveSlot = ApiSaveSlot;

/**
 * 负责管理存档槽的生命周期（创建、列出、复制、删除）。
 * 这是与后端 ProjectSaveSlotService API 交互的高层端口。
 */
export interface ISaveSlotManager
{
    /**
     * 从后端获取所有可用的存档槽列表。
     * @returns 存档槽数组。
     */
    listSlots(): Promise<SaveSlot[]>;

    /**
     * 创建一个新的存档槽。
     * @param name - 新存档的名称。
     * @param type - 存档类型 ('autosave' | 'snapshot')。
     * @returns 创建成功后的存档槽对象。
     */
    createSlot(name: string, type: 'autosave' | 'snapshot'): Promise<SaveSlot>;

    /**
     * 从一个现有的存档槽复制出一个新的存档槽。
     * @param sourceSlotId - 源存档槽的 ID。
     * @param newName - 新存档的名称。
     * @param newType - 新存档的类型。
     * @returns 复制成功后的新存档槽对象。
     */
    copySlot(sourceSlotId: string, newName: string, newType: 'autosave' | 'snapshot'): Promise<SaveSlot>;

    /**
     * 删除一个指定的存档槽。
     * @param slotId - 要删除的存档槽 ID。
     */
    deleteSlot(slotId: string): Promise<void>;
}