import { defineStore } from 'pinia';
import { ref, type Ref } from 'vue';
import { v4 as uuidv4 } from 'uuid';
import type { Instruction } from '#/game-logic/types';
import { InstructionType, InstructionStatus } from '#/game-logic/types';

export const useInstructionStreamStore = defineStore('instruction-stream', () => {
    // --- State ---
    const instructions: Ref<Instruction[]> = ref([]);

    // --- Actions ---

    /**
     * 创建一个新的指令并将其添加到指令流中。
     * 这是用户发起一个新意图时的入口点。
     * @param type - 指令的类型
     * @param context - 执行指令所需的上下文
     * @returns 新创建的指令的ID
     */
    function createInstruction(type: InstructionType, context: Instruction['context']): string {
        const newInstruction: Instruction = {
            id: uuidv4(),
            type,
            context,
            status: InstructionStatus.PENDING_USER_INPUT,
            userInput: { prompt: '' },
            aiProposal: null,
            error: null,
            createdAt: Date.now(),
        };
        instructions.value.push(newInstruction);
        return newInstruction.id;
    }

    /**
     * 根据ID查找指令的辅助函数。
     */
    function findInstruction(id: string): Instruction | undefined {
        return instructions.value.find(i => i.id === id);
    }

    /**
     * 更新指定指令的状态、提案、错误等信息。
     * @param id - 指令的ID
     * @param updates - 一个包含要更新的字段的部分对象
     */
    function updateInstruction(id: string, updates: Partial<Omit<Instruction, 'id' | 'createdAt'>>) {
        const instruction = findInstruction(id);
        if (instruction) {
            Object.assign(instruction, updates);
        } else {
            console.warn(`Attempted to update non-existent instruction with id: ${id}`);
        }
    }

    /**
     * 从指令流中移除一个指令。
     * @param id - 要移除的指令的ID
     */
    function removeInstruction(id: string) {
        instructions.value = instructions.value.filter(i => i.id !== id);
    }

    return {
        // State
        instructions,
        // Actions
        createInstruction,
        updateInstruction,
        removeInstruction,
        findInstruction,
    };
});