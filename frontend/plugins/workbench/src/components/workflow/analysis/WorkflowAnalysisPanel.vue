<!-- src/components/workflow/analysis/WorkflowAnalysisPanel.vue -->
<template>
  <div class="workflow-analysis-panel">
    <!-- 加载状态 -->
    <n-skeleton v-if="isLoading" :repeat="3" :sharp="false" text/>

    <!-- 分析报告存在时 -->
    <div v-else-if="analysisReport">
      <!-- 渲染事件树 -->
      <n-card
          v-if="hasEvents"
          :bordered="true"
          size="small"
          style="margin: 0; padding:0;"
      >
        <WorkflowEmittedEventsTree
            :events="analysisReport.workflowEmittedEvents"
        />
      </n-card>

      <!-- 工作流级别的校验状态指示器 -->
      <ValidationStatusIndicator
          v-if="hasValidationIssues"
          :validation-info="validationInfo!"
      />

      <!-- 当报告存在，但既没有事件也没有校验问题时，显示“清白”状态 -->
      <n-text v-if="!hasEvents && !hasValidationIssues" class="info-text" depth="3">
        分析完成，未发现可报告的事件或校验问题。
      </n-text>
    </div>

    <!-- 暂无分析报告 -->
    <n-text v-else class="info-text" depth="3">
      暂无分析报告。
    </n-text>
  </div>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {NSkeleton} from 'naive-ui';
import type {WorkflowConfig} from '#/types/generated/workflow-config-api-client';
import {useWorkflowAnalysis} from '#/composables/useWorkflowAnalysis';
import {WorkflowEmittedEventsTree} from '@yaesandbox-frontend/core-services/workflow';
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

// 计算属性，用于判断是否有内容需要显示，以简化模板逻辑
const hasEvents = computed(() => !!analysisReport.value?.workflowEmittedEvents && analysisReport.value.workflowEmittedEvents.length > 0);
const hasValidationIssues = computed(() => !!validationInfo.value);
</script>

<style scoped>
.workflow-analysis-panel {
  margin-bottom: 16px;
}

.info-text {
  display: block; /* 确保 n-text 表现为块级元素，可以设置内外边距 */
  padding: 16px 8px; /* 增加一些垂直内边距，让它不那么拥挤 */
  text-align: center; /* 居中显示，更符合空状态的观感 */
  font-size: 13px; /* 稍微调整字体大小 */
}
</style>