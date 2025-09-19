// src/game-logic/selectionStore.ts

import {defineStore} from 'pinia';
import {computed, ref} from 'vue';
import type {SelectionDetails} from './types';
import type {IGameEntity} from "#/game-logic/entity/entity.ts";
import {GameObjectEntity} from "#/game-logic/entity/gameObject/GameObjectEntity.ts";

export const useSelectionStore = defineStore('selection', () =>
{
    // --- State ---
    const selectedEntities = ref<IGameEntity[]>([]);

    // --- Getters ---
    const hasSelection = computed(() => selectedEntities.value.length > 0);

    const selectedObjects = computed(() =>
    {
        return selectedEntities.value
            .filter((e): e is GameObjectEntity => e instanceof GameObjectEntity)
            .map(obj => ({
                id: obj.id,
                type: obj.type,
                // 未来可以在这里暴露更多用于UI的信息，如obj.properties.name
            }));
    });

    const selectedFields = computed(() => []);
    const selectedParticles = computed(() =>  []);


    // --- Actions ---
    function selectEntities(entities: IGameEntity[])
    {
        selectedEntities.value = entities;
    }

    function clearSelection()
    {
        selectedEntities.value = [];
    }

    return {
        // state (via computed)
        selectedObjects,
        selectedFields,
        selectedParticles,
        // getters
        hasSelection,
        // actions
        selectEntities,
        clearSelection,
    };
});