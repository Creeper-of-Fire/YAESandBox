// src/game-logic/selectionStore.ts

import {defineStore} from 'pinia';
import {computed, ref} from 'vue';
import type {SelectionDetails} from './types';
import type {IGameEntity} from "#/game-logic/entity/IGameEntity.ts";
import {type EntityInfo, EntityInfoType} from "#/game-logic/entity/entityInfo.ts";

export const useSelectionStore = defineStore('selection', () =>
{
    // --- State ---
    const selectedGridPos = ref<{ x: number, y: number } | null>(null);
    const selectedEntitiesAtPos = ref<IGameEntity[]>([]);

    // --- Getters ---
    const hasSelection = computed(() => selectedEntitiesAtPos.value.length > 0);

    const selectionDetails = computed((): EntityInfo[] => {
        if (!selectedGridPos.value) return [];

        const { x, y } = selectedGridPos.value;
        return selectedEntitiesAtPos.value
            .map(entity => entity.getInfoAt(x, y))
            .filter((info): info is EntityInfo => info !== null);
    });

    const selectedObjects = computed(() =>
        selectionDetails.value.filter(d => d.type === EntityInfoType.GameObject)
    );
    const selectedFields = computed(() =>
        selectionDetails.value.filter(d => d.type === EntityInfoType.Field)
    );
    const selectedParticles = computed(() =>
        selectionDetails.value.filter(d => d.type === EntityInfoType.Particle)
    );

    // --- Actions ---
    function selectEntities(entities: IGameEntity[], gridPos: { x: number, y: number }) {
        selectedEntitiesAtPos.value = entities;
        selectedGridPos.value = gridPos;
    }

    function clearSelection() {
        selectedEntitiesAtPos.value = [];
        selectedGridPos.value = null;
    }

    return {
        // state (via computed)
        selectionDetails,
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