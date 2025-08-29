<template>
  <n-modal
      :show="show"
      preset="card"
      :style="{ width: '600px' }"
      :title="isEditing ? '编辑场景' : '新建场景'"
      @update:show="$emit('update:show', $event)"
  >
    <n-form ref="formRef" :model="formValue" :rules="rules">
      <n-form-item label="场景名称" path="name">
        <n-input v-model:value="formValue.name" placeholder="输入名称" />
      </n-form-item>
      <n-form-item label="场景描述" path="description">
        <n-input v-model:value="formValue.description" type="textarea" placeholder="输入描述" />
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
import { useMessage } from 'naive-ui';
import { useSceneStore } from '#/stores/sceneStore';
import type { Scene } from '#/types/models';

const props = defineProps<{ show: boolean; sceneId: string | null; }>();
const emit = defineEmits<{ (e: 'update:show', value: boolean): void; }>();

const sceneStore = useSceneStore();
const message = useMessage();
const formRef = ref<FormInst | null>(null);
const isEditing = computed(() => props.sceneId !== null);

const getDefaultFormValue = (): Omit<Scene, 'id'> => ({ name: '', description: '' });
const formValue = ref(getDefaultFormValue());

const rules: FormRules = {
  name: { required: true, message: '请输入场景名称', trigger: 'blur' },
};

watch(() => props.show, (newVal) => {
  if (newVal) {
    if (props.sceneId) {
      const sceneToEdit = sceneStore.scenes.find(s => s.id === props.sceneId);
      if (sceneToEdit) formValue.value = { ...sceneToEdit };
    } else {
      formValue.value = getDefaultFormValue();
    }
  }
});

function handleSave() {
  formRef.value?.validate((errors) => {
    if (!errors) {
      if (isEditing.value && props.sceneId) {
        sceneStore.updateScene({ ...formValue.value, id: props.sceneId });
        message.success('场景已更新');
      } else {
        sceneStore.addScene(formValue.value);
        message.success('场景已创建');
      }
      emit('update:show', false);
    }
  });
}
</script>