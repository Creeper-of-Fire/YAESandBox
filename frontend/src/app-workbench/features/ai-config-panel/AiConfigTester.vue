<template>
  <div> <!-- 根元素，用于包裹按钮和 Modal -->
    <!-- 触发测试模态框的按钮 -->
    <n-button
        type="info"
        secondary
        @click="handleOpenTestModal"
        :disabled="!formDataCopy || !moduleType"
        title="点击测试当前选中的 AI 模型配置"
    >
      测试当前模型
    </n-button>

    <!-- 测试模态框 -->
    <n-modal
        v-model:show="showTestModal"
        :mask-closable="false"
        preset="card"
        title="注意：测试对象为当前编辑中的数据。"
        style="width: 600px; max-width: 90vw;"
        :bordered="true"
        size="huge"
        role="dialog"
        aria-modal="true"
        @after-leave="resetModalState"
    >
      <n-spin :show="isLoading" description="正在请求 AI 服务...">
        <n-alert type="info" :show-icon="false" style="margin-bottom: 16px;">
          当前测试对象：配置集 "{{ configSetName }}" 中的 "{{ moduleType }}" 模型。
        </n-alert>

        <n-flex vertical>
          <!-- 测试文本输入区域 -->
          <n-form-item label="输入测试文本">
            <n-input
                type="textarea"
                v-model:value="testText"
                placeholder="在此输入你想发送给 AI 进行测试的内容..."
                :autosize="{ minRows: 5, maxRows: 15 }"
                :disabled="isLoading"
            />
          </n-form-item>

          <!-- 执行测试按钮 -->
          <n-button
              type="primary"
              @click="runTest"
              :loading="isLoading"
              :disabled="!testText.trim() || isLoading || !formDataCopy || !moduleType"
              block
          >
            执行测试
          </n-button>

          <!-- 测试结果显示区域 -->
          <div v-if="testResult || errorMessage" style="margin-top: 20px;">
            <n-h4>测试结果:</n-h4>
            <!-- 错误信息展示 -->
            <n-alert v-if="errorMessage" title="测试失败" type="error" closable @close="errorMessage = null">
              {{ errorMessage }}
            </n-alert>
            <!-- 成功结果展示 -->
            <n-card v-if="testResult && !errorMessage" size="small" embedded>
              <!-- 使用 pre-wrap 来允许换行 -->
              <n-text style="white-space: pre-wrap;">{{ testResult }}</n-text>
            </n-card>
            <!-- 清除结果按钮 -->
            <n-button tertiary size="small" @click="clearResult" style="margin-top: 8px;" v-if="testResult || errorMessage">
              清除当前结果
            </n-button>
          </div>
          <!-- 初始状态或清除后 -->
          <n-empty v-else-if="!isLoading" description="尚未执行测试或结果已清除。" style="margin-top: 20px;"/>

        </n-flex>
      </n-spin>
      <template #footer>
        <n-button @click="showTestModal = false">关闭</n-button>
      </template>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import {ref} from 'vue';
import {NAlert, NButton, NCard, NEmpty, NFlex, NFormItem, NH4, NInput, NModal, NSpin, NText, useMessage,} from 'naive-ui';
import {AiConfigurationsService} from '@/app-workbench/types/generated/ai-config-api-client';
import type {AbstractAiProcessorConfig} from "@/app-workbench/types/generated/ai-config-api-client";

// --- 组件 Props ---
// 和之前一样，从父组件接收这些参数
const props = defineProps<{
  formDataCopy: AbstractAiProcessorConfig | null; // 配置集 UUID
  configSetName: string | null; // 配置集名称 (用于显示)
  moduleType: string | null;    // AI 模型类型
}>();

const defaultTestText = '只回答“test”';

// --- 内部状态 ---
const message = useMessage(); // Naive UI 的消息提示工具
const showTestModal = ref(false); // 控制 Modal 的显示与隐藏
const testText = ref(defaultTestText); // 双向绑定测试输入文本
const testResult = ref<string | null>(null); // 存储测试成功的结果
const isLoading = ref(false); // 控制加载状态（Modal内部的加载）
const errorMessage = ref<string | null>(null); // 存储错误信息

// --- 方法 ---

/**
 * 打开测试模态框，并可能进行一些初始化
 */
function handleOpenTestModal()
{
  // 可以在这里重置一些状态，如果需要每次打开都是全新的话
  // 但由于父组件使用 :key，大部分情况下组件实例已重置
  // clearResult(); // 比如清除上一次的结果
  // testText.value = ''; // 清空输入框
  showTestModal.value = true;
}

/**
 * 执行测试的异步函数
 */
async function runTest()
{
  if (!props.formDataCopy || !props.moduleType)
  {
    message.error('配置集 UUID 或模型类型无效，无法执行测试。');
    return;
  }
  if (!testText.value.trim())
  {
    message.warning('请输入测试文本。');
    return;
  }

  isLoading.value = true;
  // 清空上一次的特定错误或结果，准备接收新的
  errorMessage.value = null;
  // testResult.value = null; // 可以选择在请求前清除，或请求后根据成功/失败清除

  try
  {
    testResult.value = await AiConfigurationsService.postApiAiConfigurationsAiConfigTest({
      requestBody: {configJson: props.formDataCopy, testText: testText.value},
      moduleType: props.moduleType,
    });
    message.success('测试成功！');

  } catch (error: any)
  {
    console.error("AI 配置测试失败:", error);
    const detail = error.body?.detail || error.message || '发生未知错误';
    errorMessage.value = `请求失败: ${detail}`;
    testResult.value = null; // 如果测试失败，确保清除成功的测试结果
    message.error(`测试失败: ${detail}`);
  } finally
  {
    isLoading.value = false;
  }
}

/**
 * 清除测试结果和错误信息
 */
function clearResult()
{
  testResult.value = null;
  errorMessage.value = null;
}

/**
 * 重置模态框内部状态，在模态框关闭后调用
 * 这样下次打开时，内容是干净的 (除了 testText，可以根据需求决定是否保留)
 */
function resetModalState()
{
  isLoading.value = false;
  errorMessage.value = null;
  testResult.value = null;
  // testText.value = ''; // 如果希望每次打开都清空输入框，取消此行注释
}

</script>

<style scoped>
/* 如果需要对按钮或 Modal 内部元素进行特定样式调整，可以在这里添加 */
.n-button[title] { /* 确保 title 提示能正常工作 */
  cursor: pointer;
}
</style>