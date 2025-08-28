<template>
  <div class="form-field-wrapper">
    <div class="label-container">

      <!-- 情况一：当是分组中的唯一字段时，渲染为 n-divider -->
      <n-divider
          v-if="isSingleInGroup"
          :style="{ marginTop: 'var(--form-divider-margin-top)', marginBottom: 'var(--form-divider-margin-bottom)'}"
          title-placement="left"
      >
        {{ label }}
        <n-popover v-if="description" trigger="hover">
          <template #trigger>
            <!-- 添加一些样式让图标和文字对齐更好看 -->
            <n-icon style="vertical-align: middle; margin-left: 4px; cursor: pointer;">
              <InfoIcon/>
            </n-icon>
          </template>
          {{ description }}
        </n-popover>
      </n-divider>

      <!-- 情况二：否则，使用 n-text 渲染方式 -->
      <n-text v-else :key="name">
        {{ label }}
        <n-popover v-if="description" trigger="hover">
          <template #trigger>
            <n-icon>
              <InfoIcon/>
            </n-icon>
          </template>
          {{ description }}
        </n-popover>
      </n-text>

    </div>

    <!-- 这里是真正插入组件的地方 -->
    <div class="component-container">
      <slot></slot>
    </div>

    <!-- vee-validate 的错误信息组件 -->
    <ErrorMessage :name="name" as="small" class="error-message"/>
  </div>
</template>

<script lang="ts" setup>
import {ErrorMessage} from 'vee-validate';
import {InfoIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {useThemeVars} from "naive-ui";

defineProps<{
  name: string;
  label: string;
  description?: string;
  isSingleInGroup: boolean;
}>();
const themeVars = useThemeVars();
</script>

<style scoped>
.form-field-wrapper {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.component-container {
  width: 100%;
}

.label-container {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-bottom: 0.5rem;
}

.error-message {
  color: v-bind('themeVars.errorColorSuppl');
  margin-top: 0.25rem;
  min-height: 1.25em; /* 防止布局跳动 */
  font-size: 0.875rem;
}
</style>