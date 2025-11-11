<!-- EntityFormPanel -->
<template>
  <n-form ref="formRef" :model="formValue" :rules="formRules">
    <!-- 根据 schema 动态渲染表单项 -->
    <n-form-item v-for="field in schema" :key="getKey(field)" :label="field.label" :path="getKey(field)">
      <!-- 使用动态组件 :is 来渲染输入控件 -->
      <component
          :is="field.component"
          v-model:value="formValue[getKey(field)]"
          style="width: 100%"
          v-bind="field.componentProps"
      />
    </n-form-item>
  </n-form>
</template>

<script generic="T extends Record<string, any>" lang="ts" setup>
import {computed, ref, onMounted} from 'vue';
import type {FormInst, FormRules} from 'naive-ui';
import {NForm, NFormItem, useMessage} from 'naive-ui';
import {useRefHistory} from '@vueuse/core';
import {type EntityFieldSchema, getKey} from "@yaesandbox-frontend/core-services/composables";

// 组件的 props 定义，使用泛型 T 约束实体类型，TMode 约束工作模式
const props = defineProps<{
  initialData: Partial<T> | null;
  schema: EntityFieldSchema[];
}>();

// 组件的 emits 定义
const emit = defineEmits<{
  (e: 'save', data: T): void;
}>();

const message = useMessage();
const formRef = ref<FormInst | null>(null);

// 从 schema 动态生成 Naive UI 的验证规则对象
const formRules = computed((): FormRules =>
{
  return Object.fromEntries(
      props.schema
          .filter(field => field.rules)
          .map(field => [getKey(field), field.rules!])
  );
});

// 存储表单当前状态的 ref
// const formValue = ref<Partial<T>>({});
const formValue = ref<Record<string, any>>({});

// 使用 useRefHistory 包装表单状态，以支持撤销 (取消) 操作
const {undo, commit} = useRefHistory(formValue, {deep: true});

// 使用 onMounted 初始化数据
onMounted(() =>
{
  const initialScaffold = Object.fromEntries(props.schema.map(field => [getKey(field), undefined]));
  formValue.value = {...initialScaffold, ...(props.initialData || {})};
});

// 暴露一个 submit 方法给外部调用者
async function submit(): Promise<T | null>
{
  try
  {
    await formRef.value?.validate();
    // 验证通过，返回表单数据
    return formValue.value as T;
  }
  catch (errors)
  {
    return null; // 验证失败
  }
}

// 使用 defineExpose 暴露 submit 方法
defineExpose({
  submit,
});
</script>