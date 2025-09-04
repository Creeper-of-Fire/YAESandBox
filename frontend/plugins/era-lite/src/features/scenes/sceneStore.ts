import { defineStore } from 'pinia';
import {type Item, type Scene} from '#/types/models.ts';
import { nanoid } from 'nanoid';
import { watchOnce } from '@vueuse/core';
import {useEraLiteSaveStore} from "#/features/home/useEraLiteSaveStore.ts";

const STORAGE_KEY = 'era-lite-scenes';

export const useSceneStore = defineStore(STORAGE_KEY, () => {
    const globalStore = useEraLiteSaveStore();

    const {state: scenes, isReady} = globalStore.createState<Scene[]>(
        STORAGE_KEY,
        []
    );

    // --- Actions ---
    function addScene(sceneData: Omit<Scene, 'id'>) {
        const newScene: Scene = { ...sceneData, id: nanoid() };
        scenes.value.unshift(newScene);
    }

    function updateScene(updatedScene: Scene) {
        const index = scenes.value.findIndex(s => s.id === updatedScene.id);
        if (index !== -1) {
            scenes.value[index] = updatedScene;
        }
    }

    function deleteScene(sceneId: string) {
        scenes.value = scenes.value.filter(s => s.id !== sceneId);
    }

    // --- Initialization Logic ---
    watchOnce(isReady, () => {
        if (scenes.value.length === 0) {
            console.log('No scenes found in storage, creating initial set.');
            scenes.value = [
                { id: nanoid(), name: '黄昏酒馆', description: '灯光昏暗，空气中弥漫着麦酒和旧木头的味道。' },
                { id: nanoid(), name: '图书馆禁区', description: '布满灰尘的书架高耸入云，寂静无声。' },
                { id: nanoid(), name: '废弃空间站', description: '透过破碎的舷窗，可以看到远方的星云。' },
            ];
        }
    });

    return { scenes, isReady, addScene, updateScene, deleteScene };
});