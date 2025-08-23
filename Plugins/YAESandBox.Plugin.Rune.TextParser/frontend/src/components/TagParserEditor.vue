<template>
  <div class="editor-container">
    <!-- 1. 表单字段，通过 v-model 绑定到 formValue -->
    <n-form
        :model="formValue"
        :style="{ maxWidth: '800px' }"
        label-placement="left"
        label-width="auto"
    >
      <!-- 通用配置 -->
      <NFormItem label="输入变量名" path="inputVariableName">
        <NInput v-model:value="formValue.inputVariableName"/>
      </NFormItem>

      <NFormItem label="CSS 选择器" path="selector">
        <NInput v-model:value="formValue.selector" :autosize="{ minRows: 2 }" type="textarea"/>
      </NFormItem>

      <NFormItem label="内容目标" path="matchContentMode">
        <NSelect v-model:value="formValue.matchContentMode" :options="matchContentModeOptions"/>
      </NFormItem>

      <!-- 条件渲染：当内容目标为“属性值”时显示 -->
      <NFormItem v-if="formValue.matchContentMode === 'Attribute'" label="属性名" path="attributeName">
        <NInput v-model:value="formValue.attributeName" placeholder="例如：src, href, data-id"/>
      </NFormItem>

      <NFormItem label="操作模式" path="operationMode">
        <NSelect v-model:value="formValue.operationMode" :options="operationModeOptions"/>
      </NFormItem>

      <!-- 条件渲染：替换模式专属 -->
      <div v-if="formValue.operationMode === 'Replace'">
        <NFormItem label="替换模板" path="replacementTemplate">
          <NInput v-model:value="formValue.replacementTemplate" :autosize="{ minRows: 2 }" placeholder="使用 ${match} 代表匹配到的原始内容"
                  type="textarea"/>
        </NFormItem>
      </div>

      <!-- 条件渲染：提取模式专属 -->
      <div v-if="formValue.operationMode === 'Extract'">
        <NFormItem label="输出格式" path="returnFormat">
          <NSelect v-model:value="formValue.returnFormat" :options="returnFormatOptions"/>
        </NFormItem>
      </div>

      <NFormItem label="输出变量名" path="outputVariableName">
        <NInput v-model:value="formValue.outputVariableName"/>
      </NFormItem>

    </n-form>

    <NDivider/>

    <!-- 2. 测试区域 -->
    <div class="test-section">
      <NFormItem label="测试输入文本">
        <NInput v-model:value="sampleInput" :autosize="{ minRows: 5, maxRows: 15 }" placeholder="在此处粘贴带标签的示例文本，例如HTML代码..."
                type="textarea"/>
      </NFormItem>
      <NButton :loading="isLoading" type="primary" @click="runTest">执行测试</NButton>

      <!-- 3. 测试结果显示 -->
      <NCollapseTransition :show="!!testResult || !!testError">
        <div class="result-section">
          <NAlert v-if="testError" :style="{ marginTop: '16px' }" title="测试失败" type="error">
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

<script lang="ts" setup>
import {computed, inject, ref} from 'vue';
import type {SelectOption} from 'naive-ui';
import {NAlert, NButton, NCode, NCollapseTransition, NDivider, NForm, NFormItem, NInput, NSelect} from 'naive-ui';
import {useVModel} from "@vueuse/core";

// Vue Form Generator 会通过 props 传入 modelValue
const props = defineProps<{
  modelValue: any; // 接收配置对象
}>();

// 同时，也需要 emit update:modelValue 事件来通知父组件更新
const emit = defineEmits(['update:modelValue']);

// 使用 inject 获取主程序提供的 axios 实例
const axios = inject('axios') as any;

// 定义默认值，以便在 props.modelValue 未提供时使用
const createDefaultValue = () => ({
  inputVariableName: '',
  outputVariableName: '',
  operationMode: 'Extract',
  selector: 'div.item',
  matchContentMode: 'TextContent',
  attributeName: '',
  replacementTemplate: '<span>已替换: ${match}</span>',
  returnFormat: 'First',
});

// 使用 useVModel 创建一个可写的、与父组件同步的 ref
// 当你修改 formValue.value.xxx 时，它会自动 emit('update:modelValue', ...)
// 当 props.modelValue 变化时，formValue.value 会自动更新
// passive: true 和 defaultValue 确保了即使 props.modelValue 是 undefined，组件也能正常工作
const formValue = useVModel(props, 'modelValue', emit, {
  passive: true, // 仅在 modelValue 存在时才进行双向绑定
  defaultValue: createDefaultValue(), // 如果 modelValue 是 undefined，则使用这个默认值
  deep: true, // 对对象进行深度监听和响应
});

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
    `<div name="product">
  <h3>产品A</h3>
  <p class="price">价格: ￥99</p>
  <a href="/product/a" class="link">查看详情</a>
</div>
<div class="product">
  <h3>产品B</h3>
  <p class="price">价格: ￥199</p>
  <a href="/product/b" class="link">查看详情</a>
</div>`);
const testResult = ref<string | string[] | null>(null);
const testError = ref('');
const testDebugInfo = ref<any>(null);
const isLoading = ref(false);

const formattedResult = computed(() =>
{
  if (testResult.value === null) return '';
  // 如果结果是对象或数组（例如提取模式下的列表），则格式化为JSON
  if (typeof testResult.value === 'object')
  {
    return JSON.stringify(testResult.value, null, 2);
  }
  // 否则直接作为字符串返回（例如替换模式下的HTML）
  return String(testResult.value);
});
const formattedDebugInfo = computed(() => testDebugInfo.value ? JSON.stringify(testDebugInfo.value, null, 2) : '');
type TestResponseDto = {
  isSuccess: boolean;
  result: string | string[];
  errorMessage: string;
  debugInfo: any;
}

async function runTest()
{
  if (!axios)
  {
    testError.value = "错误：未能获取到 axios 实例。";
    return;
  }
  isLoading.value = true;
  testResult.value = null;
  testError.value = '';
  testDebugInfo.value = null;

  const requestPayload = {
    runeConfig: formValue.value,
    SampleInputText: sampleInput.value
  };

  try
  {
    // API端点我将使用一个更通用的名称，它能模拟整个枢机的上下文
    const response: { data: TestResponseDto } = await axios.post('/api/v1/plugins/text-parser/run-test', requestPayload);
    const data = response.data;

    if (data.isSuccess)
    {
      // 从返回的枢机中获取输出变量的值
      testResult.value = data.result;
    }
    else
    {
      testError.value = data.errorMessage || '未知错误';
    }
    // 调试信息直接从返回结果获取
    testDebugInfo.value = data.debugInfo;

  } catch (error: any)
  {
    testError.value = error.response?.data?.detail || error.response?.data?.title || error.message || '请求失败';
  } finally
  {
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