import { defineStore } from 'pinia';
import { ref, watch } from 'vue';
import { type Character } from '#/types/models';
import { nanoid } from 'nanoid';
import localforage from 'localforage';

const STORAGE_KEY = 'era-lite-characters';

export const useCharacterStore = defineStore(STORAGE_KEY, () => {
    const characters = ref<Character[]>([
        // 一些初始数据用于测试
        { id: nanoid(), name: '爱丽丝', description: '一位好奇心旺盛的探险家。', avatar: '👩‍🚀' },
        { id: nanoid(), name: '鲍勃', description: '沉默寡言但可靠的保镖。', avatar: '💂‍♂️' },
        { id: nanoid(), name: '克莱尔', description: '神秘的占卜师，似乎知晓一切。', avatar: '🧙‍♀️' },
    ]);

    // 从 IndexedDB 加载初始状态
    localforage.getItem<Character[]>(STORAGE_KEY).then(savedCharacters => {
        if (savedCharacters && savedCharacters.length > 0) {
            characters.value = savedCharacters;
        }
    });

    // 监听变化并持久化
    watch(characters, (newCharacters) => {
        localforage.setItem(STORAGE_KEY, newCharacters);
    }, { deep: true });

    return { characters };
});