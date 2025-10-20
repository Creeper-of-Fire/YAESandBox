<!-- src/components/share/.../ValidationStatusIndicator.vue -->
<template>
  <div v-if="validationInfo" class="validation-indicator">
    <n-popover :style="{ maxWidth: '350px' }" trigger="hover">
      <template #trigger>
        <n-icon :color="iconColor">
          <component :is="iconComponent"/>
        </n-icon>
      </template>

      <!-- Popover 内容 -->
      <n-flex :size="0" vertical>
        <!-- 错误区域 -->
        <div v-if="validationInfo.errors" class="validation-section">
          <n-flex align="center" class="section-header">
            <n-h5 class="section-title">错误</n-h5>
            <n-badge :value="validationInfo.errors.count" type="error"/>
          </n-flex>
          <n-list :show-divider="false" class="message-list">
            <n-list-item v-for="(msg, index) in validationInfo.errors.messages" :key="`err-${index}`">
              {{ msg.message }}
            </n-list-item>
          </n-list>
        </div>

        <!-- 分割线 -->
        <n-divider v-if="validationInfo.errors && validationInfo.warnings" class="section-divider"/>

        <!-- 警告区域 -->
        <div v-if="validationInfo.warnings" class="validation-section">
          <n-flex align="center" class="section-header">
            <n-h5 class="section-title">警告</n-h5>
            <n-badge :value="validationInfo.warnings.count" type="warning"/>
          </n-flex>
          <n-list :show-divider="false" class="message-list">
            <n-list-item v-for="(msg, index) in validationInfo.warnings.messages" :key="`warn-${index}`">
              {{ msg.message }}
            </n-list-item>
          </n-list>
        </div>
      </n-flex>
    </n-popover>
  </div>
</template>

<script lang="ts" setup>
import {computed, type PropType} from 'vue';
import {NBadge, NDivider, NFlex, NH5, NIcon, NList, NListItem, NPopover, useThemeVars} from 'naive-ui';
import {ErrorIcon, WarningIcon} from '@yaesandbox-frontend/shared-ui/icons';
import type {ValidationInfo} from "#/components/share/validationInfo/useValidationInfo";

const props = defineProps({
  validationInfo: {
    type: Object as PropType<ValidationInfo>,
    default: null,
  },
});

const themeVars = useThemeVars();

// 决定显示哪个图标
const iconComponent = computed(() => {
  const info = props.validationInfo;
  if (!info) return null;

  // 如果同时存在错误和警告，使用 WarningIcon
  if (info.errors && info.warnings) {
    return WarningIcon;
  }
  // 如果只有错误，使用 ErrorIcon
  if (info.errors) {
    return ErrorIcon;
  }
  // 如果只有警告，使用 WarningIcon
  if (info.warnings) {
    return WarningIcon;
  }

  return null;
});

// 决定图标的颜色
const iconColor = computed(() => {
  const info = props.validationInfo;
  if (!info) return '';

  // 只要有错误，颜色就是红色
  if (info.errors) {
    return themeVars.value.errorColor;
  }
  // 如果没有错误但有警告，颜色是黄色
  if (info.warnings) {
    return themeVars.value.warningColor;
  }
  return '';
});
</script>

<style scoped>
.validation-indicator {
  display: flex;
  align-items: center;
  cursor: help;
}

.validation-section {
  padding: 4px 0;
}

.section-header {
  margin-bottom: 8px;
}

.section-title {
  margin: 0;
  font-weight: 600;
  color: v-bind('themeVars.textColor1');
}

.message-list {
  background-color: transparent; /* 移除 n-list 的默认背景色 */
}

/* 覆盖 n-list-item 的默认内边距，使其更紧凑 */
:deep(.n-list-item) {
  padding: 4px 0 !important;
}

.section-divider {
  margin: 8px 0;
}
</style>