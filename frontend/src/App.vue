<template>
  <n-config-provider :theme="lightTheme" class="app-container">
    <n-message-provider>
      <n-dialog-provider>
        <n-notification-provider>
          <div class="app-shell">
            <header class="app-header">
              <!--
                美化后的导航栏：
                - 使用 n-space 来提供合适的间距。
                - 使用 router-link 的 custom 属性，将导航功能赋予 n-button。
                - v-slot="{ navigate, isActive }" 获取路由状态。
                - @click="navigate" 将按钮的点击事件绑定到路由跳转。
                - :type="isActive ? 'primary' : 'default'" 根据路由是否激活来改变按钮样式，提供视觉反馈。
              -->
              <n-space class="navigation-controls">
<!--                <router-link v-slot="{ navigate, isActive }" custom to="/game">-->
<!--                  <n-button-->
<!--                      :ghost="!isActive"-->
<!--                      :type="isActive ? 'primary' : 'default'"-->
<!--                      strong-->
<!--                      @click="navigate"-->
<!--                  >-->
<!--                    游戏-->
<!--                  </n-button>-->
<!--                </router-link>-->
                <router-link v-slot="{ navigate, isActive }" custom to="/workbench">
                  <n-button
                      :ghost="!isActive"
                      :type="isActive ? 'primary' : 'default'"
                      strong
                      @click="navigate"
                  >
                    编辑器
                  </n-button>
                </router-link>
                <router-link v-slot="{ navigate, isActive }" custom to="/test">
                  <n-button
                      :ghost="!isActive"
                      :type="isActive ? 'primary' : 'default'"
                      strong
                      @click="navigate"
                  >
                    测试台
                  </n-button>
                </router-link>
                <router-link v-slot="{ navigate, isActive }" custom to="/dialog">
                  <n-button
                      :ghost="!isActive"
                      :type="isActive ? 'primary' : 'default'"
                      strong
                      @click="navigate"
                  >
                    聊天
                  </n-button>
                </router-link>
              </n-space>

              <!-- 新增：用户状态和登出按钮 -->
              <n-space align="center" class="user-controls">
                <span v-if="authStore.isAuthenticated">
                  欢迎, {{ authStore.user?.username }}
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
                  <component :is="Component" :key="route.path"/>
                <!--                </transition>-->
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

<script lang="ts" setup>
import {onMounted, onUnmounted} from 'vue';
import {useConnectionStore} from '@/app-game/stores/connectionStore.ts';
import {lightTheme} from "naive-ui";
import {useAuthStore} from "@/app-authentication/stores/authStore.ts";
// import GlobalErrorDisplay from '@/components/GlobalErrorDisplay.vue';
// import AppWideNotifications from '@/components/AppWideNotifications.vue';
const authStore = useAuthStore();
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

const handleLogout = () => {
  authStore.logout();
};
</script>

<style>
/* 整体应用布局 */
.app-shell {
  display: flex;
  flex-direction: column;
  height: 100vh;
  width: 100vw;
  background-color: #f5f7f9;
}

/* 头部样式 */
.app-header {
  display: flex;
  align-items: center;
  padding: 1px 1px;
  justify-content: space-between;
  border-bottom: 1px solid #e8e8e8;
  background-color: #ffffff;
  flex-shrink: 0; /* 防止头部被压缩 */
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