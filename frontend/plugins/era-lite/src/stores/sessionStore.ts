import { defineStore } from 'pinia';
import { ref, computed, watch } from 'vue';
import { useCharacterStore } from './characterStore';
import { useSceneStore } from './sceneStore';
import { type Character, type Scene } from '#/types/models';
import localforage from 'localforage';
import {useStorage} from "@vueuse/core";

const STORAGE_KEY = 'era-lite-session';

interface SessionState {
    protagonistId: string | null;
    interactTargetId: string | null;
    currentSceneId: string | null;
}

export const useSessionStore = defineStore(STORAGE_KEY, () => {
    // --- State ---
    const protagonistId = useStorage(`${STORAGE_KEY}-protagonistId`, null as string | null);
    const interactTargetId = useStorage(`${STORAGE_KEY}-interactTargetId`, null as string | null);
    const currentSceneId = useStorage(`${STORAGE_KEY}-currentSceneId`, null as string | null);

    // --- Dependencies ---
    const characterStore = useCharacterStore();
    const sceneStore = useSceneStore();

    // --- Actions ---
    function setProtagonist(id: string) {
        // 如果对方已经被选为交互对象，则交换位置
        if (interactTargetId.value === id) {
            interactTargetId.value = protagonistId.value;
        }
        protagonistId.value = id;
    }

    function setInteractTarget(id: string) {
        // 如果对方已经被选为主角，则交换位置
        if (protagonistId.value === id) {
            protagonistId.value = interactTargetId.value;
        }
        interactTargetId.value = id;
    }

    function setCurrentScene(id: string) {
        currentSceneId.value = id;
    }

    function clearSelections() {
        protagonistId.value = null;
        interactTargetId.value = null;
        currentSceneId.value = null;
    }

    // --- Getters (Computed) ---
    const selectedProtagonist = computed((): Character | undefined => {
        return protagonistId.value
            ? characterStore.characters.find(c => c.id === protagonistId.value)
            : undefined;
    });

    const selectedInteractTarget = computed((): Character | undefined => {
        return interactTargetId.value
            ? characterStore.characters.find(c => c.id === interactTargetId.value)
            : undefined;
    });

    const selectedScene = computed((): Scene | undefined => {
        return currentSceneId.value
            ? sceneStore.scenes.find(s => s.id === currentSceneId.value)
            : undefined;
    });

    const isReadyForInteraction = computed(() => {
        return !!(selectedProtagonist.value && selectedInteractTarget.value && selectedScene.value);
    });

    // --- Persistence ---
    const stateToPersist = computed((): SessionState => ({
        protagonistId: protagonistId.value,
        interactTargetId: interactTargetId.value,
        currentSceneId: currentSceneId.value,
    }));

    localforage.getItem<SessionState>(STORAGE_KEY).then(savedState => {
        if (savedState) {
            protagonistId.value = savedState.protagonistId;
            interactTargetId.value = savedState.interactTargetId;
            currentSceneId.value = savedState.currentSceneId;
        }
    });

    watch(stateToPersist, (newState) => {
        localforage.setItem(STORAGE_KEY, newState);
    }, { deep: true });


    return {
        protagonistId,
        interactTargetId,
        currentSceneId,
        setProtagonist,
        setInteractTarget,
        setCurrentScene,
        clearSelections,
        selectedProtagonist,
        selectedInteractTarget,
        selectedScene,
        isReadyForInteraction,
    };
});