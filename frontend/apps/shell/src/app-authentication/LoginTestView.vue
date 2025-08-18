<!-- src/app-authentication/LoginTestView.vue -->
<template>
  <div class="auth-container">
    <n-card title="测试登录/注册" class="auth-card">
      <template #header-extra>
        <n-tag type="warning">测试专用</n-tag>
      </template>
      <n-alert title="安全提示" type="info" :bordered="false" class="info-alert">
        此页面为测试专用，将使用您输入的用户名和统一的默认密码 "<strong>password</strong>" 进行登录或注册，以保护您的隐私安全。
      </n-alert>
      <n-form @submit.prevent="handleSubmit">
        <n-form-item-row label="测试用户名">
          <n-input v-model:value="username" placeholder="请输入一个测试用户名" />
        </n-form-item-row>
        <n-button type="primary" block attr-type="submit" :loading="isLoading">
          登录或注册
        </n-button>
        <n-alert v-if="authStore.authError" title="操作失败" type="error" class="error-alert">
          {{ authStore.authError }}
        </n-alert>
      </n-form>
      <template #footer>
        <div class="footer-info">
          遇到问题？请联系测试管理员。
        </div>
      </template>
    </n-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { useAuthStore } from '@/app-authentication/stores/authStore';
import { NCard, NForm, NFormItemRow, NInput, NButton, NAlert, NTag, useMessage } from 'naive-ui';

const username = ref('');
const isLoading = ref(false);

const authStore = useAuthStore();
const message = useMessage();

// 固定的测试密码
const FIXED_PASSWORD = 'password';

const handleSubmit = async () => {
  if (!username.value) {
    message.error('请输入一个测试用户名！');
    return;
  }

  isLoading.value = true;
  authStore.authError = null;

  // 步骤 1: 尝试登录
  const loginSuccess = await authStore.login({ username: username.value, password: FIXED_PASSWORD });

  if (!loginSuccess) {
    // 登录失败，尝试注册
    console.log("Login attempt failed, attempting to register...");
    const registerSuccess = await authStore.register({ username: username.value, password: FIXED_PASSWORD });

    if (registerSuccess) {
      message.success('新测试用户注册成功！正在自动登录...');
      // 注册成功后再次尝试登录
      await authStore.login({ username: username.value, password: FIXED_PASSWORD });
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
  width: 450px;
}
.info-alert {
  margin-bottom: 20px;
}
.error-alert {
  margin-top: 15px;
}
.footer-info {
  text-align: center;
}
</style>