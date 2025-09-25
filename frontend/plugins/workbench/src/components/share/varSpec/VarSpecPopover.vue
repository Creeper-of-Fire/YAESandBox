<!-- VarSpecPopover.vue (新基础组件) -->
<template>
  <n-popover :placement="placement" :style="{ maxWidth: '300px' }" trigger="hover">
    <!--
      #trigger 插槽的内容由父组件通过默认插槽提供。
      我们将父组件塞进来的所有东西，都放到 n-popover 的 trigger 位置。
    -->
    <template #trigger>
      <slot></slot>
    </template>

    <!--
      浮动卡片的内容是固定的，所以直接写在这里。
      这是所有组件共享的、不变的部分。
    -->
    <n-flex vertical>
      <n-text strong>变量名：{{ varName || '未命名' }}</n-text>
      <n-text>类型: {{ specDef.typeName }}</n-text>

      <!-- 只有当 isOptional 字段存在时，才渲染这一行 -->
      <n-text v-if="isOptional !== undefined">
        可选性:
        <n-tag :size="size" :type="isOptional ? 'warning' : 'success'">{{ isOptional ? '可选' : '必需' }}</n-tag>
      </n-text>

      <n-text depth="3">描述: {{ specDef.description || '无' }}</n-text>
    </n-flex>
  </n-popover>
</template>

<script lang="ts" setup>
import { NFlex, NPopover, NTag, NText } from 'naive-ui';
import type { VarSpecDef } from "#/types/generated/workflow-config-api-client";
import type { Placement } from "vueuc/lib/binder/src/interface";

withDefaults(defineProps<{
  specDef: VarSpecDef;
  varName?: string | null;
  isOptional?: boolean;
  placement?: Placement;
  size?: 'small' | 'medium' | 'large';
}>(), {
  varName: null,
  isOptional: undefined,
  placement: 'right',
  size: 'small',
});
</script>