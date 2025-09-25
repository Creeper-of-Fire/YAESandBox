<template>
  <div class="editor-container">
    <NForm :model="formValue" :style="{ maxWidth: '800px' }" label-placement="left" label-width="auto">
      <!-- 组合通用配置组件，通过 v-model 绑定到嵌套对象 -->
      <TextOperationConfigEditor v-model="formValue.textOperation"/>

      <NDivider>正则专属配置</NDivider>

      <!-- 仅保留正则专属的表单项 -->
      <NFormItem label="正则表达式" path="pattern">
        <NInput v-model:value="formValue.pattern" :autosize="{ minRows: 2 }"
                placeholder="例如：姓名：(?<name>\S+)\s+年龄：(?<age>\d+)" type="textarea"/>
      </NFormItem>
      <NFormItem label="高级选项">
        <NGrid :cols="3" :x-gap="12">
          <NFormItemGi>
            <NCheckbox v-model:checked="formValue.ignoreCase">忽略大小写 (i)</NCheckbox>
          </NFormItemGi>
          <NFormItemGi>
            <NCheckbox v-model:checked="formValue.multiline">多行模式 (m)</NCheckbox>
          </NFormItemGi>
          <NFormItemGi>
            <NCheckbox v-model:checked="formValue.dotall">点号匹配所有 (s)</NCheckbox>
          </NFormItemGi>
        </NGrid>
      </NFormItem>
      <NFormItem label="最大处理次数" path="maxMatches">
        <NInputNumber v-model:value="formValue.maxMatches" :min="0"/>
        <template #feedback>设置为 0 表示不限制次数。</template>
      </NFormItem>
    </NForm>

    <NDivider/>

    <!-- 测试区域 -->
    <div class="test-section">
      <NFormItem label="测试输入文本">
        <NInput v-model:value="sampleInput" :autosize="{ minRows: 5, maxRows: 15 }" type="textarea"/>
      </NFormItem>
      <NButton :loading="isLoading" type="primary" @click="runTest('/api/v1/plugins/text-parser/run-test')">执行测试</NButton>
      <NCollapseTransition :show="!!formattedResult || !!testError">
        <div class="result-section">
          <NAlert v-if="testError" :style="{ marginTop: '16px' }" title="测试失败" type="error">
            <pre>{{ testError }}</pre>
          </NAlert>
          <div v-if="formattedResult" :style="{ marginTop: '16px' }">
            <p><strong>最终输出:</strong></p>
            <NCode :code="formattedResult" language="text" word-wrap/>
            <p v-if="testDebugInfo" :style="{ marginTop: '10px' }"><strong>调试信息:</strong></p>
            <NCode v-if="testDebugInfo" :code="formattedDebugInfo" language="json" word-wrap/>
          </div>
        </div>
      </NCollapseTransition>
    </div>
  </div>
</template>

<script lang="ts" setup>
import {ref} from 'vue';
import {
  NAlert,
  NButton,
  NCheckbox,
  NCode,
  NCollapseTransition,
  NDivider,
  NForm,
  NFormItem,
  NFormItemGi,
  NGrid,
  NInput,
  NInputNumber
} from 'naive-ui';
import {useVModel} from "@vueuse/core";
import TextOperationConfigEditor from './TextOperationConfigEditor.vue';
import {useRuneTester} from '../composables/useRuneTester';

const props = defineProps<{ modelValue: any; }>();
const emit = defineEmits(['update:modelValue']);

const createDefaultValue = () => ({
  // 必须匹配后端的嵌套结构
  textOperation: {
    inputDataType: 'String',
    inputVariableName: 'inputText',
    outputVariableName: 'outputText',
    operationMode: 'Extract',
    replacementTemplate: "姓名: ${name}",
    returnFormat: 'AsList',
  },
  pattern: '姓名：(?<name>\\S+)',
  ignoreCase: true,
  multiline: false,
  dotall: true,
  maxMatches: 0,
});

const formValue = useVModel(props, 'modelValue', emit, {
  passive: true, // 仅在 modelValue 存在时才进行双向绑定
  defaultValue: createDefaultValue(), // 如果 modelValue 是 undefined，则使用这个默认值
  deep: true, // 对对象进行深度监听和响应
});

const sampleInput = ref(
    `第一个人，姓名：爱丽丝。\n第二个人，姓名：Bob。\n第三个人，姓名：查理。`
);

const {isLoading, testError, testDebugInfo, formattedResult, formattedDebugInfo, runTest} = useRuneTester(formValue, sampleInput);
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