<template>
  <div class="execution-interface">
    <div class="header">
      <h4>测试: {{ config.name }} ({{ configType === 'workflow' ? '工作流' : '枢机' }})</h4>
    </div>
    <n-scrollbar class="content-area">
      <div class="form-section">
        <n-h5>输入参数</n-h5>
        <n-form v-if="paramsToFill.length > 0">
          <n-form-item v-for="param in paramsToFill" :key="param" :label="param">
            <n-input type="textarea" :autosize="{minRows:1, maxRows: 5}" v-model:value="paramValues[param]" :placeholder="`输入 ${param} 的值`" />
          </n-form-item>
        </n-form>
        <n-empty v-else description="此项无需输入参数" />
      </div>
      <n-button type="primary" block @click="handleExecute" :loading="isLoading">执行</n-button>
      <div class="result-section" v-if="executionResult !== null">
        <n-h5>执行结果</n-h5>
        <n-card size="small" :bordered="true">
          <n-alert v-if="!executionResult.isSuccess" :title="`执行失败: ${executionResult.errorCode}`" type="error">
            {{ executionResult.errorMessage }}
          </n-alert>
          <pre v-else>{{ executionResult.errorMessage || '[执行成功但无文本返回]' }}</pre>
        </n-card>
      </div>
    </n-scrollbar>
  </div>
</template>

<script setup lang="ts">
import {ref, reactive, computed, watch} from 'vue';
import { NScrollbar, NInput, NButton, NForm, NFormItem, NEmpty, NH5, NCard, NAlert, useMessage } from 'naive-ui';
import type { WorkflowConfig, TuumConfig } from '@yaesandbox-frontend/plugin-workbench/types/generated/workflow-config-api-client';
import {type WorkflowExecutionResult, WorkflowExecutionService} from "#/types/generated/workflow-test-api-client";

const props = defineProps<{
  config: WorkflowConfig | TuumConfig;
  configType: 'workflow' | 'tuum';
}>();

const message = useMessage();
const isLoading = ref(false);
const executionResult = ref<WorkflowExecutionResult | null>(null);
const paramValues = reactive<Record<string, string>>({});

// 1. computed 属性现在只负责计算和返回参数列表，不再有任何副作用。
const paramsToFill = computed<string[]>(() => {
  if (props.configType === 'workflow') {
    return (props.config as WorkflowConfig).workflowInputs || [];
  } else {
    const tuumConfig = props.config as TuumConfig;
    const globalVars = Object.keys(tuumConfig.inputMappingsList || {});
    return [...new Set(globalVars)];
  }
});

// 2. 使用 watch 监听 `paramsToFill` 的变化，来安全地执行副作用（初始化 paramValues）。
watch(paramsToFill, (newParams, oldParams) => {
  // 清理掉不再需要的旧参数
  const newParamsSet = new Set(newParams);
  const oldParamsSet = oldParams || [];
  for (const oldParam of oldParamsSet) {
    if (!newParamsSet.has(oldParam)) {
      delete paramValues[oldParam];
    }
  }

  // 为新出现的参数设置初始空字符串值
  for (const newParam of newParams) {
    if (!Object.prototype.hasOwnProperty.call(paramValues, newParam)) {
      paramValues[newParam] = '';
    }
  }

  // 清空上一次的执行结果
  executionResult.value = null;

}, { immediate: true }); // immediate: true 确保组件首次加载时就会执行一次


// `handleExecute` 函数保持不变，它现在可以安全地读取用户输入的值了。
async function handleExecute() {
  isLoading.value = true;
  executionResult.value = null;

  let requestBody;

  if (props.configType === 'workflow') {
    const workflowConfig = props.config as WorkflowConfig;
    requestBody = {
      workflowConfig: workflowConfig,
      workflowInputs: paramValues
    };
  } else {
    const tuumConfig = props.config as TuumConfig;
    const workflowInputsForTempWorkflow = [...new Set(Object.keys(tuumConfig.inputMappingsList))];
    const tempWorkflow: WorkflowConfig = {
      name: `测试枢机: ${tuumConfig.name}`,
      tuums: [tuumConfig],
      workflowInputs: workflowInputsForTempWorkflow
    };
    requestBody = {
      workflowConfig: tempWorkflow,
      workflowInputs: paramValues
    };
  }

  try {
    const result = await WorkflowExecutionService.postApiV1WorkflowExecutionExecute({ requestBody });
    executionResult.value = result;
  } catch (error: any) {
    const errorMsg = `请求失败: ${error.body?.detail || error.message || '未知错误'}`;
    message.error(errorMsg);
    executionResult.value = { isSuccess: false, errorMessage: errorMsg, errorCode: 'RequestError' };
  } finally {
    isLoading.value = false;
  }
}
</script>

<style scoped>
.execution-interface {
  display: flex;
  flex-direction: column;
  height: 100%;
}
.header {
  padding: 12px 16px;
  border-bottom: 1px solid #e8e8e8;
  flex-shrink: 0;
}
.header h4 {
  margin: 0;
}
.content-area {
  padding: 16px;
}
.form-section {
  margin-bottom: 24px;
}
.result-section {
  margin-top: 24px;
}
pre {
  white-space: pre-wrap;
  word-wrap: break-word;
  background-color: #f7f7f7;
  padding: 12px;
  border-radius: 4px;
}
</style>