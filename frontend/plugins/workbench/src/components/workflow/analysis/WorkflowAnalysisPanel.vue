<!-- src/components/workflow/analysis/WorkflowAnalysisPanel.vue -->
<template>
  <div class="workflow-analysis-panel">
    <!-- 加载状态 -->
    <n-skeleton v-if="isLoading" :repeat="3" :sharp="false" text/>

    <!-- 成功状态：渲染事件树 -->
    <WorkflowEmittedEventsTree
        v-else-if="analysisReport && analysisReport.workflowEmittedEvents"
        :events="analysisReport.workflowEmittedEvents"
    />

    <!-- 工作流级别的校验状态指示器 -->
    <ValidationStatusIndicator
        v-if="validationInfo"
        :validation-info="validationInfo"
    />

    <!-- 空状态 -->
    <n-empty v-else description="暂无分析报告"/>
  </div>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {NEmpty, NSkeleton} from 'naive-ui';
import type {WorkflowConfig} from '#/types/generated/workflow-config-api-client';
import {useWorkflowAnalysis} from '#/composables/useWorkflowAnalysis';
import WorkflowEmittedEventsTree from './WorkflowEmittedEventsTree.vue';
import ValidationStatusIndicator from "#/components/share/validationInfo/ValidationStatusIndicator.vue";
import {useValidationInfo} from "#/components/share/validationInfo/useValidationInfo.ts";

const props = defineProps<{
  workflow: WorkflowConfig;
}>();

// 使用 useWorkflowAnalysis Composable 获取分析报告
const {analysisReport, isLoading} = useWorkflowAnalysis(
    computed(() => props.workflow)
);

const messagesRef = computed(() =>
{
  if (!analysisReport.value) return [];
  // 合并全局消息和所有连接消息
  const allMessages = [...analysisReport.value.globalMessages];
  Object.values(analysisReport.value.connectionMessages).forEach(msgs => allMessages.push(...msgs));
  return allMessages;
});
const {validationInfo} = useValidationInfo(messagesRef);
</script>

<style scoped>
.workflow-analysis-panel {
  margin-bottom: 16px;
}
</style>