<template>
  <div class="editor-container">
    <!-- 1. 默认的表单字段，通过 v-model 绑定到 props.modelValue -->
    <NForm label-placement="left" label-width="auto" :style="{ maxWidth: '800px' }">
      <NFormItem label="输入变量名">
        <NInput v-model:value="formValue.inputVariableName" @update:value="updateModel" />
      </NFormItem>
      <NFormItem label="CSS 选择器">
        <NInput v-model:value="formValue.selector" @update:value="updateModel" type="textarea" :autosize="{ minRows: 2 }" />
      </NFormItem>
      <NFormItem label="提取模式">
        <NSelect v-model:value="formValue.extractionMode" @update:value="updateModel" :options="extractionModeOptions" />
      </NFormItem>
      <!-- 条件渲染 -->
      <div v-if="formValue.extractionMode === 'Attribute'">
        <NFormItem  label="属性名">
          <NInput v-model:value="formValue.attributeName" @update:value="updateModel" placeholder="例如：src, href, data-id" />
        </NFormItem>
      </div>
      <NFormItem label="返回格式">
        <NSelect v-model:value="formValue.returnFormat" @update:value="updateModel" :options="returnFormatOptions" />
      </NFormItem>
      <NFormItem label="输出变量名">
        <NInput v-model:value="formValue.outputVariableName" @update:value="updateModel" />
      </NFormItem>
    </NForm>

    <NDivider />

    <!-- 2. 测试区域 -->
    <div class="test-section">
      <NFormItem label="测试输入文本">
        <NInput type="textarea" v-model:value="sampleInput" placeholder="在此处粘贴带标签的示例文本..." :autosize="{ minRows: 5, maxRows: 15 }" />
      </NFormItem>
      <NButton @click="runTest" :loading="isLoading" type="primary">执行测试</NButton>

      <!-- 3. 测试结果显示 -->
      <NCollapseTransition :show="!!testResult || !!testError">
        <div class="result-section">
          <NAlert v-if="testError" title="测试失败" type="error" :style="{ marginTop: '16px' }">
            <pre>{{ testError }}</pre>
          </NAlert>
          <div v-if="testResult" :style="{ marginTop: '16px' }">
            <p><strong>测试结果:</strong></p>
            <NCode :code="formattedResult" language="json" word-wrap />
            <p :style="{ marginTop: '10px' }"><strong>调试信息:</strong></p>
            <NCode :code="formattedDebugInfo" language="json" word-wrap />
          </div>
        </div>
      </NCollapseTransition>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, inject, watch, reactive } from 'vue';
import { NForm, NFormItem, NInput, NButton, NDivider, NCode, NAlert, NSelect, NCollapseTransition } from 'naive-ui';

// Vue Form Generator 会通过 props 传入 modelValue
const props = defineProps<{
  modelValue: any; // 接收配置对象
}>();

// 同时，也需要 emit update:modelValue 事件来通知父组件更新
const emit = defineEmits(['update:modelValue']);

// 使用 inject 获取主程序提供的 axios 实例
const axios = inject('axios') as any;

// 为了避免直接修改props，我们创建一个响应式的本地副本
const formValue = reactive({ ...props.modelValue });

// 监听外部变化，同步到本地
watch(() => props.modelValue, (newValue) => {
  Object.assign(formValue, newValue);
}, { deep: true });

// 每次本地表单更新时，通知父组件
const updateModel = () => {
  emit('update:modelValue', { ...formValue });
};

const extractionModeOptions = [
  { label: '纯文本', value: 'TextContent' },
  { label: '内部HTML', value: 'InnerHtml' },
  { label: '完整HTML', value: 'OuterHtml' },
  { label: '提取属性', value: 'Attribute' },
];

const returnFormatOptions = [
  { label: '仅第一个', value: 'First' },
  { label: '作为列表', value: 'AsList' },
  { label: '作为JSON字符串', value: 'AsJsonString' },
];

const sampleInput = ref('<div class="item">Hello <b>World</b>!</div>\n<div class="item" data-id="123">Another item.</div>');
const testResult = ref<any>(null);
const testError = ref('');
const testDebugInfo = ref<any>(null);
const isLoading = ref(false);

const formattedResult = computed(() => testResult.value ? JSON.stringify(testResult.value, null, 2) : '');
const formattedDebugInfo = computed(() => testDebugInfo.value ? JSON.stringify(testDebugInfo.value, null, 2) : '');

async function runTest() {
  if (!axios) {
    testError.value = "错误：未能获取到 axios 实例。";
    return;
  }
  isLoading.value = true;
  testResult.value = null;
  testError.value = '';
  testDebugInfo.value = null;

  const requestPayload = {
    runeConfig: formValue,
    sampleInputText: sampleInput.value,
  };

  try {
    const response = await axios.post('/api/v1/plugins/text-parser/test-parser/run-test', requestPayload);
    const data = response.data;

    if (data.isSuccess) {
      testResult.value = data.result;
    } else {
      testError.value = data.errorMessage || '未知错误';
    }
    testDebugInfo.value = data.debugInfo;

  } catch (error: any) {
    testError.value = error.response?.data?.title || error.message || '请求失败';
  } finally {
    isLoading.value = false;
  }
}
</script>

<style scoped>
.editor-container {
  padding: 10px;
  border: 1px solid #eee;
  border-radius: 4px;
}
.test-section {
  margin-top: 20px;
}
.result-section {
  margin-top: 16px;
  padding: 16px;
  background-color: #f7f7f7;
  border-radius: 4px;
}
</style>
