<template>
  <n-config-provider :theme="transitioningTheme" class="app-container">
    <n-message-provider>
      <n-dialog-provider>
        <n-notification-provider>
          <AppShell v-model="themeMode"/>
        </n-notification-provider>
      </n-dialog-provider>
    </n-message-provider>
  </n-config-provider>
  <!-- 可能的全局元素，如全局错误弹窗 -->
  <!--  <GlobalErrorDisplay />-->
  <!--  <AppWideNotifications />-->
</template>

<script lang="ts" setup>
import AppShell from "@/AppShell.vue";
import {computed, provide} from "vue";
import {IsDarkThemeKey} from "@yaesandbox-frontend/core-services/injectKeys";
import {usePreferredDark, useStorage} from "@vueuse/core";
import {useThemeTransition} from "@/composables/useThemeTransition.ts";
import {darkTheme, lightTheme} from "naive-ui";

// --- 状态管理和计算 ---
const themeMode = useStorage<'light' | 'dark' | 'system'>('theme-mode', 'system');
const isSystemDark = usePreferredDark();

const finalThemeName = computed<'light' | 'dark'>(() =>
{
  if (themeMode.value === 'system')
  {
    return isSystemDark.value ? 'dark' : 'light';
  }
  return themeMode.value;
});

// 新增：计算出最终的 isDark 状态
const isDark = computed(() => finalThemeName.value === 'dark');

// --- 主题过渡 ---
const {transitioningTheme} = useThemeTransition(finalThemeName, {light: lightTheme, dark: darkTheme});

// --- 状态提供 ---
// 使用 IsDarkThemeKey 将计算出的 isDark 状态提供给所有后代组件
provide(IsDarkThemeKey, isDark);
</script>