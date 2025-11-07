<template>
  <n-config-provider :hljs="hljs" :theme="finalThemeObject" class="app-container">
    <n-message-provider>
      <n-modal-provider>
        <n-dialog-provider>
          <n-notification-provider>
            <AppShell/>
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
import {IsDarkThemeKey, type ThemeControl, ThemeControlKey} from "@yaesandbox-frontend/core-services/inject-key";
import {usePreferredDark} from "@vueuse/core";
import {isTransitioning, triggerThemeTransition} from "#/composables/useThemeTransition.ts";
import {darkTheme, lightTheme} from "naive-ui";
import {useScopedStorage} from "@yaesandbox-frontend/core-services/composables";
import ThemeTransitionMask from "#/component/ThemeTransitionMask.vue";
import hljs from 'highlight.js/lib/core'
import javascript from 'highlight.js/lib/languages/javascript'
import xml from 'highlight.js/lib/languages/xml'

hljs.registerLanguage('javascript', javascript);
hljs.registerLanguage('xml', xml);

// --- 状态管理 ---

const themeMode = useScopedStorage<'light' | 'dark' | 'system'>('theme-mode', 'system');

// 从 VueUse 获取当前操作系统的深色模式状态。
const isSystemDark = usePreferredDark();

// 计算出的“期望”主题。这是逻辑上的目标。
const desiredThemeName = computed<'light' | 'dark'>(() => {
  if (themeMode.value === 'system') {
    return isSystemDark.value ? 'dark' : 'light';
  }
  return themeMode.value;
});

// 真正“当前渲染”的主题。它的更新必须通过动画函数来完成。
const finalThemeName = ref<'light' | 'dark'>(desiredThemeName.value);

const isCurrentlyDark = computed(() => finalThemeName.value === 'dark');
const themes = {light: lightTheme, dark: darkTheme};
const finalThemeObject = computed(() => themes[finalThemeName.value]);

// --- 统一的动画触发器 ---

/**
 * 这是一个核心函数，所有主题变更都必须经过它。
 * 它负责计算变更，并触发带动画的过渡。
 * @param updateIntent - 一个函数，封装了改变用户意图状态的逻辑。
 * @param event - 鼠标事件，用于动画定位。
 */
const changeThemeWithAnimation = (updateIntent: () => void, event: MouseEvent) =>
{
  if (isTransitioning.value) return;

  const currentTheme = finalThemeName.value;

  // 执行意图更新
  updateIntent();

  // 获取意图更新后的目标主题
  const targetTheme = desiredThemeName.value;

  // 如果主题没有实际变化，则不执行任何操作
  if (currentTheme === targetTheme) return;

  triggerThemeTransition(
      event,
      themes,
      currentTheme,
      targetTheme,
      700,
      () =>
      {
        // 在动画的关键帧，安全地更新最终渲染的主题状态。
        finalThemeName.value = targetTheme;
      }
  );
};

/**
 * 监听 `desiredThemeName` 的变化。
 * 这个 watcher 专门处理非用户交互引起的变更，最典型的就是操作系统主题自动切换。
 */
watch(desiredThemeName, (newDesiredTheme) => {
  // 如果动画正在进行，立即返回。这可以防止与用户点击操作发生任何冲突。
  // 动画的控制权完全交给 `changeThemeWithAnimation`。
  if (isTransitioning.value) {
    return;
  }

  // 如果期望的主题与当前渲染的主题不同，则立即进行同步。
  // 这个更新是没有过渡动画的，因为它是由系统自动触发的。
  if (newDesiredTheme !== finalThemeName.value) {
    finalThemeName.value = newDesiredTheme;
  }
});

// --- 实现 ThemeControl 接口 ---

const toggleTheme = (event: MouseEvent) =>
{
  changeThemeWithAnimation(() =>
  {
    if (themeMode.value === 'system') {
      // 从“系统”模式退出，并切换到当前所见主题的对立面
      themeMode.value = isCurrentlyDark.value ? 'light' : 'dark';
    } else {
      // 在 'light' 和 'dark' 之间切换
      themeMode.value = themeMode.value === 'light' ? 'dark' : 'light';
    }
  }, event);
};

const toggleSystemMode = (event: MouseEvent) =>
{
  changeThemeWithAnimation(() =>
  {
    if (themeMode.value === 'system') {
      // 从“系统”模式退出，并“固定”为当前所见的主题
      themeMode.value = isCurrentlyDark.value ? 'dark' : 'light';
    } else {
      // 进入“系统”模式
      themeMode.value = 'system';
    }
  }, event);
};

// --- 状态提供 ---

// 提供主题控制对象
provide<ThemeControl>(ThemeControlKey, {
  toggleTheme,
  toggleSystemMode,
  isFollowingSystem: computed(() => themeMode.value === 'system'),
});

// 提供最终的只读暗色状态
provide(IsDarkThemeKey, isCurrentlyDark);

defineOptions({
  name: 'app-shell:AppMain',
})
</script>
<style>
/* --- ✨ 为 View Transitions API 添加样式 --- */

/*
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