<!-- src/components/AdvancedSettingsModal.vue -->
<script setup lang="ts">
import { computed } from 'vue';
import { useConfigStore } from '../stores/configStore.ts';
import BaseModal from './BaseModal.vue';
import InfoPopover from "./InfoPopover.vue";

defineProps<{
  modelValue: boolean;
}>();

const emit = defineEmits(['update:modelValue']);

const configStore = useConfigStore();

// 筛选出除了核心清单URL和主题之外的其他所有配置项
const otherEntries = computed(() =>
    configStore.configEntries.filter(e =>
        e.key !== configStore.CORE_MANIFEST_KEY && e.key !== 'theme'
    )
);

// 为 v-model 创建一个处理函数，以调用 store 的 action
function handleInput(key: string, event: Event) {
  const target = event.target as HTMLInputElement;
  configStore.updateConfigValue(key, target.value);
}
</script>

<template>
  <BaseModal
      :model-value="modelValue"
      title="高级设置"
      @update:modelValue="emit('update:modelValue', $event)"
  >
    <div class="advanced-settings-form">
      <p class="form-description">
        此处的更改会立即保存。请谨慎修改。<br />
        如果不慎修改失误，可以删除应用程序所在目录下的“launcher.config”文件，以恢复默认设置。
      </p>
      <div v-for="entry in otherEntries" :key="entry.key" class="setting-group">
        <div class="label-wrapper">
          <label :for="`setting-${entry.key}`">{{ entry.key }}:</label>
          <InfoPopover :text="entry.comments.join('\n')"/>
        </div>
        <!--
          这里不直接使用 v-model="entry.value" 是因为 Pinia state 默认是只读的。
          我们通过 :value 绑定值，并通过 @input 事件调用 action 来确保修改是受控的。
        -->
        <input
            :id="`setting-${entry.key}`"
            :value="entry.value"
            type="text"
            @input="handleInput(entry.key, $event)"
        >
      </div>
      <div v-if="otherEntries.length === 0" class="no-settings">
        没有其他可用的高级设置。
      </div>
    </div>
  </BaseModal>
</template>

<style scoped>
.advanced-settings-form {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.form-description {
  font-size: 0.9em;
  color: var(--text-color-muted);
  margin-top: 0;
  margin-bottom: 1rem;
  border-left: 3px solid var(--border-color-strong);
  padding-left: 0.75rem;
}

.setting-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.setting-group label {
  font-weight: 600;
  font-size: 0.9em;
  color: var(--text-color-primary);
}

/* 用于对齐 Label 和 Popover 图标 */
.label-wrapper {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.label-wrapper label {
  font-weight: 600;
  font-size: 0.9em;
  color: var(--text-color-primary);
}

input[type="text"] {
  width: 100%;
  box-sizing: border-box;
  padding: 0.6rem;
  border-radius: 4px;
  border: 1px solid var(--border-color-strong);
  background-color: var(--bg-color-main);
  color: var(--text-color-primary);
}

.no-settings {
  text-align: center;
  color: var(--text-color-muted);
  padding: 1rem;
}
</style>