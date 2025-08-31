<!-- DraggablePromptItem.vue -->
<template>
  <ConfigItemBase
      style="user-select: none"
      :enabled="finalEnabledState"
      :hidden-switch="context === 'pool'"
      :highlight-color-calculator="promptItem.name"
      :is-selected="isSelected"
      is-draggable
      @dblclick="$emit('edit', promptItem.identifier)"
      @update:enabled="(newVal) => $emit('update:enabled', newVal)"
  >
    <template #content="{ titleClass }">
      <div class="prompt-item-content">
        <n-icon class="item-icon">
          <component :is="itemIcon"/>
        </n-icon>
        <span :class="titleClass" class="item-name">{{ promptItem.name }}</span>
      </div>
    </template>
    <template #actions>
      <n-button circle size="small" text @click.stop="$emit('edit', promptItem.identifier)">
        <template #icon>
          <n-icon :component="EditIcon"/>
        </template>
      </n-button>
      <n-button v-if="showDelete" circle size="small" text @click.stop="$emit('delete', promptItem.identifier)">
        <template #icon>
          <n-icon :color="themeVars.errorColor" :component="DeleteIcon"/>
        </template>
      </n-button>
      <n-button v-if="showUnlink" circle size="small" text @click.stop="$emit('unlink', promptItem.identifier)">
        <template #icon>
          <n-icon :color="themeVars.errorColor" :component="UnlinkIcon"/>
        </template>
      </n-button>
    </template>
  </ConfigItemBase>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {NButton, NIcon, useThemeVars} from 'naive-ui';
import type {PromptItem} from './sillyTavernPreset';
// 假设你有一些图标
import {
  DeleteIcon,
  DocumentTextIcon as ContentIcon,
  EditIcon,
  LinkIcon as UnlinkIcon,
  PricetagIcon as MarkerIcon
} from '@yaesandbox-frontend/shared-ui/icons';
import ConfigItemBase from "#/components/share/renderer/ConfigItemBase.vue";

const props = defineProps<{
  promptItem: PromptItem;
  enabled: boolean;
  isSelected: boolean;
  context: 'pool' | 'order'; // 'pool' = 左栏, 'order' = 右栏
}>();

defineEmits(['edit', 'delete', 'unlink', 'update:enabled']);

const itemIcon = computed(() => props.promptItem.marker ? MarkerIcon : ContentIcon);

const showDelete = computed(() => props.context === 'pool');
const showUnlink = computed(() => props.context === 'order');

const finalEnabledState = computed(() =>
{
  if (props.context === 'pool')
  {
    // 在 'pool' 上下文中，我们希望它永远是 'enabled' (亮的) 状态
    return true;
  }
  // 在 'order' 上下文中，它才真正反映 OrderItem 的 enabled 状态。
  return props.enabled;
});

const themeVars = useThemeVars();
</script>

<style scoped>
.prompt-item-content {
  flex-grow: 1;
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}
</style>