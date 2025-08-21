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
          <n-icon v-else :component="GameIcon" :size="32"/>
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
      <n-message-provider>
        <div class="main-content">
          <router-view/>
        </div>
      </n-message-provider>
    </n-layout>
  </n-layout>
</template>

<script lang="ts" setup>
import {h} from 'vue';
import {RouterLink, useRoute} from 'vue-router';
import {NIcon, NLayout, NLayoutSider, NMenu, NMessageProvider, useThemeVars} from 'naive-ui';
import {BagIcon, EarthIcon, GameIcon, HomeIcon, PeopleIcon, StorefrontIcon} from '#/utils/icon.ts';
import {useScopedStorage} from "@yaesandbox-frontend/core-services/composables";

const collapsed = useScopedStorage('collapsed', false);

const route = useRoute();

const renderIcon = (icon: any) => () => h(NIcon, null, {default: () => h(icon)});

const menuOptions = [
  {
    label: () => h(RouterLink, {to: {name: 'Home'}}, {default: () => '主菜单'}),
    key: 'Home',
    icon: renderIcon(HomeIcon),
  },
  {
    label: () => h(RouterLink, {to: {name: 'Characters'}}, {default: () => '角色列表'}),
    key: 'Characters',
    icon: renderIcon(PeopleIcon),
  },
  {
    label: () => h(RouterLink, {to: {name: 'Scenes'}}, {default: () => '场景列表'}),
    key: 'Scenes',
    icon: renderIcon(EarthIcon),
  },
  {
    label: () => h(RouterLink, {to: {name: 'Shop'}}, {default: () => '道具商店'}),
    key: 'Shop',
    icon: renderIcon(StorefrontIcon),
  },
  {
    label: () => h(RouterLink, {to: {name: 'Backpack'}}, {default: () => '我的背包'}),
    key: 'Backpack',
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