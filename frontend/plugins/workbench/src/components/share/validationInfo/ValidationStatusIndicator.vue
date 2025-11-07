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
      <component :is="popoverContentVNode"/>
    </n-popover>
  </div>
</template>

<script lang="tsx" setup>
import {computed, type PropType, type VNode} from 'vue';
import {NBadge, NDivider, NFlex, NH5, NIcon, NList, NListItem, NPopover, useThemeVars} from 'naive-ui';
import {ErrorIcon, WarningIcon} from '@yaesandbox-frontend/shared-ui/icons';
import type {ValidationInfo, ValidationResult} from "#/components/share/validationInfo/useValidationInfo";

const props = defineProps({
  validationInfo: {
    type: Object as PropType<ValidationInfo>,
    default: null,
  },
});

const themeVars = useThemeVars();

// 决定显示哪个图标
const iconComponent = computed(() =>
{
  const info = props.validationInfo;
  if (!info) return null;

  // 如果同时存在错误和警告，使用 WarningIcon
  if (info.errors && info.warnings)
  {
    return WarningIcon;
  }
  // 如果只有错误，使用 ErrorIcon
  if (info.errors)
  {
    return ErrorIcon;
  }
  // 如果只有警告，使用 WarningIcon
  if (info.warnings)
  {
    return WarningIcon;
  }

  return null;
});

// 决定图标的颜色
const iconColor = computed(() =>
{
  const info = props.validationInfo;
  if (!info) return '';

  // 只要有错误，颜色就是红色
  if (info.errors)
  {
    return themeVars.value.errorColor;
  }
  // 如果没有错误但有警告，颜色是黄色
  if (info.warnings)
  {
    return themeVars.value.warningColor;
  }
  return '';
});

/**
 * 渲染一个独立的校验区块（错误或警告）
 * @param title 区块标题, "错误" 或 "警告"
 * @param type 徽章类型, 'error' 或 'warning'
 * @param data 包含数量和消息列表的校验结果对象
 * @param keyPrefix 用于生成列表项 key 的前缀
 */
const renderValidationSection = (
    title: string,
    type: 'error' | 'warning',
    data: ValidationResult,
    keyPrefix: string
): VNode =>
{
  return (
      <div class="validation-section">
        <NFlex align="center" class="section-header">
          <NH5 class="section-title">{title}</NH5>
          <NBadge value={data.count} type={type}/>
        </NFlex>
        <NList show-divider={false} class="message-list">
          {data.messages.map((msg, index) => (
              <NListItem key={`${keyPrefix}-${index}`}>{msg.message}</NListItem>
          ))}
        </NList>
      </div>
  );
};

/**
 * 计算属性，用于生成 Popover 内容的 VNode。
 * 这种方式将渲染逻辑集中在 script 中，使代码更清晰、更易于维护。
 */
const popoverContentVNode = computed(() =>
{
  if (!props.validationInfo)
  {
    return null;
  }

  const {errors, warnings} = props.validationInfo;
  const contentNodes: VNode[] = [];

  // 1. 如果有错误，渲染错误区块
  if (errors)
  {
    contentNodes.push(renderValidationSection('错误', 'error', errors, 'err'));
  }

  // 2. 如果同时有错误和警告，渲染分割线
  if (errors && warnings)
  {
    contentNodes.push(<NDivider class="section-divider"/>);
  }

  // 3. 如果有警告，渲染警告区块
  if (warnings)
  {
    contentNodes.push(renderValidationSection('警告', 'warning', warnings, 'warn'));
  }

  // 4. 将所有生成的 VNode 包裹在一个 NFlex 容器中返回
  return <NFlex size={0} vertical>{contentNodes}</NFlex>;
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