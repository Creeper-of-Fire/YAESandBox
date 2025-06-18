<template>
  <n-config-provider :theme="lightTheme" class="app-container">
    <n-message-provider>
      <n-dialog-provider>
        <n-notification-provider>
          <div class="app-shell">
            <header class="app-header">
              <!-- 简单的导航 -->
              <router-link to="/game">游戏</router-link>
              <router-link to="/workbench">编辑器</router-link>
            </header>
            <main class="app-main-content">
              <router-view v-slot="{ Component }">
                <transition name="fade" mode="out-in">
                  <component :is="Component"/>
                </transition>
              </router-view>
            </main>
          </div>
        </n-notification-provider>
      </n-dialog-provider>
    </n-message-provider>
  </n-config-provider>
  <!-- 可能的全局元素，如全局错误弹窗 -->
  <!--  <GlobalErrorDisplay />-->
  <!--  <AppWideNotifications />-->
</template>

<script setup lang="ts">
import {onMounted, onUnmounted} from 'vue';
import {useConnectionStore} from '@/app-game/stores/connectionStore.ts';
import {lightTheme} from "naive-ui";
// import GlobalErrorDisplay from '@/components/GlobalErrorDisplay.vue';
// import AppWideNotifications from '@/components/AppWideNotifications.vue';

const connectionStore = useConnectionStore();

onMounted(async () => {
  console.log("App [onMounted]: 应用启动，初始化连接...");
  await connectionStore.connectSignalR();
  if (connectionStore.connectionError) {
    console.error("App [onMounted]: SignalR 初始连接失败。", connectionStore.connectionError);
    // 这里可以触发一个全局错误状态
  }
});

onUnmounted(() => {
  console.log("App [onUnmounted]: 应用关闭。");
  // connectionStore.disconnectSignalR(); // 如果有断开方法
});
</script>

<style>
/* 全局过渡动画等 */
.fade-enter-active, .fade-leave-active {
  transition: opacity 0.3s ease;
}

.fade-enter-from, .fade-leave-to {
  opacity: 0;
}
</style>