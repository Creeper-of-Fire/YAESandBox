<!-- src/app-authentication/LoginView.vue -->
<template>
  <div class="auth-container">
    <n-card :title="isLoginMode ? '欢迎登录' : '创建账户'" class="auth-card">
      <n-form @submit.prevent="handleSubmit">
        <n-form-item-row label="用户名">
          <n-input v-model:value="username" placeholder="请输入用户名" />
        </n-form-item-row>
        <n-form-item-row label="密码">
          <n-input
              v-model:value="password"
              type="password"
              show-password-on="mousedown"
              placeholder="请输入密码"
          />
        </n-form-item-row>
        <!-- 仅在注册模式下显示确认密码 -->
        <n-form-item-row v-if="!isLoginMode" label="确认密码">
          <n-input
              v-model:value="confirmPassword"
              type="password"
              show-password-on="mousedown"
              placeholder="请再次输入密码"
          />
        </n-form-item-row>
        <n-button type="primary" block attr-type="submit" :loading="isLoading">
          {{ isLoginMode ? '登录' : '注册' }}
        </n-button>
      </n-form>
      <template #footer>
        <div class="switch-mode">
          <span>{{ isLoginMode ? '还没有账户？' : '已有账户？' }}</span>
          <n-button text type="primary" @click="toggleMode">
            {{ isLoginMode ? '立即注册' : '立即登录' }}
          </n-button>
        </div>
      </template>
    </n-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { NCard, NForm, NFormItemRow, NInput, NButton, NAlert, useMessage } from 'naive-ui';
import {useAuthStore} from "#/app-authentication/stores/authStore.ts";

const isLoginMode = ref(true);
const username = ref('');
const password = ref('');
const confirmPassword = ref('');
const isLoading = ref(false);

const authStore = useAuthStore();
const message = useMessage(); // 用于成功提示

const toggleMode = () => {
  isLoginMode.value = !isLoginMode.value;
  // 清空表单和错误信息
  username.value = '';
  password.value = '';
  confirmPassword.value = '';
};

const handleSubmit = async () => {
  if (!username.value || !password.value) {
    message.error('用户名和密码不能为空！');
    return;
  }

  isLoading.value = true;
  if (isLoginMode.value) {
    // 登录逻辑
    await authStore.login({ username: username.value, password: password.value });
  } else {
    // 注册逻辑
    if (password.value !== confirmPassword.value) {
      message.error('两次输入的密码不一致！');
      isLoading.value = false;
      return;
    }
    const success = await authStore.register({ username: username.value, password: password.value });
    if (success) {
      message.success('注册成功！现在可以登录了。');
      toggleMode(); // 注册成功后切换回登录模式
    }
  }
  isLoading.value = false;
};
</script>

<style scoped>
.auth-container {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 100vh;
}
.auth-card {
  width: 400px;
}
.error-alert {
  margin-top: 15px;
}
.switch-mode {
  text-align: center;
}
.switch-mode span {
  margin-right: 8px;
}
</style>