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

<script lang="tsx" setup>
import {RouterLink, useRoute} from 'vue-router';
import {type MenuOption, NIcon, NLayout, NLayoutSider, NMenu, useThemeVars} from 'naive-ui';
import {BagIcon, ChatIcon, EarthIcon, HomeIcon, PeopleIcon, StorefrontIcon} from '#/utils/icon.ts';
import {GameControllerIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {useScopedStorage} from "@yaesandbox-frontend/core-services/composables";
import type {Component} from "vue";

const collapsed = useScopedStorage('mainLayout:collapsed', false);

const route = useRoute();

interface NavLink
{
  label: string;
  key: string; // key 和 route name 我们约定为同一个值
  icon: Component;
}

const navLinks: NavLink[] = [
  {
    label: '主菜单',
    key: 'Era_Lite_Home',
    icon: HomeIcon
  },
  {
    label: '对话记录',
    key: 'Era_Lite_Chat_List',
    icon: ChatIcon
  },
  {
    label: '角色列表',
    key: 'Era_Lite_Characters',
    icon: PeopleIcon
  },
  {
    label: '场景列表',
    key: 'Era_Lite_Scenes',
    icon: EarthIcon
  },
  {
    label: '道具商店',
    key: 'Era_Lite_Shop',
    icon: StorefrontIcon
  },
  {
    label: '我的背包',
    key: 'Era_Lite_Backpack',
    icon: BagIcon
  },
];

function processNavLinksToMenuOptions(links: NavLink[]): MenuOption[]
{
  return links.map(link =>
  {
    return {
      key: link.key,
      label: () => (
          <RouterLink to={{name: link.key}}>{link.label}</RouterLink>
      ),
      icon: () => (
          <NIcon component={link.icon}/>
      ),
    };
  });
}

const menuOptions = processNavLinksToMenuOptions(navLinks);

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