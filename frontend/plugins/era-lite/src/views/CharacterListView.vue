<!-- era-lite/src/views/CharacterListView.vue -->
<template>
  <n-flex :size="24" vertical>
    <n-page-header>
      <template #title>
        <n-h1 style="margin:0">角色列表</n-h1>
      </template>
      <template #extra>
        <n-button type="primary" @click="openCreateModal">新建角色</n-button>
      </template>
    </n-page-header>

    <GeneratorPanel
        v-model="generatedCharacter"
        :expected-inputs="['topic']"
        :schema="characterSchema"
        entity-name="角色"
        generation-prompt-label="输入你想要生成的角色描述，例如：一位从未来穿越而来的机器人武士"
        storage-key="character-generator"
        title="AI 角色生成器"
        @accept="addGeneratedCharacter"
    />

    <n-list bordered clickable hoverable>
      <CharacterItemDisplay
          v-for="char in characterStore.characters"
          :key="char.id"
          :character-id="char.id"
      />
    </n-list>

    <!-- 这个编辑器只用于“新建”模式 -->
    <EntityEditor
        v-model:show="showCreateModal"
        :initial-data="null as Partial<Character> | null"
        :schema="characterSchema"
        entity-name="角色"
        mode="create"
        @save="handleCreate"
    />
  </n-flex>
</template>

<script lang="ts" setup>
import {ref} from 'vue';
import {NButton, NFlex, NH1, NInput, NList, NPageHeader, useMessage} from 'naive-ui';
import {useCharacterStore} from '../stores/characterStore';
import type {Character} from '#/types/models';
import GeneratorPanel from "#/components/GeneratorPanel.vue";
import EntityEditor from "#/components/EntityEditor.vue";
import CharacterItemDisplay from "#/components/CharacterItemDisplay.vue";
import {characterSchema} from "#/schemas/entitySchemas.ts";

const characterStore = useCharacterStore();
const message = useMessage();

// --- 角色模态框逻辑 ---
const showCreateModal = ref(false);
function openCreateModal() {
  showCreateModal.value = true;
}
function handleCreate(charData: Omit<Character, 'id'>) {
  characterStore.addCharacter(charData);
  message.success('新角色已创建');
}

// --- AI 生成器逻辑 ---
const generatedCharacter = ref<Partial<Character> | null>(null);
function addGeneratedCharacter(charData: Omit<Character, 'id'>) {
  characterStore.addCharacter(charData);
  message.success(`角色 “${charData.name}” 已成功创建！`);
  generatedCharacter.value = null;
}
</script>