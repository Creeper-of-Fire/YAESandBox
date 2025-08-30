<template>
  <n-config-provider :theme="finalThemeObject" class="app-container">
    <n-message-provider>
      <n-modal-provider>
        <n-dialog-provider>
          <n-notification-provider>
            <AppShell v-model:themeMode="themeMode" @toggle-theme="handleThemeToggle"/>
          </n-notification-provider>
        </n-dialog-provider>
      </n-modal-provider>
    </n-message-provider>
  </n-config-provider>
  <!-- 可能的全局元素，如全局错误弹窗 -->
  <!--  <GlobalErrorDisplay />-->
  <!--  <AppWideNotifications />-->

  <!-- 引入降级方案的遮罩组件 -->
  <ThemeTransitionMask/>
</template>

<script lang="ts" setup>
import AppShell from "#/AppShell.vue";
import {computed, provide, ref, watch} from "vue";
import {IsDarkThemeKey} from "@yaesandbox-frontend/core-services/injectKeys";
import {usePreferredDark} from "@vueuse/core";
import {isTransitioning, triggerThemeTransition} from "#/composables/useThemeTransition.ts";
import {darkTheme, lightTheme} from "naive-ui";
import {useScopedStorage} from "@yaesandbox-frontend/core-services/composables";
import ThemeTransitionMask from "#/component/ThemeTransitionMask.vue";

// --- 状态管理和计算 ---
const themeMode = useScopedStorage<'light' | 'dark' | 'system'>('theme-mode', 'system');
const isSystemDark = usePreferredDark();

const finalThemeName = ref<'light' | 'dark'>(
    (themeMode.value === 'system' ? (isSystemDark.value ? 'dark' : 'light') : themeMode.value)
);

// 监听系统主题和用户设置的变化来更新 finalThemeName
watch([themeMode, isSystemDark], () =>
{
  if (isTransitioning.value) return; // 如果正在过渡，则不立即切换
  const newTheme = themeMode.value === 'system' ? (isSystemDark.value ? 'dark' : 'light') : themeMode.value;
  finalThemeName.value = newTheme;
});

const themes = {light: lightTheme, dark: darkTheme};
const finalThemeObject = computed(() => themes[finalThemeName.value]);

const handleThemeToggle = (event: MouseEvent) =>
{
  if (isTransitioning.value) return;

  const currentTheme = finalThemeName.value;
  const targetTheme = currentTheme === 'light' ? 'dark' : 'light';

  triggerThemeTransition(
      event,
      themes,
      currentTheme,
      targetTheme,
      700, // 动画时长
      () =>
      {
        // 这是核心：这个回调函数会在正确的时机（View Transition 内部或遮罩动画第一帧）
        // 安全地更新我们的主题状态
        finalThemeName.value = targetTheme;
        // 同时更新存储的用户偏好
        // 注意：这里可能需要根据你的 AppShell v-model 逻辑调整
        // 假设我们直接设置 themeMode
        themeMode.value = themeMode.value === 'system' ? targetTheme : (themeMode.value === 'light' ? 'dark' : 'light');
      }
  );
};

// 计算出最终的 isDark 状态
const isDark = computed(() => finalThemeName.value === 'dark');

// --- 状态提供 ---
// 使用 IsDarkThemeKey 将计算出的 isDark 状态提供给所有后代组件
provide(IsDarkThemeKey, isDark);

defineOptions({
  name: 'app-shell:AppMain',
})
</script>
<style>
/* --- ✨ 新增：为 View Transitions API 添加样式 --- */

/*
  这是核心修复：
  告诉浏览器不要对旧视图（即将消失的那个）应用任何默认动画。
  它会静静地待在原地，等待被新视图的圆形动画覆盖。
*/
::view-transition-old(root) {
  animation: none;
}

/* 将上面定义的动画应用到新视图上 */
::view-transition-new(root) {
  animation: reveal-in 0.7s ease-in-out; /* 这里的 0.5s 应该和 JS 里的 duration 匹配 */
}
</style>