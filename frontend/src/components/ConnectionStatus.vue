<template>
  <!-- 将点击事件绑定到 Tooltip 的触发器上 -->
  <n-tooltip trigger="hover">
    <template #trigger>
      <div
          class="status-indicator-wrapper"
          @click="attemptReconnect"
          :class="{ clickable: canAttemptReconnect }"
          role="button"
          :aria-label="statusText"
      >
        <div class="status-indicator" :class="statusClass"></div>
      </div>
    </template>
    {{ statusText }}
  </n-tooltip>
</template>

<script setup lang="ts">
import {computed} from 'vue';
import {useConnectionStore} from '@/stores/connectionStore';
import {NTooltip, useNotification} from 'naive-ui'; // 引入 useNotification

const connectionStore = useConnectionStore();
const notification = useNotification(); // 获取 notification API

const statusClass = computed(() => {
  if (connectionStore.isSignalRConnecting) return 'connecting';
  if (connectionStore.isSignalRConnected) return 'connected';
  return 'disconnected';
});

const statusText = computed(() => {
  if (connectionStore.isSignalRConnecting) return '连接中...';
  if (connectionStore.isSignalRConnected) return 'SignalR 已连接';
  return 'SignalR 已断开 (点击重连)'; // 修改提示文本
});

// 计算是否可以尝试重连 (未连接且不处于连接中状态)
const canAttemptReconnect = computed(() => !connectionStore.isSignalRConnected && !connectionStore.isSignalRConnecting);

// 尝试重新连接的方法
const attemptReconnect = async () => {
  if (!canAttemptReconnect.value) {
    return; // 如果已连接或正在连接，则不执行任何操作
  }

  console.log("ConnectionStatus: 用户点击尝试重新连接...");
  // notification.info({ // 显示通知
  //   title: 'SignalR 连接',
  //   content: '正在尝试重新连接...',
  //   duration: 3000 // 通知显示时间 (ms)
  // });

  try {
    // 调用 store 中的连接方法
    await connectionStore.connectSignalR();
    // 连接成功/失败的状态更新由 connectSignalR 内部处理
    // 可以在这里根据结果再显示成功/失败通知，但 connectSignalR 内部可能已有日志
    if (connectionStore.isSignalRConnected) {
      notification.success({
        title: 'SignalR 连接',
        content: '重新连接成功！',
        duration: 2500
      });
    } else {
      // 如果 connectSignalR 内部处理了错误且没有抛出，这里可能不知道失败
      // 假设如果没连接成功就是失败了（需要看 connectSignalR 的具体实现）
      // notification.error({
      //     title: 'SignalR 连接',
      //     content: '重新连接失败，请检查网络或服务器状态。',
      //     duration: 5000
      // });
    }
  } catch (error) {
    console.error("ConnectionStatus: 尝试重新连接失败:", error);
    notification.error({ // 显示错误通知
      title: 'SignalR 连接错误',
      content: `重新连接失败: ${error instanceof Error ? error.message : '未知错误'}`,
      duration: 5000
    });
  }
};
</script>

<style scoped>
/* 添加一个包装器以便更好地处理点击区域和样式 */
.status-indicator-wrapper {
  display: inline-flex; /* 让内部指示器居中 */
  align-items: center;
  justify-content: center;
  padding: 5px; /* 增大点击区域 */
  border-radius: 50%;
  cursor: default; /* 默认光标 */
  transition: background-color 0.2s;
}

.status-indicator-wrapper.clickable {
  cursor: pointer; /* 只有可点击时才显示指针 */
}

.status-indicator-wrapper.clickable:hover {
  background-color: rgba(0, 0, 0, 0.05); /* 点击时给一点反馈 */
}


.status-indicator {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  transition: background-color 0.3s ease;
  /* margin-left: 10px; */ /* 边距移到 wrapper 上 */
  border: 1px solid rgba(0, 0, 0, 0.1);
}

.status-indicator.connected {
  background-color: #66bb6a; /* 绿色 */
}

.status-indicator.disconnected {
  background-color: #ef5350; /* 红色 */
}

.status-indicator.connecting {
  background-color: #ffa726; /* 橙色 */
  animation: pulse 1.5s infinite ease-in-out;
}

@keyframes pulse {
  0%, 100% {
    opacity: 1;
  }
  50% {
    opacity: 0.5;
  }
}
</style>