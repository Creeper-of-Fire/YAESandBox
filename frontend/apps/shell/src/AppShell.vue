<template>
  <div class="app-shell">
    <header class="app-header">
      <n-space class="navigation-controls">
        <router-link
            v-for="link in navLinks"
            :key="link.to"
            v-slot="{ navigate, isActive }"
            :to="link.to"
            custom
        >
          <n-button
              :ghost="!isActive"
              :type="isActive ? 'primary' : 'default'"
              strong
              @click="navigate"
          >
            {{ link.label }}
          </n-button>
        </router-link>
      </n-space>

      <!-- 用户状态和登出按钮 -->
      <n-space align="center" class="user-controls">
        <DayNightToggleWithDropDown v-model:themeMode="themeMode"/>
        <span v-if="authStore.isAuthenticated">
          欢迎, {{ userName }}
        </span>
        <n-button v-if="authStore.isAuthenticated" ghost type="error" @click="handleLogout">
          登出
        </n-button>
      </n-space>
    </header>

    <main class="app-main-content">
      <!--
        修复后的 router-view：
        - 从 v-slot 中额外获取 route 对象。
        - 为 <component> 绑定了唯一的 :key="route.path"。
        - 当路由从 /game 切换到 /workbench 时, key 会发生变化，
          Vue 会强制销毁旧组件、创建新组件，从而避免白屏问题。
      -->
      <router-view v-slot="{ Component, route }">
        <!--                transition 调了半天还是不舒服，扔了得了，美化是没完没了的-->
        <!--                <transition name="fade" mode="in-out">-->
        <keep-alive>
          <component :is="Component" :key="route.path"/>
        </keep-alive>
        <!--                </transition>-->
      </router-view>
    </main>
  </div>
</template>

<script lang="ts" setup>
import {useThemeVars} from "naive-ui";
import {useAuthStore} from "#/app-authentication/stores/authStore.ts";
import {computed, inject} from "vue";
import DayNightToggleWithDropDown from "#/component/DayNightToggleWithDropDown.vue";
import type {PluginModule} from "@yaesandbox-frontend/core-services";
// import GlobalErrorDisplay from '#/components/GlobalErrorDisplay.vue';
// import AppWideNotifications from '#/components/AppWideNotifications.vue';

// 注入由 main.ts 提供的插件元数据
const loadedPlugins = inject<PluginModule['meta'][]>('loadedPlugins', []);

// 计算出需要显示在导航栏的插件，并排序
const navLinks = computed(() =>
{
  return loadedPlugins
      .filter(meta => meta.navEntry) // 只选择有 navEntry 的插件
      .sort((a, b) => (a.navEntry!.order ?? 99) - (b.navEntry!.order ?? 99)) // 排序
      .map(meta => ({
        to: `/${meta.name}`, // 约定路由路径和插件名一致
        label: meta.navEntry!.label,
      }));
});

const themeMode = defineModel<'light' | 'dark' | 'system'>({default: 'system'});

const authStore = useAuthStore();
const themeVars = useThemeVars();

const userName = computed(() =>
{
  return authStore.user?.username;
})


// const connectionStore = useConnectionStore();

// onMounted(async () =>
// {
//   console.log("App [onMounted]: 应用启动，初始化连接...");
//   await connectionStore.connectSignalR();
//   if (connectionStore.connectionError)
//   {
//     console.error("App [onMounted]: SignalR 初始连接失败。", connectionStore.connectionError);
//     // 这里可以触发一个全局错误状态
//   }
// });
//
// onUnmounted(() =>
// {
//   console.log("App [onUnmounted]: 应用关闭。");
//   // connectionStore.disconnectSignalR(); // 如果有断开方法
// });

const handleLogout = () =>
{
  authStore.logout();
};

const backgroundColor = computed(() => themeVars.value.baseColor);
const cardColor = computed(() => themeVars.value.cardColor);
const borderColor = computed(() => themeVars.value.borderColor);
const textColor = computed(() => themeVars.value.textColor1);
</script>

<style>
/* 整体应用布局 */
.app-shell {
  display: flex;
  flex-direction: column;
  height: 100vh;
  width: 100vw;
  background-color: v-bind(backgroundColor);
  color: v-bind(textColor);
}

/* 头部样式 */
.app-header {
  display: flex;
  align-items: center;
  padding: 1px 1px;
  justify-content: space-between;
  flex-shrink: 0; /* 防止头部被压缩 */
  border-bottom: 1px solid v-bind(borderColor);
  background-color: v-bind(cardColor);
}

.navigation-controls {
  margin-left: 20px;
}

/* 主内容区域样式 */
.app-main-content {
  flex-grow: 1; /* 占据剩余所有空间 */
  overflow-y: auto; /* 如果内容超长，则内部滚动 */
  box-sizing: border-box;
}

.user-controls {
  margin-right: 20px;
}

/* 定义淡入淡出过渡效果 */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.1s ease, transform 0.1s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
  transform: translateY(5px);
}
</style>