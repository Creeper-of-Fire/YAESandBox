<!-- era-lite/src/components/CharacterItemDisplay.vue -->
<template>
  <n-list-item v-if="character">
    <template #prefix>
      <CharacterAvatar :character="character" :size="48"/>
    </template>
    <n-thing
        :description="character.description"
        :description-style="{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }"
        :title="character.name"/>

    <template #suffix>
      <n-flex>
        <n-button :type="sessionStore.playerCharacterId === character.id ? 'primary' : 'default'" ghost
                  @click="sessionStore.setPlayerCharacter(character.id)">设为主角
        </n-button>
        <n-button :type="sessionStore.targetCharacterId === character.id ? 'success' : 'default'"
                  ghost @click="sessionStore.setTargetCharacter(character.id)">设为交互对象
        </n-button>
        <n-button circle @click="openEditModal">
          <template #icon>
            <n-icon :component="EditIcon"/>
          </template>
        </n-button>
        <n-button circle ghost type="error" @click="handleDelete">
          <template #icon>
            <n-icon :component="DeleteIcon"/>
          </template>
        </n-button>
      </n-flex>
    </template>
  </n-list-item>

  <EntityEditor
      v-if="character"
      v-model:show="showEditorModal"
      :initial-data="character"
      :schema="characterSchema"
      entity-name="角色"
      mode="edit"
      @save="handleSave"
  />
</template>

<script lang="ts" setup>
import {computed, ref} from 'vue';
import {NButton, NFlex, NIcon, NListItem, NThing, useDialog, useMessage} from 'naive-ui';
import {useCharacterStore} from '#/features/characters/characterStore';
import {useSessionStore} from '#/stores/sessionStore';
import {Pencil as EditIcon, TrashBinOutline as DeleteIcon} from '@vicons/ionicons5';
import type {Character} from '#/types/models';
import EntityEditor from "#/components/EntityEditor.vue";
import {characterSchema} from "#/schemas/entitySchemas";
import CharacterAvatar from "#/components/CharacterAvatar.vue";

const props = defineProps<{ characterId: string }>();

const characterStore = useCharacterStore();
const sessionStore = useSessionStore();
const dialog = useDialog();
const message = useMessage();

const character = computed(() => characterStore.characters.find(c => c.id === props.characterId));

const showEditorModal = ref(false);

function openEditModal()
{
  showEditorModal.value = true;
}

function handleSave(updatedChar: Character)
{
  characterStore.updateCharacter(updatedChar);
  message.success('角色信息已更新');
}

function handleDelete()
{
  if (!character.value) return;
  dialog.warning({
    title: '确认删除',
    content: `你确定要删除角色 “${character.value.name}” 吗？`,
    positiveText: '确定',
    negativeText: '取消',
    onPositiveClick: () =>
    {
      characterStore.deleteCharacter(props.characterId);
      message.success('角色已删除');
    },
  });
}
</script>