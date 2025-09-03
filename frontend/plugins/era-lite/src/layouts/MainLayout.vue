<!-- era-lite/src/layouts/MainLayout.vue -->
<template>
  <n-layout has-sider style="height: 100%">
    <n-layout-sider
        :collapsed="collapsed"
        :collapsed-width="64"
        :native-scrollbar="false"
        :width="240"
        bordered
        collapse-mode="width"
        show-trigger
        style="user-select: none"
        @collapse="collapsed = true"
        @expand="collapsed = false"
        @dblclick.prevent="collapsed = !collapsed"
    >
      <div class="logo">
        <Transition mode="out-in" name="fade">
          <!-- 如果未折叠，显示完整标题 -->
          <span v-if="!collapsed">ERA-LITE</span>
          <!-- 如果已折叠，显示游戏图标 -->
          <n-icon v-else :component="GameControllerIcon" :size="32"/>
        </Transition>
      </div>
      <n-menu
          :collapsed="collapsed"
          :collapsed-icon-size="22"
          :collapsed-width="64"
          :options="menuOptions"
          :value="route.name?.toString()"
      />
    </n-layout-sider>
    <n-layout>
      <div class="main-content">
        <router-view/>
      </div>
    </n-layout>
  </n-layout>
</template>

<script lang="ts" setup>
import {h, watch} from 'vue';
import {RouterLink, useRoute, useRouter} from 'vue-router';
import {NIcon, NLayout, NLayoutSider, NMenu, useThemeVars} from 'naive-ui';
import {BagIcon, ChatIcon, EarthIcon, HomeIcon, PeopleIcon, StorefrontIcon} from '#/utils/icon.ts';
import {GameControllerIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {useScopedStorage} from "@yaesandbox-frontend/core-services/composables";
import {useEraLiteGameMenu} from "#/features/home/useEraLiteGameMenu.ts";

const collapsed = useScopedStorage('mainLayout:collapsed', false);

const route = useRoute();

const router = useRouter();

const gameMenu = useEraLiteGameMenu();

// --- 路由守卫逻辑 ---
// 监听当前激活的存档名称。
watch(
    () => gameMenu.isGameLoaded.value,
    (activeScopeName) =>
    {
      // 如果没有激活的存档 (例如，用户点击了“返回主菜单”)
      if (!activeScopeName)
      {
        console.log('没有检测到激活的存档，正在重定向到启动页...');
        // 强制跳转回启动页
        router.push({name: 'Era_Lite_Startup'});
      }
    },
    {
      // 立即执行一次，确保在组件挂载时就进行检查
      immediate: true,
    }
);


const renderIcon = (icon: any) => () => h(NIcon, null, {default: () => h(icon)});

const menuOptions = [
  {
    label: () => h(RouterLink, {to: {name: 'Era_Lite_Home'}}, {default: () => '主菜单'}),
    key: 'Era_Lite_Home',
    icon: renderIcon(HomeIcon),
  },
  {
    label: () => h(RouterLink, {to: {name: 'Era_Lite_Chat_List'}}, {default: () => '对话记录'}),
    key: 'Era_Lite_Chat_List',
    icon: renderIcon(ChatIcon),
  },
  {
    label: () => h(RouterLink, {to: {name: 'Era_Lite_Characters'}}, {default: () => '角色列表'}),
    key: 'Era_Lite_Characters',
    icon: renderIcon(PeopleIcon),
  },
  {
    label: () => h(RouterLink, {to: {name: 'Era_Lite_Scenes'}}, {default: () => '场景列表'}),
    key: 'Era_Lite_Scenes',
    icon: renderIcon(EarthIcon),
  },
  {
    label: () => h(RouterLink, {to: {name: 'Era_Lite_Shop'}}, {default: () => '道具商店'}),
    key: 'Era_Lite_Shop',
    icon: renderIcon(StorefrontIcon),
  },
  {
    label: () => h(RouterLink, {to: {name: 'Era_Lite_Backpack'}}, {default: () => '我的背包'}),
    key: 'Era_Lite_Backpack',
    icon: renderIcon(BagIcon),
  },
];

defineOptions({
  name: 'era-lite:MainLayout',
})
const themeVars = useThemeVars();
</script>

<style scoped>
.logo {
  height: 60px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  font-weight: bold;
  color: v-bind('themeVars.primaryColor');
}

/* fade 过渡进入和离开的激活状态 */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s ease, transform 0.3s ease;
}

.main-content {
  padding: 24px;
  height: 100%;
  overflow-y: auto;
  box-sizing: border-box;
}
</style>