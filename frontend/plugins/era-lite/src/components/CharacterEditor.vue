<template>
  <n-modal
      :show="show"
      preset="card"
      :style="{ width: '600px' }"
      :title="isEditing ? '编辑角色' : '新建角色'"
      @update:show="$emit('update:show', $event)"
  >
    <n-form ref="formRef" :model="formValue" :rules="rules">
      <n-form-item label="角色名称" path="name">
        <n-input v-model:value="formValue.name" placeholder="输入名称" />
      </n-form-item>
      <n-form-item label="角色描述" path="description">
        <n-input v-model:value="formValue.description" type="textarea" placeholder="输入描述" />
      </n-form-item>
      <n-form-item label="头像 (Emoji)" path="avatar">
        <n-input v-model:value="formValue.avatar" placeholder="输入一个 Emoji" />
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
import { useCharacterStore } from '#/stores/characterStore';
import type { Character } from '#/types/models';

const props = defineProps<{ show: boolean; characterId: string | null; }>();
const emit = defineEmits<{ (e: 'update:show', value: boolean): void; }>();

const characterStore = useCharacterStore();
const message = useMessage();
const formRef = ref<FormInst | null>(null);
const isEditing = computed(() => props.characterId !== null);

const getDefaultFormValue = (): Omit<Character, 'id'> => ({ name: '', description: '', avatar: '🙂' });
const formValue = ref(getDefaultFormValue());

const rules: FormRules = {
  name: { required: true, message: '请输入角色名称', trigger: 'blur' },
  avatar: { required: true, message: '请输入头像', trigger: 'blur' },
};

watch(() => props.show, (newVal) => {
  if (newVal) {
    if (props.characterId) {
      const charToEdit = characterStore.characters.find(c => c.id === props.characterId);
      if (charToEdit) formValue.value = { ...charToEdit };
    } else {
      formValue.value = getDefaultFormValue();
    }
  }
});

function handleSave() {
  formRef.value?.validate((errors) => {
    if (!errors) {
      if (isEditing.value && props.characterId) {
        characterStore.updateCharacter({ ...formValue.value, id: props.characterId });
        message.success('角色已更新');
      } else {
        characterStore.addCharacter(formValue.value);
        message.success('角色已创建');
      }
      emit('update:show', false);
    }
  });
}
</script>