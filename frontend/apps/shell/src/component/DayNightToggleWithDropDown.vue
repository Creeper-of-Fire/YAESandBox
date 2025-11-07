<!-- DayNightToggleWithDropDown -->
<template>
  <DayNightToggle
      :ref="setTriggerRef"
      v-model="isCurrentlyDark"
      :duration="1000"
      :size="1.5"
      @click="handleToggleClick($event)"
  />
  <ContextMenu/>
</template>
<script lang="tsx" setup>
import DayNightToggle from "#/component/DayNightToggle.vue";
import {computed, inject, ref, type VNode} from "vue";
import {IsDarkThemeKey, type ThemeControl, ThemeControlKey} from "@yaesandbox-frontend/core-services/inject-key";
import {NSwitch} from "naive-ui";
import {useContextMenu} from "@yaesandbox-frontend/shared-ui";

const themeControl = inject(ThemeControlKey) as ThemeControl;
const isCurrentlyDark = inject(IsDarkThemeKey);

if (!themeControl || !isCurrentlyDark)
{
  throw new Error('主题约束没有被注入，请确保父组件提供了它们。');
}

// 从 themeControl 中解构出需要的方法和只读状态
const {toggleTheme, toggleSystemMode, isFollowingSystem} = themeControl;

const longPressed = ref(false);

/**
 * 处理主切换按钮的点击事件。
 */
const handleToggleClick = (event: MouseEvent) =>
{
  if (longPressed.value)
  {
    longPressed.value = false; // 消费并重置标志位
    return; // 阻止单击事件逻辑
  }
  toggleTheme(event);
}


const themeOptions = computed(() => [
  {
    key: 'system-toggle-render',
    type: 'render', // 指定类型为 render，这样 naive-ui 不会处理它的点击事件
    render: (): VNode => (
        <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              minWidth: '120px',
              cursor: 'pointer',
              padding: '6px 12px',
            }}
            onClick={(event: MouseEvent) =>
            {
              toggleSystemMode(event);
              // 切换后关闭菜单
              hideMenu();
            }}
        >
          <span>跟随系统</span>
          <NSwitch value={isFollowingSystem.value}/>
        </div>
    )
  },
]);

const {setTriggerRef, ContextMenu, hideMenu} = useContextMenu(themeOptions, {
  triggerOn: ['contextmenu', 'longpress'],
  longpressOptions: {delay: 500},
  onShow: () =>
  {
    longPressed.value = true;
  }
});
</script>