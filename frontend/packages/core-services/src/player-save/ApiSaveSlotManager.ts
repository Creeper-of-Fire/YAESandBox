import type {ISaveSlotManager, SaveSlot} from './ISaveSlotManager.ts';
import {ProjectSaveSlotService} from "../types/generated/player-save-api-client";

/**
 * ApiSaveSlotManager 的一个具体实现。
 * 它通过构造函数接收 projectUniqueName，使其可以为任何项目工作。
 */
export class ApiSaveSlotManager implements ISaveSlotManager
{
    private readonly projectUniqueName: string;

    /**
     * 创建一个 ApiSaveSlotManager 实例。
     * @param projectUniqueName - 此管理器实例将要操作的项目的唯一标识符。
     */
    constructor(projectUniqueName: string)
    {
        if (!projectUniqueName)
        {
            throw new Error("Project unique name must be provided.");
        }
        this.projectUniqueName = projectUniqueName;
    }

    async listSlots(): Promise<SaveSlot[]>
    {
        return ProjectSaveSlotService.getApiV1UserDataSaves({
            projectUniqueName: this.projectUniqueName,
        });
    }

    async createSlot(name: string, type: 'autosave' | 'snapshot'): Promise<SaveSlot>
    {
        return ProjectSaveSlotService.postApiV1UserDataSaves({
            projectUniqueName: this.projectUniqueName,
            requestBody: {name, type},
        });
    }

    async copySlot(sourceSlotId: string, newName: string, newType: 'autosave' | 'snapshot'): Promise<SaveSlot>
    {
        return ProjectSaveSlotService.postApiV1UserDataSavesCopy({
            projectUniqueName: this.projectUniqueName,
            sourceSlotId: sourceSlotId,
            requestBody: {name: newName, type: newType},
        });
    }

    async deleteSlot(slotId: string): Promise<void>
    {
        await ProjectSaveSlotService.deleteApiV1UserDataSaves({
            projectUniqueName: this.projectUniqueName,
            slotId: slotId,
        });
    }
}