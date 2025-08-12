<template>
  <div :class="{ 'is-list-disabled': isParentDisabled }" class="collapsible-config-list">
    <draggable
        :animation="150"
        :group="{ name: groupName, put: [groupName] }"
        :model-value="items"
        class="draggable-area"
        ghost-class="workbench-ghost-item"
        handle=".drag-handle"
        item-key="configId"
        @update:model-value="value => emit('update:items', value)"
    >
      <div v-for="(element, index) in items" :key="element.configId">
        <!--
          使用作用域插槽将渲染权交还给父组件。
          父组件可以访问到列表中的每个元素和它的索引。
        -->
        <slot :element="element as T" :index="index" name="item"></slot>
      </div>
      <div v-if="items && items.length === 0" class="empty-zone">
        <n-empty :description="emptyDescription" size="tiny"/>
      </div>
    </draggable>
  </div>
</template>
<script generic="T extends AbstractRuneConfig | TuumConfig" lang="ts" setup>
import {VueDraggable as draggable} from 'vue-draggable-plus';
import {NEmpty, useThemeVars} from 'naive-ui';
import type {AbstractRuneConfig, TuumConfig} from '@/app-workbench/types/generated/workflow-config-api-client';
import {computed, inject, ref} from "vue";
import {IsParentDisabledKey} from "@/app-workbench/utils/injectKeys.ts";

const props = withDefaults(defineProps<{
  items: T[];
  groupName: string;
  emptyDescription?: string;
}>(), {
  emptyDescription: '拖拽项目到此处',
});

const emit = defineEmits<{
  (e: 'update:items', value: T[]): void;
}>();

const isParentDisabled = inject(IsParentDisabledKey, ref(false));

const themeVars = useThemeVars();
// 将主题变量暴露给 <style>
const listBackgroundColor = computed(() => themeVars.value.actionColor);
const listBorderColor = computed(() => themeVars.value.borderColor);
const listDisabledBackgroundColor = computed(() => themeVars.value.inputColorDisabled);
const listDisabledBorderColor = computed(() => themeVars.value.borderColor);
</script>

<style scoped>
.collapsible-config-list {
  border-radius: 4px;
  padding: 6px 2px 6px 12px;
  background-color: v-bind(listBackgroundColor);
  border-right: 1px dashed v-bind(listBorderColor);
  border-bottom: 1px dashed v-bind(listBorderColor);
  border-left: 1px dashed v-bind(listBorderColor);
  display: flex;
  flex-direction: column;
  transition: background-color 0.3s, border-color 0.3s;
}

.draggable-area {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.empty-zone {
  border: 1px solid v-bind(listBorderColor);
}

/* 列表禁用时的样式 */
.collapsible-config-list.is-list-disabled {
  background-color: v-bind(listDisabledBackgroundColor);
  border-color: v-bind(listDisabledBorderColor);
}
</style>