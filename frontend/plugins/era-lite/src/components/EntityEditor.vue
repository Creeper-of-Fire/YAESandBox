<template>
  <n-modal
      :show="show"
      :style="{width: '90vw', maxWidth: '600px' }"
      :title="title"
      preset="card"
      @update:show="handleClose"
  >
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
    <template #footer>
      <n-flex justify="end">
        <n-button @click="handleClose">取消</n-button>
        <n-button type="primary" @click="handleSave">保存</n-button>
      </n-flex>
    </template>
  </n-modal>
</template>

<script generic="T extends Record<string, any>, TMode extends 'create' | 'edit' | 'complete'" lang="ts" setup>
import {computed, ref, watch} from 'vue';
import type {FormInst, FormRules} from 'naive-ui';
import {NButton, NFlex, NForm, NFormItem, NModal, useMessage} from 'naive-ui';
import {useRefHistory} from '@vueuse/core';
import {type EntityFieldSchema, getKey} from "#/types/entitySchema.ts";

// 组件的 props 定义，使用泛型 T 约束实体类型，TMode 约束工作模式
const props = defineProps<{
  show: boolean;
  mode: TMode;
  initialData: TMode extends 'edit' ? T : Partial<T> | null;
  schema: EntityFieldSchema[];
  entityName: string; // 用于生成标题，例如 "物品", "角色"
}>();

// 组件的 emits 定义，载荷类型根据 TMode 动态推断
const emit = defineEmits<{
  (e: 'update:show', value: boolean): void;
  (e: 'save', data: TMode extends 'edit' ? T : Omit<T, 'id'>): void;
}>();

const message = useMessage();
const formRef = ref<FormInst | null>(null);

// 根据 mode 动态计算模态框标题
const title = computed(() =>
{
  if (props.mode === 'edit') return `编辑${props.entityName}`;
  if (props.mode === 'complete') return `补全${props.entityName}信息`;
  return `新建${props.entityName}`;
});

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

// 监听模态框的显示状态，用于初始化表单数据和历史记录
watch(() => props.show, (newVal) =>
{
  if (newVal)
  {
    // 合并默认值和传入的初始数据，确保表单结构完整
    const initialScaffold = Object.fromEntries(props.schema.map(field => [getKey(field), undefined]));
    const initial = {...initialScaffold, ...(props.initialData || {})};
    formValue.value = JSON.parse(JSON.stringify(initial));

    // 将初始状态提交到历史记录，作为“取消”操作的回滚点
    commit();
  }
});

// 处理保存按钮点击事件
function handleSave()
{
  formRef.value?.validate((errors) =>
  {
    if (!errors)
    {
      emit('save', formValue.value as any);
      emit('update:show', false);
    }
    else
    {
      message.error('请检查输入项');
    }
  });
}

// 处理取消或关闭模态框事件
function handleClose()
{
  // 撤销所有在表单中的更改
  undo();
  emit('update:show', false);
}
</script>