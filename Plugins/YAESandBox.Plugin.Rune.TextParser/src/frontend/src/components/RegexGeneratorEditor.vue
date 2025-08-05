<template>
  <div class="editor-container">
    <!-- 1. 表单字段 -->
    <NForm
        label-placement="left"
        label-width="auto"
        :style="{ maxWidth: '800px' }"
        :model="modelValue"
    >
      <NFormItem label="输入变量名">
        <NInput v-model:value="formValue.inputVariableName" @update:value="updateModel"/>
      </NFormItem>
      <NFormItem label="正则表达式">
        <NInput
            v-model:value="formValue.pattern"
            @update:value="updateModel"
            type="textarea"
            placeholder="例如：姓名：(?<name>\S+)\s+年龄：(?<age>\d+)"
            :autosize="{ minRows: 2 }"
        />
      </NFormItem>
      <NFormItem label="输出模板">
        <NInput
            v-model:value="formValue.outputTemplate"
            @update:value="updateModel"
            type="textarea"
            placeholder="例如：- 角色名: ${name}, 年龄: ${2}岁。"
            :autosize="{ minRows: 2 }"
        />
      </NFormItem>
      <NFormItem label="连接符">
        <NInput
            v-model:value="formValue.joinSeparator"
            @update:value="updateModel"
            placeholder="用于拼接多个匹配结果的分隔符，默认为换行"
        />
      </NFormItem>
      <NFormItem label="输出变量名">
        <NInput v-model:value="formValue.outputVariableName" @update:value="updateModel"/>
      </NFormItem>
    </NForm>

    <NDivider/>

    <!-- 2. 测试区域 -->
    <div class="test-section">
      <NFormItem label="测试输入文本">
        <NInput
            type="textarea"
            v-model:value="sampleInput"
            placeholder="在此处粘贴用于测试的源文本..."
            :autosize="{ minRows: 5, maxRows: 15 }"
        />
      </NFormItem>
      <NButton @click="runTest" :loading="isLoading" type="primary">执行测试</NButton>

      <!-- 3. 测试结果显示 -->
      <NCollapseTransition :show="!!testResult || !!testError">
        <div class="result-section">
          <NAlert v-if="testError" title="测试失败" type="error" :style="{ marginTop: '16px' }">
            <pre>{{ testError }}</pre>
          </NAlert>
          <div v-if="testResult !== null" :style="{ marginTop: '16px' }">
            <p><strong>测试结果:</strong></p>
            <!-- 结果通常是字符串，所以可以直接用 pre 标签或 NCode 显示 -->
            <NCode :code="testResult" language="text" word-wrap/>
            <p :style="{ marginTop: '10px' }"><strong>调试信息:</strong></p>
            <NCode :code="formattedDebugInfo" language="json" word-wrap/>
          </div>
        </div>
      </NCollapseTransition>
    </div>
  </div>
</template>

<script setup lang="ts">
import {ref, computed, inject, watch, reactive} from 'vue';
import {NForm, NFormItem, NInput, NButton, NDivider, NCode, NAlert, NCollapseTransition} from 'naive-ui';

// 定义 Props 和 Emits
const props = defineProps<{
  modelValue: any;
}>();
const emit = defineEmits(['update:modelValue']);

// 获取主程序提供的 axios 实例
const axios = inject('axios') as any;

// 创建本地响应式副本
const formValue: any = reactive({...props.modelValue});

// 监听外部变化同步到本地
watch(() => props.modelValue, (newValue) =>
{
  Object.assign(formValue, newValue);
}, {deep: true});

// 更新时通知父组件
const updateModel = () =>
{
  emit('update:modelValue', {...formValue});
};

// --- 测试相关状态 ---
const sampleInput = ref(
    `日志条目 #1
姓名：爱丽丝 年龄：25 职业：工程师
日志条目 #2
姓名：鲍勃 年龄：32 职业：设计师`
);
const testResult = ref<string | null>(null);
const testError = ref('');
const testDebugInfo = ref<any>(null);
const isLoading = ref(false);

const formattedDebugInfo = computed(() => testDebugInfo.value ? JSON.stringify(testDebugInfo.value, null, 2) : '');

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
    runeConfig: {
      // 确保发送的是最新的表单值
      ...formValue,
      // 添加一个类型标识，帮助后端正确反序列化
      RuneType: "RegexGeneratorRuneConfig"
    },
    sampleInputText: sampleInput.value,
  };

  try
  {
    const response = await axios.post('/api/v1/plugins/text-parser/test-parser/run-test', requestPayload);
    const data = response.data;

    if (data.isSuccess)
    {
      testResult.value = data.result;
    } else
    {
      testError.value = data.errorMessage || '未知错误';
    }
    testDebugInfo.value = data.debugInfo;

  } catch (error: any)
  {
    testError.value = error.response?.data?.title || error.message || '请求失败';
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

/* 方便阅读长错误信息 */
pre {
  white-space: pre-wrap;
  word-wrap: break-word;
}
</style>
