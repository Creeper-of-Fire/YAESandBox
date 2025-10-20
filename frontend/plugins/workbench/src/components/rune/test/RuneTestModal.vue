<!-- src/components/rune/test/RuneTestModal.vue -->
<template>
  <n-modal
      :show="show"
      :mask-closable="false"
      preset="card"
      style="width: 90%; max-width: 1200px;"
      title="符文功能测试"
      @update:show="$emit('update:show', $event)"
  >
    <n-grid :cols="2" :x-gap="16">
      <!-- 左侧：输入配置 -->
      <n-gi>
        <n-h4>模拟输入</n-h4>
        <n-p depth="3">根据符文分析出的输入变量，为它们提供模拟值。</n-p>
        <n-spin :show="isAnalysisLoading">
          <div v-if="consumedVariables && consumedVariables.length > 0">
            <n-form label-placement="top">
              <n-form-item v-for="variable in consumedVariables" :key="variable.name" :label="variable.name">
                <template #label>
                  <VarWithSpecTag :is-optional="variable.isOptional" :spec-def="variable.def" :var-name="variable.name"/>
                </template>
                <n-input
                    v-model:value="mockInputs[variable.name]"
                    :placeholder="`输入 ${variable.name} 的值 (JSON格式)`"
                    type="textarea"
                    :autosize="{ minRows: 2, maxRows: 6 }"
                />
              </n-form-item>
            </n-form>
          </div>
          <n-empty v-else description="此符文没有需要输入的变量"/>
        </n-spin>

        <n-button
            :loading="isTesting"
            block
            strong
            type="primary"
            style="margin-top: 16px;"
            @click="handleRunTest"
        >
          <template #icon>
            <n-icon :component="PlayIcon"/>
          </template>
          执行测试
        </n-button>
      </n-gi>

      <!-- 右侧：测试结果 -->
      <n-gi>
        <n-h4>测试结果</n-h4>
        <div class="result-panel">
          <n-spin :show="isTesting">
            <div v-if="testError">
              <n-alert title="测试执行失败" type="error">
                {{ testError }}
              </n-alert>
            </div>
            <div v-else-if="testResult">
              <n-alert :title="testResult.isSuccess ? '测试成功' : '测试失败'" :type="testResult.isSuccess ? 'success' : 'error'">
                <span v-if="!testResult.isSuccess">{{ testResult.errorMessage }}</span>
              </n-alert>

              <n-divider title-placement="left">输出变量</n-divider>
              <n-code v-if="testResult.producedOutputs" :code="JSON.stringify(testResult.producedOutputs, null, 2)" language="json" />
              <n-empty v-else description="没有输出变量" />

              <n-divider title-placement="left">调试信息</n-divider>
              <n-code v-if="testResult.debugInfo" :code="JSON.stringify(testResult.debugInfo, null, 2)" language="json" />
              <n-empty v-else description="没有调试信息" />
            </div>
            <n-empty v-else description="点击“执行测试”查看结果"/>
          </n-spin>
        </div>
      </n-gi>
    </n-grid>
  </n-modal>
</template>

<script lang="ts" setup>
import {computed, ref, toRef, watchEffect} from 'vue';
import {
  NModal, NGrid, NGi, NH4, NP, NSpin, NForm, NFormItem, NInput, NEmpty, NButton, NIcon, NAlert, NDivider, NCode
} from 'naive-ui';
import type {AbstractRuneConfig} from "#/types/generated/workflow-config-api-client";
import {useRuneAnalysis} from "#/composables/useRuneAnalysis.ts";
import {useRuneTester} from "#/composables/useRuneTester.ts";
import {PlayIcon} from "@yaesandbox-frontend/shared-ui/icons";
import VarWithSpecTag from "#/components/share/varSpec/VarWithSpecTag.vue";

const props = defineProps<{
  show: boolean;
  rune: AbstractRuneConfig;
}>();

defineEmits(['update:show']);

// 1. 使用分析 Composable 获取输入变量
const runeRef = toRef(props, 'rune');
const { analysisResult, isLoading: isAnalysisLoading } = useRuneAnalysis(runeRef);
const consumedVariables = computed(() => analysisResult.value?.consumedVariables);

// 2. 模拟输入的数据模型
const mockInputs = ref<Record<string, any>>({});

// 当分析结果变化时，初始化或更新 mockInputs 的 keys
watchEffect(() => {
  const newInputs: Record<string, any> = {};
  consumedVariables.value?.forEach(v => {
    // 保留已有的值，否则设为 null 字符串，方便用户编辑
    newInputs[v.name] = mockInputs.value[v.name] ?? 'null';
  });
  mockInputs.value = newInputs;
});

// 3. 使用测试 Composable
const { result: testResult, isLoading: isTesting, error: testError, executeTest } = useRuneTester();

// 4. 执行测试
async function handleRunTest() {
  await executeTest(props.rune, mockInputs.value);
}
</script>

<style scoped>
.result-panel {
  border: 1px solid #eee;
  padding: 16px;
  border-radius: 4px;
  min-height: 400px;
  background-color: #fafafa;
}
</style>