<!-- era-lite/src/components/ItemEditor.vue -->
<template>
  <n-modal
      :show="show"
      preset="card"
      :style="{ width: '600px' }"
      :title="isEditing ? '编辑物品' : '新建物品'"
      @update:show="$emit('update:show', $event)"
  >
    <n-form ref="formRef" :model="formValue" :rules="rules">
      <n-form-item label="物品名称" path="name">
        <n-input v-model:value="formValue.name" placeholder="输入名称" />
      </n-form-item>
      <n-form-item label="物品描述" path="description">
        <n-input
            v-model:value="formValue.description"
            type="textarea"
            placeholder="输入描述"
        />
      </n-form-item>
      <n-form-item label="价格" path="price">
        <n-input-number v-model:value="formValue.price" :min="0" style="width: 100%" />
      </n-form-item>
    </n-form>
    <template #footer>
      <n-flex justify="end">
        <n-button @click="$emit('update:show', false)">取消</n-button>
        <n-button type="primary" @click="handleSave">保存</n-button>
      </n-flex>
    </template>
  </n-modal>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue';
import type { FormInst, FormRules } from 'naive-ui';
import { NModal, NForm, NFormItem, NInput, NInputNumber, NFlex, NButton, useMessage } from 'naive-ui';
import { useShopStore } from '#/stores/shopStore';
import type { Item } from '#/types/models';

const props = defineProps<{
  show: boolean;
  itemId: string | null; // null 表示创建模式, string 表示编辑模式
}>();

const emit = defineEmits<{
  (e: 'update:show', value: boolean): void;
}>();

const shopStore = useShopStore();
const message = useMessage();
const formRef = ref<FormInst | null>(null);

const isEditing = computed(() => props.itemId !== null);

const getDefaultFormValue = (): Omit<Item, 'id'> => ({
  name: '',
  description: '',
  price: 100,
});

const formValue = ref(getDefaultFormValue());

const rules: FormRules = {
  name: { required: true, message: '请输入物品名称', trigger: 'blur' },
  price: { type: 'number', required: true, message: '请输入价格', trigger: 'blur' },
};

watch(() => props.show, (newVal) => {
  if (newVal) {
    if (props.itemId) {
      // 编辑模式: 加载现有数据
      const itemToEdit = shopStore.itemsForSale.find(i => i.id === props.itemId);
      if (itemToEdit) {
        formValue.value = { ...itemToEdit };
      }
    } else {
      // 创建模式: 重置为默认值
      formValue.value = getDefaultFormValue();
    }
  }
});

function handleSave() {
  formRef.value?.validate((errors) => {
    if (!errors) {
      if (isEditing.value && props.itemId) {
        shopStore.updateItem({ ...formValue.value, id: props.itemId });
        message.success('物品已更新');
      } else {
        shopStore.addItem(formValue.value);
        message.success('物品已创建');
      }
      emit('update:show', false);
    } else {
      message.error('请检查输入项');
    }
  });
}
</script>