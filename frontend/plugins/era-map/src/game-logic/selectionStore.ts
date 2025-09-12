// src/game-logic/selectionStore.ts

import {defineStore} from 'pinia';
import {ref, computed} from 'vue';
import type {SelectionDetails} from './types';

export const useSelectionStore = defineStore('selection', () => {
    // --- State ---
    const currentSelection = ref<SelectionDetails | null>(null);

    // --- Getters ---
    const hasSelection = computed(() =>
        currentSelection.value !== null &&
        (currentSelection.value.objects.length > 0 ||
            currentSelection.value.fields.length > 0 ||
            currentSelection.value.particles.length > 0)
    );

    const selectedObjects = computed(() => currentSelection.value?.objects ?? []);
    const selectedFields = computed(() => currentSelection.value?.fields ?? []);
    const selectedParticles = computed(() => currentSelection.value?.particles ?? []);


    // --- Actions ---
    function selectDetails(details: SelectionDetails) {
        currentSelection.value = details;
    }

    function clearSelection() {
        currentSelection.value = null;
    }

    return {
        // state (via computed)
        selectedObjects,
        selectedFields,
        selectedParticles,
        // getters
        hasSelection,
        // actions
        selectDetails,
        clearSelection,
    };
});