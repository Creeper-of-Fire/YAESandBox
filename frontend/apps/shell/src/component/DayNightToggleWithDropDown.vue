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
import {type ComponentPublicInstance, computed, inject, ref, shallowRef, type VNode} from "vue";
import {IsDarkThemeKey} from "@yaesandbox-frontend/core-services/inject-key";
import {NSwitch} from "naive-ui";
import {useContextMenu} from "@yaesandbox-frontend/shared-ui";

const isCurrentlyDark = inject(IsDarkThemeKey, ref(false));
const props = defineProps<{
  themeMode: 'light' | 'dark' | 'system';
}>();
const emit = defineEmits<{
  (e: 'update:themeMode', value: 'light' | 'dark' | 'system'): void;
  (e: 'toggle', event: MouseEvent): void;
}>();

const longPressed = ref(false);

// 这是切换“跟随系统”模式的核心逻辑
const toggleSystemMode = () =>
{
  let newMode: 'light' | 'dark' | 'system';
  if (props.themeMode === 'system')
  {
    // 如果当前是“跟随系统”，则关闭它
    // 新模式将固定为当前正在显示的主题
    newMode = isCurrentlyDark.value ? 'dark' : 'light';
  }
  else
  {
    // 如果当前是明确模式，则开启“跟随系统”
    newMode = 'system';
  }
  emit('update:themeMode', newMode);
  // 切换后关闭下拉菜单
  hideMenu();
};

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
            onClick={toggleSystemMode}
        >
          <span>跟随系统</span>
          <NSwitch value={props.themeMode === 'system'}/>
        </div>
    )
  },
]);

const handleToggleClick = (event: MouseEvent) =>
{
  if (longPressed.value)
  {
    longPressed.value = false; // 消费并重置标志位
    return; // 阻止单击事件逻辑
  }

  let newMode: 'light' | 'dark' | 'system';
  // 如果当前是跟随系统模式，点击后应该切换到明确的模式
  if (props.themeMode === 'system')
  {
    // 如果当前显示为暗色，则切换到亮色模式，反之亦然
    newMode = isCurrentlyDark.value ? 'light' : 'dark';
  }
  else
  {
    // 如果已经是明确模式，则在亮/暗之间切换
    newMode = props.themeMode === 'light' ? 'dark' : 'light';
  }
  emit('update:themeMode', newMode);

  // 在更新 v-model 的同时，发出带有 MouseEvent 的 toggle 事件
  emit('toggle', event);
}

const {setTriggerRef, ContextMenu, hideMenu} = useContextMenu(themeOptions, {
  triggerOn: ['contextmenu', 'longpress'],
  longpressOptions: {delay: 500},
  onShow: () => {
    longPressed.value = true;
  }
});
</script>