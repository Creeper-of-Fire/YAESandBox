<!-- src/components/common/AppModeSwitcher.vue -->
<template>
  <FloatMenu
      :dimension="50"
      :position="'top left'"
      :menu-data="menuItems"
      :on-selected="handleMenuSelection"
      :menu-dimension="{ width: 200 }"
      :theme="mergedTheme"
      menu-style="accordion"
  >
    <!-- 自定义悬浮按钮的图标或文本 -->
    <template #default>
      <!-- 你可以使用 Naive UI 的图标或其他 SVG 图标 -->
      <n-icon :component="Analytics" size="24">
      </n-icon>
    </template>

    <!-- 如果菜单项需要自定义图标，可以使用具名插槽 -->
    <!-- 例如，如果 menuItems 中有 { name: '游戏模式', iconSlot: 'gameIcon' } -->
    <!-- <template #gameIcon> <GameIconComponent /> </template> -->
  </FloatMenu>
</template>

<script setup lang="ts">
import {Analytics} from '@vicons/ionicons5'
// noinspection TypeScriptCheckImport
import {FloatMenu, type MenuItem} from 'vue-float-menu'; // 确保从库中正确导入类型
import 'vue-float-menu/dist/vue-float-menu.css'; // 导入样式
import {useRouter} from 'vue-router';
import {NIcon} from 'naive-ui';
import {type PropType,computed} from "vue"; // 如果使用Naive UI图标

const router = useRouter();

// 定义期望的 theme prop 的类型
// 这个类型应该与 vue-float-menu 的 theme prop 兼容
interface FloatMenuTheme {
  primary?: string;
  textColor?: string;
  menuBgColor?: string;
  menuTextColor?: string; // 假设的菜单文本颜色 key
  textSelectedColor?: string; // 假设的选中颜色 key
  // ... vue-float-menu 支持的其他主题键
  [key: string]: any;
}

const props = defineProps({
  /**
   * 自定义主题对象，用于覆盖 vue-float-menu 的默认主题。
   * 参考 vue-float-menu 文档了解可用的键。
   */
  customThemeOverride: {
    type: Object as PropType<Partial<FloatMenuTheme>>, // Partial 表示可以只覆盖部分属性
    default: () => ({}) // 默认是一个空对象
  }
});

// 定义菜单项数据
// 注意：MenuItem 类型可能需要根据 vue-float-menu 的实际导出调整
// 我这里假设 MenuItem 是库导出的类型，或者你可以自己定义一个兼容的类型
const menuItems: MenuItem[] = [
  {
    name: '游戏模式',
    // id: 'game-mode' // 可以给每个菜单项一个唯一的ID，方便在 handleMenuSelection 中识别
  },
  {
    name: '编辑器模式',
    // id: 'editor-mode'
  },
  {
    name: '调试模式',
    // id: 'debug-mode',
    // disabled: true, // 可以禁用某些菜单项
  },
  {divider: true}, // 分隔线
  {
    name: '设置',
    // id: 'settings-mode',
    // subMenu: { // 支持子菜单
    //   name: 'settings-sub-menu',
    //   items: [
    //     { name: '偏好设置', id: 'preference-settings' },
    //     { name: '账户设置', id: 'account-settings' },
    //   ]
    // }
  }
];

// 定义一个默认的基础主题 (可选，如果希望有回退)
const defaultAppTheme: FloatMenuTheme = {
  primary: '#1890ff', // 例如，一个应用级的默认主色
  textColor: '#333333',
  menuBgColor: '#ffffff',
  menuTextColor: '#333333',
};

// 合并默认主题和外部注入的主题
const mergedTheme = computed<FloatMenuTheme>(() => {
  return {
    ...defaultAppTheme, // 先应用组件内部的默认值
    ...props.customThemeOverride, // 然后用外部传入的 props 覆盖
  };
});

// 处理菜单项选择的函数
const handleMenuSelection = (selectedItem: MenuItem) => {
  console.log('Selected item:', selectedItem);

  // 根据 selectedItem.name (或你添加的id) 进行路由跳转
  switch (selectedItem.name) { // 或者用 id: selectedItem.id
    case '游戏模式':
      router.push({name: 'game'});
      break;
    case '编辑器模式':
      router.push({name: 'editor'});
      break;
    case '调试模式':
      // router.push({ name: 'DebugViewRoot' }); // 假设调试模式路由
      alert('调试模式暂未开放');
      break;
    case '设置':
      // router.push({ name: 'SettingsPage' });
      alert('设置功能暂未开放');
      break;
      // case 'preference-settings': // 处理子菜单项
      //   router.push('/settings/preferences');
      //   break;
    default:
      console.warn('未知的菜单项:', selectedItem);
  }
};
</script>

<style scoped>
/* 你可以为这个封装组件添加一些特定的样式，例如调整 z-index */
/* FloatMenu 组件自身可能已经有很高的 z-index */
/* :deep() 可以穿透到 FloatMenu 内部的样式，但要小心使用 */
/*
:deep(.f-menu-container) {
  z-index: 10000 !important; // 确保它在最上层
}
*/
</style>