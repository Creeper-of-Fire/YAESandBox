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
        @update:model-value="(value:T[]) => emit('update:items', value)"
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
import type {AbstractRuneConfig, TuumConfig} from '#/types/generated/workflow-config-api-client';
import {computed, inject, ref} from "vue";
import {IsParentDisabledKey} from "#/utils/injectKeys.ts";

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
  user-select: none;
  border-radius: 4px 4px 4px 4px;
  border: 1px dashed v-bind(listBorderColor);
  background-color: v-bind(listBackgroundColor);
  display: flex;
  flex-direction: column;
  transition: background-color 0.3s, border-color 0.3s;
}

/* 选择所有作为其他列表后代的列表 */
.collapsible-config-list .collapsible-config-list {
  /* 同样的样式覆盖 */
  border-radius: 0 0 0 0;
  border-right: none;
  border-top: none;
  border-bottom: none;
  border-left: none;
  margin-left: 0;
  padding-top: 4px;
  padding-bottom: 4px;
  padding-left: 10px;
}

/* 选择所有作为其他列表后代的后代的列表 */
.collapsible-config-list .collapsible-config-list .collapsible-config-list {
  border-bottom-left-radius: 4px;
  border-left: 1px dashed v-bind(listBorderColor);
  border-bottom: 1px dashed v-bind(listBorderColor);
}

.draggable-area {
  display: flex;
  flex-direction: column;
}

.empty-zone {
  border: 1px solid v-bind(listBorderColor);
  margin: 4px;
}

/* 列表禁用时的样式 */
.collapsible-config-list.is-list-disabled {
  background-color: v-bind(listDisabledBackgroundColor);
  border-color: v-bind(listDisabledBorderColor);
}
</style>