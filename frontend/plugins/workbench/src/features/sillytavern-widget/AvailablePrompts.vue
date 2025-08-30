<!-- AvailablePrompts.vue -->
<template>
  <n-scrollbar style="max-height: 100%;">
    <div class="available-prompts-container">

      <VueDraggable
          v-if="localPrompts.length > 0"
          v-model="localPrompts"
          :group="draggableGroupConfig"
          :sort="false"
          class="prompts-list"
          ghost-class="ghost-item"
          item-key="identifier"
      >
        <div v-for="item in localPrompts" :key="item.identifier" class="draggable-list-item">
          <DraggablePromptItem
              :enabled="item.enabled ?? true"
              :is-selected="false"
              :prompt-item="item"
              context="pool"
              @delete="handleDelete"
              @edit="handleEdit"
          />
        </div>
      </VueDraggable>

      <div v-else class="empty-state">
        <n-empty description="尚未定义任何提示词">
          <template #extra>
            <n-text depth="3">
              点击上方的“添加新提示词”按钮来创建一个。
            </n-text>
          </template>
        </n-empty>
      </div>
    </div>
  </n-scrollbar>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {NEmpty, NText, useThemeVars} from 'naive-ui';
import {VueDraggable} from 'vue-draggable-plus';
import DraggablePromptItem from './DraggablePromptItem.vue';
import type {PromptItem} from './sillyTavernPreset';

// 1. 定义 Props 和 Emits
const props = defineProps<{
  prompts: PromptItem[];
}>();

const emit = defineEmits<{
  (e: 'update:prompts', value: PromptItem[]): void;
  (e: 'edit', identifier: string): void;
  (e: 'delete', identifier: string): void;
}>();

// 2. 实现 v-model:prompts
// 使用 computed 属性来代理 props，这是 Vue 3 中处理 v-model 的标准做法。
// 注意：由于我们设置了 sort: false 和 put: false, setter 实际上不会被拖拽操作调用，
// 但为了 v-model 的完整性以及未来可能的扩展，保留此结构是良好的实践。
const localPrompts = computed({
  get: () => props.prompts,
  set: (value) =>
  {
    emit('update:prompts', value);
  },
});

// 3. 配置拖拽行为
const draggableGroupConfig = {
  name: 'silly-tavern-prompts', // 必须与 ActivePromptOrder.vue 中的组名相同
  pull: 'clone', // 关键！拖拽时复制而不是移动
  put: false,    // 关键！禁止将任何项拖入此列表
} as const;

// 4. 事件处理器
// 组件本身不处理业务逻辑，只是将事件冒泡给父组件
const handleEdit = (identifier: string) =>
{
  emit('edit', identifier);
};

const handleDelete = (identifier: string) =>
{
  emit('delete', identifier);
};

const themeVars = useThemeVars();
</script>

<style scoped>
.available-prompts-container {
  border: 1px solid v-bind('themeVars.borderColor');
  border-radius: v-bind('themeVars.borderRadius');
  background-color: v-bind('themeVars.baseColor');
  padding: 8px;
  height: 100%;
  min-height: 400px; /* 确保有一个合理的最小高度 */
  overflow-y: auto;
}

.prompts-list {
  display: flex;
  flex-direction: column;
  gap: 4px; /* 项目之间的间距 */
}

.draggable-list-item {
  /* 可以在这里添加一些样式，比如 transition */
  transition: all 0.2s ease-in-out;
}

/* 拖拽时的占位符样式 */
.ghost-item {
  opacity: 0.5;
  background: v-bind('themeVars.actionColor');
  border: 1px dashed v-bind('themeVars.primaryColor');
  border-radius: 4px;
}

.empty-state {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 100%;
  min-height: 400px;
}
</style>