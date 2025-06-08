// composables/useMicroWorkflow.ts
import {ref, onMounted, onBeforeUnmount, readonly} from 'vue';
import {v4 as uuidv4} from 'uuid';
import {eventBus} from '@/app-game/services/eventBus.ts';
import {signalrService} from '@/app-game/services/signalrService.ts'; // 用于触发
import type {TriggerMicroWorkflowRequestDto} from '@/app-game/types/generated/public-api-client';
import {StreamStatus, UpdateMode} from '@/app-game/types/generated/public-api-client'

interface MicroWorkflowResult
{
    content: string | null;
    status: StreamStatus;// 和MainWorkflow不同，MicroWorkflow的生命周期只能通过StreamStatus展示
    // 目前没加 script之类的
}

export function useMicroWorkflow()
{
    const targetId = ref(uuidv4()); // 为每次调用此 composable 生成唯一 ID
    const result = ref<MicroWorkflowResult>({content: null, status: StreamStatus.COMPLETE});
    const isLoading = ref(false);

    // 事件处理器
    const handleUpdate = (data: { content: string | null, status: StreamStatus, updateMode: string }) =>
    {
        console.log(`useMicroWorkflow (${targetId.value}): Received update`, data);
        result.value = {content: data.content, status: data.status};
        isLoading.value = data.status === StreamStatus.STREAMING;
        // 注意：这里简单替换内容。如果需要累积增量内容，需要更复杂的逻辑
        // TODO 增量内容的实现
    };

    // 订阅/取消订阅
    onMounted(() =>
    {
        const eventName = `microWorkflowUpdate:${targetId.value}` as const;
        eventBus.on(eventName, handleUpdate);
        console.log(`useMicroWorkflow (${targetId.value}): Subscribed to ${eventName}`);
    });

    onBeforeUnmount(() =>
    {
        const eventName = `microWorkflowUpdate:${targetId.value}` as const;
        eventBus.off(eventName, handleUpdate);
        console.log(`useMicroWorkflow (${targetId.value}): Unsubscribed from ${eventName}`);
    });

    // 触发函数
    const trigger = async (contextBlockId: string, workflowName: string, params: Record<string, any>) =>
    {
        if (isLoading.value)
        {
            console.warn(`useMicroWorkflow (${targetId.value}): Already loading, ignoring trigger.`);
            return;
        }
        isLoading.value = true;
        result.value = {content: null, status: StreamStatus.STREAMING}; // 重置状态

        const request: TriggerMicroWorkflowRequestDto = {
            requestId: uuidv4(),
            contextBlockId,
            targetElementId: targetId.value, // 使用 composable 的 ID
            workflowName,
            params,
        };

        try
        {
            await signalrService.triggerMicroWorkflow(request);
        } catch (error)
        {
            console.error(`useMicroWorkflow (${targetId.value}): Trigger failed`, error);
            // 错误已由 signalrService 通过 eventBus 发送，这里只需处理本地加载状态
            isLoading.value = false;
            // result 状态会被 eventBus 处理器更新为 Error
        }
        // 注意：isLoading 在收到 Complete 或 Error 事件时也会被 handleUpdate 更新
    };

    return {
        targetId: readonly(targetId), // 只读 Target ID，供调试或特殊场景
        result: readonly(result),     // 只读结果
        isLoading: readonly(isLoading), // 只读加载状态
        trigger,                      // 触发方法
    };
}