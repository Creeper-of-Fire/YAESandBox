import {defineStore} from 'pinia';
import {computed} from 'vue';
import {useCharacterStore} from '#/features/characters/characterStore.ts';
import {useSceneStore} from '#/features/scenes/sceneStore.ts';
import {type Character, type Scene} from '#/types/models';
import {createPersistentState} from '#/composables/createPersistentState';

const STORAGE_KEY = 'era-lite-session';

interface SessionState
{
    protagonistId: string | null;
    interactTargetId: string | null;
    currentSceneId: string | null;
}

export const useSessionStore = defineStore(STORAGE_KEY, () =>
{
    const {state: session, isReady} = createPersistentState<SessionState>(STORAGE_KEY, {
        protagonistId: null,
        interactTargetId: null,
        currentSceneId: null,
    });

    // --- Computed Refs for easier access and assignment ---
    const protagonistId = computed({
        get: () => session.value.protagonistId,
        set: (val) =>
        {
            session.value.protagonistId = val;
        }
    });
    const interactTargetId = computed({
        get: () => session.value.interactTargetId,
        set: (val) =>
        {
            session.value.interactTargetId = val;
        }
    });
    const currentSceneId = computed({
        get: () => session.value.currentSceneId,
        set: (val) =>
        {
            session.value.currentSceneId = val;
        }
    });

    // --- Dependencies ---
    const characterStore = useCharacterStore();
    const sceneStore = useSceneStore();

    // --- Actions ---
    function setProtagonist(id: string)
    {
        if (interactTargetId.value === id)
        {
            interactTargetId.value = protagonistId.value;
        }
        protagonistId.value = id;
    }

    function setInteractTarget(id: string)
    {
        if (protagonistId.value === id)
        {
            protagonistId.value = interactTargetId.value;
        }
        interactTargetId.value = id;
    }

    function setCurrentScene(id: string)
    {
        currentSceneId.value = id;
    }

    function clearSelections()
    {
        protagonistId.value = null;
        interactTargetId.value = null;
        currentSceneId.value = null;
    }

    // --- Getters (Computed) ---
    const selectedProtagonist = computed((): Character | undefined =>
    {
        return protagonistId.value
            ? characterStore.characters.find(c => c.id === protagonistId.value)
            : undefined;
    });

    const selectedInteractTarget = computed((): Character | undefined =>
    {
        return interactTargetId.value
            ? characterStore.characters.find(c => c.id === interactTargetId.value)
            : undefined;
    });

    const selectedScene = computed((): Scene | undefined =>
    {
        return currentSceneId.value
            ? sceneStore.scenes.find(s => s.id === currentSceneId.value)
            : undefined;
    });

    const isReadyForInteraction = computed(() =>
    {
        return !!(selectedProtagonist.value && selectedInteractTarget.value && selectedScene.value);
    });

    return {
        protagonistId,
        interactTargetId,
        currentSceneId,
        isSessionReady: isReady,
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