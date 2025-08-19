<template>
  <n-popover :style="{ maxWidth: '300px' }" placement="right" trigger="hover">
    <!-- 触发器: 显示给用户看的标签部分 -->
    <template #trigger>
      <n-flex :size="4" :wrap="false" align="center">
        <!-- ✨ type 直接由 props 决定 -->
        <n-tag :size="size" :type="tagType">
          {{ specDef.typeName }}
        </n-tag>
      </n-flex>
    </template>

    <!-- 浮动卡片: 显示详细信息 -->
    <n-space vertical>
      <n-text strong>变量名：{{ varName }}</n-text>
      <n-text>类型: {{ specDef.typeName }}</n-text>

      <!-- 只有当 isOptional 字段存在时，才渲染这一行 -->
      <n-text v-if="isOptional !== undefined">
        可选性:
        <n-tag :size="size" :type="tagType">{{ isOptional ? '可选' : '必需' }}</n-tag>
      </n-text>

      <n-text depth="3">描述: {{ specDef.description || '无' }}</n-text>
    </n-space>
  </n-popover>
</template>

<script lang="ts" setup>
import {NFlex, NPopover, NSpace, NTag, NText} from 'naive-ui';
import type {VarSpecDef} from "#/types/generated/workflow-config-api-client";

type TagType = 'default' | 'success' | 'warning' | 'error' | 'info';

const props = withDefaults(defineProps<{
  specDef: VarSpecDef;
  varName?: string | null;
  isOptional?: boolean;
  size?: 'small' | 'medium' | 'large';
  /**
   * 由外部直接提供标签的类型，决定其颜色。
   */
  tagType?: TagType;
}>(), {
  name: null,
  isOptional: undefined,
  size: 'small',
  tagType: 'default', // 提供一个安全的默认值
});
</script>