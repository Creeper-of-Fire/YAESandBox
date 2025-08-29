<!-- era-lite/src/views/CharacterListView.vue -->
<template>
  <n-flex vertical :size="24">
    <n-page-header>
      <template #title><n-h1 style="margin:0">角色列表</n-h1></template>
      <template #extra>
        <n-button type="primary" @click="openCreateModal">新建角色</n-button>
      </template>
    </n-page-header>

    <GeneratorPanel
        v-model="generatedCharacter"
        :schema="characterSchema"
        storage-key="character-generator"
        :expected-inputs="['topic']"
        title="AI 角色生成器"
        generation-prompt-label="输入你想要生成的角色描述，例如：一位从未来穿越而来的机器人武士"
        @accept="addGeneratedCharacterToStore"
    />

    <n-list bordered hoverable clickable>
      <n-list-item v-for="char in characterStore.characters" :key="char.id">
        <template #prefix>
          <n-avatar :size="48" style="font-size: 32px;">{{ char.avatar }}</n-avatar>
        </template>
        <n-thing :title="char.name" :description="char.description" />
        <template #suffix>
          <n-flex>
            <n-button @click="sessionStore.setProtagonist(char.id)" :type="sessionStore.protagonistId === char.id ? 'primary' : 'default'" ghost>设为主角</n-button>
            <n-button @click="sessionStore.setInteractTarget(char.id)" :type="sessionStore.interactTargetId === char.id ? 'success' : 'default'" ghost>设为交互对象</n-button>
            <n-button @click="openEditModal(char.id)" circle><template #icon><n-icon :component="EditIcon" /></template></n-button>
            <n-button @click="handleDelete(char)" circle type="error" ghost><template #icon><n-icon :component="DeleteIcon" /></template></n-button>
          </n-flex>
        </template>
      </n-list-item>
    </n-list>
    <CharacterEditor v-model:show="showEditorModal" :character-id="editingCharacterId" />
  </n-flex>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { NFlex, NH1, NList, NListItem, NAvatar, NThing, NButton, NPageHeader, useDialog, useMessage, NIcon } from 'naive-ui';
import { useCharacterStore } from '../stores/characterStore';
import { useSessionStore } from '../stores/sessionStore';
import CharacterEditor from '#/components/CharacterEditor.vue';
import { Pencil as EditIcon, TrashBinOutline as DeleteIcon } from '@vicons/ionicons5';
import type { Character } from '#/types/models';
import GeneratorPanel from "#/components/GeneratorPanel.vue";
import type {SchemaField} from "#/types/generator.ts";

const characterStore = useCharacterStore();
const sessionStore = useSessionStore();
const dialog = useDialog();
const message = useMessage();

const showEditorModal = ref(false);
const editingCharacterId = ref<string | null>(null);

function openCreateModal() {
  editingCharacterId.value = null;
  showEditorModal.value = true;
}

function openEditModal(id: string) {
  editingCharacterId.value = id;
  showEditorModal.value = true;
}

function handleDelete(character: Character) {
  dialog.warning({
    title: '确认删除',
    content: `你确定要删除角色 “${character.name}” 吗？`,
    positiveText: '确定',
    negativeText: '取消',
    onPositiveClick: () => {
      characterStore.deleteCharacter(character.id);
      message.success('角色已删除');
    },
  });
}

// --- AI 生成器逻辑 ---
const characterSchema: SchemaField[] = [
  { key: 'name', label: '角色名称', type: 'text' },
  { key: 'description', label: '角色描述', type: 'textarea' },
  { key: 'avatar', label: '头像 (Emoji)', type: 'text' },
];
const generatedCharacter = ref<Partial<Character> | null>(null);

function addGeneratedCharacterToStore(newChar: Partial<Character>) {
  if (!newChar.name || !newChar.avatar) {
    message.error('AI 生成的数据不完整，已丢弃。');
    return;
  }
  characterStore.addCharacter({
    name: newChar.name,
    description: newChar.description || '无描述',
    avatar: newChar.avatar,
  });
  message.success(`角色 “${newChar.name}” 已成功创建！`);
  generatedCharacter.value = null;
}
</script>