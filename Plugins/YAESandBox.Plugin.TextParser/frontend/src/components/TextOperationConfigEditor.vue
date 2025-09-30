<template>
  <div>
    <NFormItem label="输入数据类型" path="textOperation.inputDataType">
      <NSelect v-model:value="formValue.inputDataType" :options="inputDataTypeOptions"/>
    </NFormItem>
    <NFormItem label="输入变量名" path="textOperation.inputVariableName">
      <NInput v-model:value="formValue.inputVariableName"/>
    </NFormItem>
    <NFormItem label="操作模式" path="textOperation.operationMode">
      <NSelect v-model:value="formValue.operationMode" :options="operationModeOptions"/>
    </NFormItem>

    <!-- 替换模式专属 -->
    <div v-if="formValue.operationMode === 'Replace'">
      <NFormItem label="替换模板" path="textOperation.replacementTemplate">
        <NInput v-model:value="formValue.replacementTemplate" :autosize="{ minRows: 2 }"
                placeholder="使用 ${match} 或 ${groupName} 代表匹配内容" type="textarea"/>
      </NFormItem>
    </div>

    <!-- 提取模式专属 -->
    <div v-if="formValue.operationMode === 'Extract'">
      <NFormItem label="输出格式" path="textOperation.returnFormat">
        <NSelect v-model:value="formValue.returnFormat" :options="returnFormatOptions"/>
      </NFormItem>
    </div>

    <NFormItem label="输出变量名" path="textOperation.outputVariableName">
      <NInput v-model:value="formValue.outputVariableName"/>
    </NFormItem>
  </div>
</template>

<script lang="ts" setup>
import { NFormItem, NInput, NSelect } from 'naive-ui';
import type { SelectOption } from 'naive-ui';
import { useVModel } from "@vueuse/core";

const props = defineProps<{ modelValue: any; }>();
const emit = defineEmits(['update:modelValue']);

// 直接绑定 props.modelValue，因为这个组件是无状态的，
// 它的所有状态都由父组件通过 v-model 传入。
const formValue = useVModel(props, 'modelValue', emit, { deep: true });

const inputDataTypeOptions: SelectOption[] = [
  { label: '文本 (String)', value: 'String' },
  { label: '提示词列表 (PromptList)', value: 'PromptList' },
];

const operationModeOptions: SelectOption[] = [
  { label: '提取 (Extract)', value: 'Extract' },
  { label: '替换 (Replace)', value: 'Replace' },
];

const returnFormatOptions: SelectOption[] = [
  { label: '仅第一个 (String)', value: 'First' },
  { label: '作为列表 (String[])', value: 'AsList' },
  { label: '作为JSON字符串 (JsonString)', value: 'AsJsonString' },
];
</script>