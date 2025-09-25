<!-- VarSpecTag -->
<template>
  <VarSpecPopover v-bind="props">
    <n-flex :size="4" :wrap="false" align="center">
      <n-tag :size="size" :type="tagType">
        {{ specDef.typeName }}
      </n-tag>
    </n-flex>
  </VarSpecPopover>
</template>

<script lang="ts" setup>
import {NFlex, NTag} from 'naive-ui';
import type {VarSpecDef} from "#/types/generated/workflow-config-api-client";
import type {Placement} from "vueuc/lib/binder/src/interface";
import VarSpecPopover from "#/components/share/varSpec/VarSpecPopover.vue";

type TagType = 'default' | 'success' | 'warning' | 'error' | 'info';

const props = withDefaults(defineProps<{
  specDef: VarSpecDef;
  varName?: string | null;
  isOptional?: boolean;
  placement?: Placement;
  size?: 'small' | 'medium' | 'large';
  /**
   * 由外部直接提供标签的类型，决定其颜色。
   */
  tagType?: TagType;
}>(), {
  varName: null,
  isOptional: undefined,
  placement: 'right',
  size: 'small',
  tagType: 'default', // 提供一个安全的默认值
});
</script>