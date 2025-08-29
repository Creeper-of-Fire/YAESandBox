<!-- era-lite/src/components/ItemEditor.vue -->
<template>
  <n-modal
      :show="show"
      preset="card"
      :style="{ width: '600px' }"
      :title="isEditing ? '编辑物品' : '新建物品'"
      @update:show="handleClose"
  >
    <n-form ref="formRef" :model="formValue" :rules="rules">
      <n-form-item label="物品名称" path="name">
        <n-input v-model:value="formValue.name" placeholder="输入名称"/>
      </n-form-item>
      <n-form-item label="物品描述" path="description">
        <n-input
            v-model:value="formValue.description"
            type="textarea"
            placeholder="输入描述"
        />
      </n-form-item>
      <n-form-item label="价格" path="price">
        <n-input-number v-model:value="formValue.price" :min="0" style="width: 100%"/>
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

<script setup lang="ts" generic="TMode extends 'create' | 'edit'">
import {computed, ref, watch} from 'vue';
import type {FormInst, FormRules} from 'naive-ui';
import {useMessage} from 'naive-ui';
import type {Item} from '#/types/models';
import {useRefHistory} from "@vueuse/core";

const props = defineProps<{
  show: boolean;
  mode: TMode;
  initialData: TMode extends 'edit' ? Item : (Omit<Item, 'id'> | null);
}>();

const emit = defineEmits<{
  (e: 'update:show', value: boolean): void;
  (e: 'save', data: TMode extends 'edit' ? Item : Omit<Item, 'id'>): void;
}>();

const message = useMessage();
const formRef = ref<FormInst | null>(null);

const isEditing = computed(() => props.mode === 'edit');

const getDefaultFormValue = (): Omit<Item, 'id'> => ({
  name: '',
  description: '',
  price: 100,
});

const rules: FormRules = {
  name: {required: true, message: '请输入物品名称', trigger: 'blur'},
  price: {type: 'number', required: true, message: '请输入价格', trigger: 'blur'},
};

// 创建一个 ref 来存储表单的当前状态
const formValue = ref<Omit<Item, 'id'> | Item>(getDefaultFormValue());

// 用 useRefHistory 包裹这个 ref，并启用深拷贝
const {history, undo, commit} = useRefHistory(formValue, {
  deep: true, // 必须开启，因为我们操作的是对象
});

// 当模态框显示时，重置状态和历史记录
watch(() => props.show, (newVal) =>
{
  if (newVal)
  {
    // 设置表单的初始值
    formValue.value = JSON.parse(JSON.stringify(props.initialData || getDefaultFormValue()));
    // 将这个初始值作为新的历史记录起点
    commit();
  }
});

function handleSave()
{
  formRef.value?.validate((errors) =>
  {
    if (!errors)
    {
      // 验证通过，发出 save 事件
      emit('save', formValue.value);
      emit('update:show', false);
    } else
    {
      message.error('请检查输入项');
    }
  });
}

function handleClose()
{
  // 'undo' 会将 formValue.value 恢复到上一次 commit 时的状态
  undo();
  emit('update:show', false);
}
</script>