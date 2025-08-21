import {defineStore} from 'pinia';
import {ref, watch} from 'vue';
import {type Scene} from '#/types/models';
import {nanoid} from 'nanoid';
import localforage from 'localforage';

const STORAGE_KEY = 'era-lite-scenes';

export const useSceneStore = defineStore(STORAGE_KEY, () =>
{
    const scenes = ref<Scene[]>([
        // 初始数据
        {id: nanoid(), name: '黄昏酒馆', description: '灯光昏暗，空气中弥漫着麦酒和旧木头的味道。'},
        {id: nanoid(), name: '图书馆禁区', description: '布满灰尘的书架高耸入云，寂静无声。'},
        {id: nanoid(), name: '废弃空间站', description: '透过破碎的舷窗，可以看到远方的星云。'},
    ]);

    // 加载与持久化逻辑
    localforage.getItem<Scene[]>(STORAGE_KEY).then(savedScenes =>
    {
        if (savedScenes && savedScenes.length > 0)
        {
            scenes.value = savedScenes;
        }
    });

    watch(scenes, (newScenes) =>
    {
        localforage.setItem(STORAGE_KEY, newScenes);
    }, {deep: true});

    return {scenes};
});