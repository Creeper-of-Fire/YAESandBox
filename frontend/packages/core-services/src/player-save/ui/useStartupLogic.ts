import {computed, ref, watch} from 'vue';
import {useMessage} from 'naive-ui';
import {useGameSaveService} from "../save-service/injectKeys.ts";
import {useScopedStorage} from "../../composables.ts";
import {onBeforeRouteLeave} from 'vue-router';

export function useStartupLogic()
{
    const message = useMessage();
    const saveService = useGameSaveService();

    // --- 状态管理 ---
    const autoLoadEnabled = useScopedStorage('startup:auto-load-enabled', false);
    const showSaveManager = ref(false);
    const showNewGameModal = ref(false);
    const newGameName = ref('新的冒险');

    // --- 派生状态 ---
    const canContinue = computed(() => !!saveService.lastActiveSlotName.value);

    // 一个状态，用于防止 "弹回" bug
    const hasManuallyQuit = ref(false);

    // --- 自动加载逻辑 ---
    watch(
        // 监听多个源
        [
            () => saveService.isInitialized.value,
            () => autoLoadEnabled.value,
            () => saveService.activeSlot.value
        ],
        ([isReady, autoLoad, activeSlot], [oldIsReady, oldAutoLoad, oldActiveSlot]) =>
        {
            // 当游戏从 "已加载" 变为 "未加载" (null)，说明用户退出了游戏
            if (oldActiveSlot && !activeSlot)
            {
                hasManuallyQuit.value = true;
            }

            // 核心守卫条件：
            // 1. 服务必须初始化完成
            // 2. 自动加载开关必须打开
            // 3. 当前没有加载任何游戏 (activeSlot 为 null)
            // 4. 必须有“上次游戏”可供加载
            // 5. 用户不是刚刚手动退出游戏的
            if (isReady && autoLoad && !activeSlot && canContinue.value && !hasManuallyQuit.value)
            {
                message.loading('正在自动加载上次游戏...', {duration: 1500});
                setTimeout(() =>
                {
                    saveService.loadLastGame();
                }, 500);
            }

            // 当游戏成功加载时，重置“手动退出”标志，为下一次退出做准备
            if (!oldActiveSlot && activeSlot) {
                hasManuallyQuit.value = false;
            }
        },
        {immediate: true}
    );

    // 当用户离开 StartupView 时，重置 "手动退出" 标志
    // 这样下次回到 StartupView 时，自动加载逻辑又能正常工作了
    onBeforeRouteLeave(() =>
    {
        hasManuallyQuit.value = false;
    });

    // --- 事件处理器 ---
    async function handleContinue()
    {
        if (!canContinue.value) return;
        await saveService.loadLastGame();
    }

    function handleNewGame()
    {
        showNewGameModal.value = true;
    }

    function handleShowSaveManager()
    {
        showSaveManager.value = true;
    }

    async function confirmNewGame()
    {
        const trimmedName = newGameName.value.trim();
        if (!trimmedName)
        {
            message.error('存档名不能为空');
            return;
        }
        await saveService.startNewGame(trimmedName);
        showNewGameModal.value = false;
    }

    return {
        // 状态
        autoLoadEnabled,
        showSaveManager,
        showNewGameModal,
        newGameName,
        canContinue,

        // 方法
        handleContinue,
        handleNewGame,
        handleShowSaveManager,
        confirmNewGame,

        // 把 saveService 也透传出去，模板中可能还会用到
        saveService,
    };
}