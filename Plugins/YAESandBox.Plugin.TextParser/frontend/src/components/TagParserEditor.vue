<template>
  <div class="editor-container">
    <NForm :model="formValue" :style="{ maxWidth: '800px' }" label-placement="left" label-width="auto">
      <!-- 组合通用配置组件 -->
      <TextOperationConfigEditor v-model="formValue.textOperation"/>

      <NDivider>标签解析专属配置</NDivider>

      <!-- 仅保留标签解析专属的表单项 -->
      <NFormItem label="CSS 选择器" path="selector">
        <NInput v-model:value="formValue.selector" :autosize="{ minRows: 2 }" type="textarea"/>
      </NFormItem>
      <NFormItem label="内容目标" path="matchContentMode">
        <NSelect v-model:value="formValue.matchContentMode" :options="matchContentModeOptions"/>
      </NFormItem>
      <NFormItem v-if="formValue.matchContentMode === 'Attribute'" label="属性名" path="attributeName">
        <NInput v-model:value="formValue.attributeName" placeholder="例如：src, href, data-id"/>
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
import {ref} from 'vue';
import type {SelectOption} from 'naive-ui';
import {NAlert, NButton, NCode, NCollapseTransition, NDivider, NForm, NFormItem, NInput, NSelect} from 'naive-ui';
import {useVModel} from "@vueuse/core";
import TextOperationConfigEditor from './TextOperationConfigEditor.vue';
import {useRuneTester} from '../composables/useRuneTester';

const props = defineProps<{
  modelValue: any; // 接收配置对象
}>();
const emit = defineEmits(['update:modelValue']);

// 定义默认值，以便在 props.modelValue 未提供时使用
const createDefaultValue = () => ({
  textOperation: {
    inputDataType: 'String',
    inputVariableName: 'inputText',
    outputVariableName: 'outputText',
    operationMode: 'Extract',
    replacementTemplate: '<span>已替换: ${match}</span>',
    returnFormat: 'First',
  },
  selector: 'div.product a',
  matchContentMode: 'Attribute',
  attributeName: 'href',
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

// 将原有的 extractionModeOptions 重命名为 matchContentModeOptions，因为它现在对两种模式都生效
const matchContentModeOptions: SelectOption[] = [
  {label: '纯文本 (TextContent)', value: 'TextContent'},
  {label: '内部HTML (InnerHtml)', value: 'InnerHtml'},
  {label: '完整HTML (OuterHtml)', value: 'OuterHtml'},
  {label: '属性值 (Attribute)', value: 'Attribute'},
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
  overflow-x: auto;
}
</style>