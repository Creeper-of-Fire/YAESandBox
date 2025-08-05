<template>
  <div class="editor-container">
    <!-- 1. 表单字段，通过 v-model 绑定到 formValue -->
    <NForm
        label-placement="left"
        label-width="auto"
        :style="{ maxWidth: '800px' }"
        :model="formValue"
    >
      <!-- 通用配置 -->
      <NFormItem label="输入变量名" path="inputVariableName">
        <NInput v-model:value="formValue.inputVariableName" @update:value="updateModel"/>
      </NFormItem>

      <NFormItem label="操作模式" path="operationMode">
        <NSelect v-model:value="formValue.operationMode" @update:value="updateModel" :options="operationModeOptions"/>
      </NFormItem>

      <NFormItem label="CSS 选择器" path="selector">
        <NInput v-model:value="formValue.selector" @update:value="updateModel" type="textarea" :autosize="{ minRows: 2 }"/>
      </NFormItem>

      <NFormItem label="内容目标" path="matchContentMode">
        <NSelect v-model:value="formValue.matchContentMode" @update:value="updateModel" :options="matchContentModeOptions"/>
      </NFormItem>

      <!-- 条件渲染：当内容目标为“属性值”时显示 -->
      <NFormItem v-if="formValue.matchContentMode === 'Attribute'" label="属性名" path="attributeName">
        <NInput v-model:value="formValue.attributeName" @update:value="updateModel" placeholder="例如：src, href, data-id"/>
      </NFormItem>

      <!-- 条件渲染：替换模式专属 -->
      <div v-if="formValue.operationMode === 'Replace'">
        <NFormItem label="替换模板" path="replacementTemplate">
          <NInput v-model:value="formValue.replacementTemplate" @update:value="updateModel" type="textarea" :autosize="{ minRows: 2 }" placeholder="使用 ${match} 代表匹配到的原始内容"/>
        </NFormItem>
      </div>

      <!-- 条件渲染：提取模式专属 -->
      <div v-if="formValue.operationMode === 'Extract'">
        <NFormItem label="输出格式" path="returnFormat">
          <NSelect v-model:value="formValue.returnFormat" @update:value="updateModel" :options="returnFormatOptions"/>
        </NFormItem>
      </div>

      <NFormItem label="输出变量名" path="outputVariableName">
        <NInput v-model:value="formValue.outputVariableName" @update:value="updateModel"/>
      </NFormItem>

    </NForm>

    <NDivider/>

    <!-- 2. 测试区域 -->
    <div class="test-section">
      <NFormItem label="测试输入文本">
        <NInput type="textarea" v-model:value="sampleInput" placeholder="在此处粘贴带标签的示例文本，例如HTML代码..."
                :autosize="{ minRows: 5, maxRows: 15 }"/>
      </NFormItem>
      <NButton @click="runTest" :loading="isLoading" type="primary">执行测试</NButton>

      <!-- 3. 测试结果显示 -->
      <NCollapseTransition :show="!!testResult || !!testError">
        <div class="result-section">
          <NAlert v-if="testError" title="测试失败" type="error" :style="{ marginTop: '16px' }">
            <pre>{{ testError }}</pre>
          </NAlert>
          <div v-if="testResult !== null" :style="{ marginTop: '16px' }">
            <p><strong>最终输出:</strong></p>
            <NCode :code="formattedResult" language="html" word-wrap/>
            <p v-if="testDebugInfo" :style="{ marginTop: '10px' }"><strong>调试信息:</strong></p>
            <NCode v-if="testDebugInfo" :code="formattedDebugInfo" language="json" word-wrap/>
          </div>
        </div>
      </NCollapseTransition>
    </div>
  </div>
</template>

<script setup lang="ts">
import {ref, computed, inject, watch, reactive} from 'vue';
import {
  NForm, NFormItem, NInput, NButton, NDivider, NCode, NAlert, NSelect, NCollapseTransition
} from 'naive-ui';
import type {SelectOption} from 'naive-ui';

// Vue Form Generator 会通过 props 传入 modelValue
const props = defineProps<{
  modelValue: any; // 接收配置对象
}>();

// 同时，也需要 emit update:modelValue 事件来通知父组件更新
const emit = defineEmits(['update:modelValue']);

// 使用 inject 获取主程序提供的 axios 实例
const axios = inject('axios') as any;

// 为了避免直接修改props，我们创建一个响应式的本地副本
// 并设置合理的默认值，防止 props.modelValue 缺少某些属性时出错
const formValue = reactive({
  inputVariableName: '',
  outputVariableName: '',
  operationMode: 'Extract', // 默认为提取模式
  selector: 'div.item',
  matchContentMode: 'TextContent',
  attributeName: '',
  replacementTemplate: '<span>已替换: ${match}</span>',
  returnFormat: 'First',
  ...props.modelValue // 使用传入的 modelValue 覆盖默认值
});


// 监听外部变化，同步到本地
watch(() => props.modelValue, (newValue) => {
  Object.assign(formValue, newValue);
}, {deep: true});

// 每次本地表单更新时，通知父组件
const updateModel = () => {
  emit('update:modelValue', {...formValue});
};

const operationModeOptions: SelectOption[] = [
  {label: '提取内容', value: 'Extract'},
  {label: '替换内容', value: 'Replace'},
];

// 将原有的 extractionModeOptions 重命名为 matchContentModeOptions，因为它现在对两种模式都生效
const matchContentModeOptions: SelectOption[] = [
  {label: '纯文本 (TextContent)', value: 'TextContent'},
  {label: '内部HTML (InnerHtml)', value: 'InnerHtml'},
  {label: '完整HTML (OuterHtml)', value: 'OuterHtml'},
  {label: '属性值 (Attribute)', value: 'Attribute'},
];

const returnFormatOptions: SelectOption[] = [
  {label: '仅第一个', value: 'First'},
  {label: '作为列表', value: 'AsList'},
  {label: '作为JSON字符串', value: 'AsJsonString'},
];

// 提供一个更适合测试替换功能的示例文本
const sampleInput = ref(
    `<div class="product">
  <h3>产品A</h3>
  <p class="price">价格: ￥99</p>
  <a href="/product/a" class="link">查看详情</a>
</div>
<div class="product">
  <h3>产品B</h3>
  <p class="price">价格: ￥199</p>
  <a href="/product/b" class="link">查看详情</a>
</div>`);
const testResult = ref<any>(null);
const testError = ref('');
const testDebugInfo = ref<any>(null);
const isLoading = ref(false);

const formattedResult = computed(() => {
  if (testResult.value === null) return '';
  // 如果结果是对象或数组（例如提取模式下的列表），则格式化为JSON
  if (typeof testResult.value === 'object') {
    return JSON.stringify(testResult.value, null, 2);
  }
  // 否则直接作为字符串返回（例如替换模式下的HTML）
  return String(testResult.value);
});
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

  // 请求体现在会包含 operationMode 等所有字段
  const requestPayload = {
    runeConfig: formValue,
    // 我们将测试文本单独发送，而不是放在模拟的祝祷变量中
    // 这与您原始代码的设计一致
    sampleTuum: {
      [formValue.inputVariableName]: sampleInput.value
    }
  };

  // 注意：这里的API端点我猜测是 /run-test-with-tuum，如果不是请修改
  // 这个端点应该能接收一个完整的模拟祝祷，而不是单个文本
  // 这样更符合符文的实际运行环境
  try {
    // API端点我将使用一个更通用的名称，它能模拟整个祝祷的上下文
    const response = await axios.post('/api/v1/workflows/run-rune-test', requestPayload);
    const data = response.data;

    if (data.isSuccess) {
      // 从返回的祝祷中获取输出变量的值
      testResult.value = data.tuum[formValue.outputVariableName];
    } else {
      testError.value = data.errorMessage || '未知错误';
    }
    // 调试信息直接从返回结果获取
    testDebugInfo.value = data.debugInfo;

  } catch (error: any) {
    testError.value = error.response?.data?.detail || error.response?.data?.title || error.message || '请求失败';
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
  overflow-x: auto;
}
</style>