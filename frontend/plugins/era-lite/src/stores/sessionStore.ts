import {defineStore} from 'pinia';
import {computed} from 'vue';
import {useCharacterStore} from '#/features/characters/characterStore.ts';
import {useSceneStore} from '#/features/scenes/sceneStore.ts';
import {type Character, type Scene} from '#/types/models';
import {createPersistentState} from '#/composables/createPersistentState';

const STORAGE_KEY = 'era-lite-session';

interface SessionState
{
    playerCharacterId: string | null;
    targetCharacterId: string | null;
    currentSceneId: string | null;
}

export const useSessionStore = defineStore(STORAGE_KEY, () =>
{
    const {state: session, isReady} = createPersistentState<SessionState>(STORAGE_KEY, {
        playerCharacterId: null,
        targetCharacterId: null,
        currentSceneId: null,
    });

    // --- Computed Refs for easier access and assignment ---
    const playerCharacterId = computed({
        get: () => session.value.playerCharacterId,
        set: (val) =>
        {
            session.value.playerCharacterId = val;
        }
    });
    const targetCharacterId = computed({
        get: () => session.value.targetCharacterId,
        set: (val) =>
        {
            session.value.targetCharacterId = val;
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
    function setPlayerCharacter(id: string)
    {
        if (targetCharacterId.value === id)
        {
            targetCharacterId.value = playerCharacterId.value;
        }
        playerCharacterId.value = id;
    }

    function setTargetCharacter(id: string)
    {
        if (playerCharacterId.value === id)
        {
            playerCharacterId.value = targetCharacterId.value;
        }
        targetCharacterId.value = id;
    }

    function setCurrentScene(id: string)
    {
        currentSceneId.value = id;
    }

    function clearSelections()
    {
        playerCharacterId.value = null;
        targetCharacterId.value = null;
        currentSceneId.value = null;
    }

    // --- Getters (Computed) ---
    const selectedPlayerCharacter = computed((): Character | undefined =>
    {
        return playerCharacterId.value
            ? characterStore.characters.find(c => c.id === playerCharacterId.value)
            : undefined;
    });

    const selectedTargetCharacter = computed((): Character | undefined =>
    {
        return targetCharacterId.value
            ? characterStore.characters.find(c => c.id === targetCharacterId.value)
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
        return !!(selectedPlayerCharacter.value && selectedTargetCharacter.value && selectedScene.value);
    });

    return {
        playerCharacterId,
        targetCharacterId,
        currentSceneId,
        isSessionReady: isReady,
        setPlayerCharacter,
        setTargetCharacter,
        setCurrentScene,
        clearSelections,
        selectedPlayerCharacter,
        selectedTargetCharacter,
        selectedScene,
        isReadyForInteraction,
    };
});