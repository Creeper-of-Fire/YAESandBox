<template>
  <div class="editor-container">
    <NForm label-placement="left" label-width="auto" :style="{ maxWidth: '800px' }" :model="formValue">
      <NFormItem label="输入变量名" path="inputVariableName">
        <NInput v-model:value="formValue.inputVariableName" @update:value="updateModel"/>
      </NFormItem>
      <NFormItem label="操作模式" path="operationMode">
        <NSelect v-model:value="formValue.operationMode" @update:value="updateModel" :options="operationModeOptions"/>
      </NFormItem>
      <NFormItem label="正则表达式" path="pattern">
        <NInput v-model:value="formValue.pattern" @update:value="updateModel" type="textarea"
                placeholder="例如：姓名：(?<name>\S+)\s+年龄：(?<age>\d+)" :autosize="{ minRows: 2 }"/>
      </NFormItem>

      <!-- 高级正则选项 -->
      <NFormItem label="高级选项">
        <NGrid :cols="3" :x-gap="12">
          <NFormItemGi>
            <NCheckbox v-model:checked="formValue.ignoreCase" @update:checked="updateModel">
              忽略大小写 (i)
            </NCheckbox>
          </NFormItemGi>
          <NFormItemGi>
            <NCheckbox v-model:checked="formValue.multiline" @update:checked="updateModel">
              多行模式 (m)
            </NCheckbox>
          </NFormItemGi>
          <NFormItemGi>
            <NCheckbox v-model:checked="formValue.dotall" @update:checked="updateModel">
              点号匹配所有 (s)
            </NCheckbox>
          </NFormItemGi>
        </NGrid>
      </NFormItem>

      <NFormItem label="输出模板" path="outputTemplate">
        <NInput v-model:value="formValue.outputTemplate" @update:value="updateModel" type="textarea"
                placeholder="使用 ${name} 或 $1 引用捕获组" :autosize="{ minRows: 2 }"/>
      </NFormItem>

      <!-- 最大处理次数 -->
      <NFormItem label="最大处理次数" path="maxMatches">
        <NInputNumber v-model:value="formValue.maxMatches" @update:value="updateModel" :min="0"/>
        <template #feedback>设置为 0 表示不限制次数。</template>
      </NFormItem>

      <NFormItem v-if="formValue.operationMode === 'Generate'" label="连接符" path="joinSeparator">
        <NInput v-model:value="formValue.joinSeparator" @update:value="updateModel"
                placeholder="用于拼接多个匹配结果的分隔符"/>
      </NFormItem>
      <NFormItem label="输出变量名" path="outputVariableName">
        <NInput v-model:value="formValue.outputVariableName" @update:value="updateModel"/>
      </NFormItem>
    </NForm>

    <NDivider/>

    <div class="test-section">
      <NFormItem label="测试输入文本">
        <NInput type="textarea" v-model:value="sampleInput"
                placeholder="在此处粘贴用于测试的源文本..." :autosize="{ minRows: 5, maxRows: 15 }"/>
      </NFormItem>
      <NButton @click="runTest" :loading="isLoading" type="primary">执行测试</NButton>

      <NCollapseTransition :show="!!testResult || !!testError">
        <div class="result-section">
          <NAlert v-if="testError" title="测试失败" type="error" :style="{ marginTop: '16px' }">
            <pre>{{ testError }}</pre>
          </NAlert>
          <div v-if="testResult !== null" :style="{ marginTop: '16px' }">
            <p><strong>最终输出:</strong></p>
            <NCode :code="testResult" language="text" word-wrap/>
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
  NForm, NFormItem, NInput, NButton, NDivider, NCode, NAlert, NSelect, NCollapseTransition,
  NCheckbox, NInputNumber, NGrid, NFormItemGi
} from 'naive-ui';
import type {SelectOption} from 'naive-ui';

const props = defineProps<{ modelValue: any; }>();
const emit = defineEmits(['update:modelValue']);
const axios = inject('axios') as any;

const formValue = reactive({
  inputVariableName: 'inputText',
  outputVariableName: 'outputText',
  operationMode: 'Generate',
  pattern: '姓名：(?<name>\\S+)', // 简化一下，便于测试
  outputTemplate: '${name}',
  joinSeparator: ', ',
  ignoreCase: false,
  multiline: false,
  dotall: false,
  maxMatches: 0,
  ...props.modelValue
});

watch(() => props.modelValue, (newValue) =>
{
  Object.assign(formValue, newValue);
}, {deep: true});

const updateModel = () =>
{
  emit('update:modelValue', {...formValue});
};

const operationModeOptions: SelectOption[] = [
  {label: '生成 (根据匹配项构建新文本)', value: 'Generate'},
  {label: '替换 (在原文中替换匹配项)', value: 'Replace'},
];

const sampleInput = ref(
    `第一个人，姓名：爱丽丝。
第二个人，姓名：Bob。
第三个人，姓名：查理。`
);
const testResult = ref<string | null>(null);
const testError = ref('');
const testDebugInfo = ref<any>(null);
const isLoading = ref(false);

const formattedDebugInfo = computed(() => testDebugInfo.value ? JSON.stringify(testDebugInfo.value, null, 2) : '');

async function runTest()
{
  isLoading.value = true;
  testResult.value = null;
  testError.value = '';
  testDebugInfo.value = null;

  const requestPayload = {
    runeConfig: formValue,
    sampleTuum: {[formValue.inputVariableName]: sampleInput.value}
  };

  try
  {
    const response = await axios.post('/api/v1/workflows/run-rune-test', requestPayload);
    const data = response.data;
    if (data.isSuccess)
    {
      testResult.value = data.tuum[formValue.outputVariableName];
    } else
    {
      testError.value = data.errorMessage || '未知错误';
    }
    testDebugInfo.value = data.debugInfo;
  } catch (error: any)
  {
    testError.value = error.response?.data?.detail || error.message || '请求失败';
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
}

pre {
  white-space: pre-wrap;
  word-wrap: break-word;
}
</style>