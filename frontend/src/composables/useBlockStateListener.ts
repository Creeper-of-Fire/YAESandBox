// composables/useBlockStateListener.ts
import { ref, watchEffect, onBeforeUnmount, readonly } from 'vue';
import type { Ref } from 'vue';
import { eventBus } from '@/services/eventBus';

// 可以定义事件负载的类型，如果事件总线发送了数据的话
// type WorldStateChangeEvent = { changedEntityIds?: string[] };
// type GameStateChangeEvent = { changedSettings?: string[] };

export function useBlockStateListener(blockIdRef: Ref<string | null>) {
    const worldStateChangedSignal = ref(0); // 简单地用一个递增数字表示收到了信号
    const gameStateChangedSignal = ref(0);   // 或者使用 boolean ref

    let cleanupFunctions: (() => void)[] = [];

    const clearListeners = () => {
        cleanupFunctions.forEach(cleanup => cleanup());
        cleanupFunctions = [];
        // console.log(`useBlockStateListener: Listeners cleared for previous block.`);
    };

    // 使用 watchEffect 来响应 blockIdRef 的变化并管理订阅
    watchEffect((onCleanup) => {
        clearListeners(); // 清理旧的监听器

        const currentBlockId = blockIdRef.value;
        if (!currentBlockId) {
            console.log(`useBlockStateListener: No blockId provided, listeners inactive.`);
            return; // 如果 blockId 为空，则不设置监听器
        }

        const worldStateEventName = `${currentBlockId}:WorldStateChanged` as const;
        const gameStateEventName = `${currentBlockId}:GameStateChanged` as const;

        // 定义处理器
        const handleWorldStateChange = (/* payload?: WorldStateChangeEvent */) => {
            console.log(`useBlockStateListener: Received ${worldStateEventName}`);
            worldStateChangedSignal.value++; // 触发信号
        };
        const handleGameStateChange = (/* payload?: GameStateChangeEvent */) => {
            console.log(`useBlockStateListener: Received ${gameStateEventName}`);
            gameStateChangedSignal.value++; // 触发信号
        };

        // 订阅事件
        eventBus.on(worldStateEventName, handleWorldStateChange);
        eventBus.on(gameStateEventName, handleGameStateChange);
        console.log(`useBlockStateListener: Subscribed to events for block ${currentBlockId}`);

        // 将取消订阅函数添加到清理列表
        const cleanupWorld = () => eventBus.off(worldStateEventName, handleWorldStateChange);
        const cleanupGame = () => eventBus.off(gameStateEventName, handleGameStateChange);
        cleanupFunctions.push(cleanupWorld, cleanupGame);

        // watchEffect 的清理函数
        onCleanup(() => {
            console.log(`useBlockStateListener: Cleaning up listeners for block ${currentBlockId}`);
            clearListeners();
        });
    });

    // 确保组件卸载时也清理
    onBeforeUnmount(() => {
        clearListeners();
    });

    return {
        // 返回响应式的信号，组件可以 watch 这两个 ref
        worldStateChangedSignal: readonly(worldStateChangedSignal),
        gameStateChangedSignal: readonly(gameStateChangedSignal),
        // 不直接返回 fetch 函数，让组件自己决定如何获取数据
    };
}