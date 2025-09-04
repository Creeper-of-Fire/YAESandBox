import { defineStore } from 'pinia';
import { type Character } from '#/types/models.ts';
import { nanoid } from 'nanoid';
import { createScopedPersistentState } from '#/share/createScopedPersistentState.ts';
import { watchOnce } from '@vueuse/core';
import {useEraLiteSaveStore} from "#/features/home/useEraLiteSaveStore.ts";

const STORAGE_KEY = 'era-lite-characters';

export const useCharacterStore = defineStore(STORAGE_KEY, () => {
    const globalStore = useEraLiteSaveStore();

    const { state: characters, isReady } =  globalStore.createState<Character[]>(STORAGE_KEY, []);

    // --- Actions ---
    function addCharacter(charData: Omit<Character, 'id'>) {
        const newChar: Character = { ...charData, id: nanoid() };
        characters.value.unshift(newChar);
    }

    function updateCharacter(updatedChar: Character) {
        const index = characters.value.findIndex(c => c.id === updatedChar.id);
        if (index !== -1) {
            characters.value[index] = updatedChar;
        }
    }

    function deleteCharacter(characterId: string) {
        characters.value = characters.value.filter(c => c.id !== characterId);
    }

    // --- Initialization Logic ---
    watchOnce(isReady, () => {
        if (characters.value.length === 0) {
            console.log('No characters found in storage, creating initial set.');
            characters.value = [
                { id: nanoid(), name: '爱丽丝', description: '一位好奇心旺盛的探险家。', avatar: '👩‍🚀' },
                { id: nanoid(), name: '鲍勃', description: '沉默寡言但可靠的保镖。', avatar: '💂‍♂️' },
                { id: nanoid(), name: '克莱尔', description: '神秘的占卜师，似乎知晓一切。', avatar: '🧙‍♀️' },
            ];
        }
    });

    return { characters, isReady, addCharacter, updateCharacter, deleteCharacter };
});