<!-- src/app-authentication/LoginTestView.vue -->
<template>
  <div class="auth-container">
    <n-card class="auth-card" title="测试登录/注册">
      <template #header-extra>
        <n-tag type="warning">测试专用</n-tag>
      </template>
      <n-alert :bordered="false" class="info-alert" title="安全提示" type="info">
        此页面为测试专用，将使用您输入的用户名和统一的默认密码 "<strong>password</strong>" 进行登录或注册，以保护您的隐私安全。
      </n-alert>
      <n-form @submit.prevent="handleSubmit">
        <n-form-item-row label="测试用户名">
          <n-input v-model:value="username" placeholder="请输入一个测试用户名"/>
        </n-form-item-row>
        <n-button :loading="isLoading" attr-type="submit" block type="primary">
          登录或注册
        </n-button>
      </n-form>
      <template #footer>
        <div class="footer-info">
          遇到问题？请联系测试管理员。
        </div>
      </template>
    </n-card>
  </div>
</template>

<script lang="ts" setup>
import {ref} from 'vue';
import {NAlert, NButton, NCard, NForm, NFormItemRow, NInput, NTag, useMessage} from 'naive-ui';
import {useAuthNavigation} from "#/app-authentication/composables/useAuthNavigation.ts";

const username = ref('');
const isLoading = ref(false);

const message = useMessage();
const {loginOrRegisterAndRedirect} = useAuthNavigation();

// 固定的测试密码
const FIXED_PASSWORD = 'password';

const handleSubmit = async () =>
{
  if (!username.value)
  {
    message.error('请输入一个测试用户名！');
    return;
  }

  isLoading.value = true;

  try
  {
    // 只需调用一个函数，它会处理所有复杂的逻辑
    await loginOrRegisterAndRedirect({
      username: username.value,
      password: FIXED_PASSWORD,
    });
  } finally
  {
    isLoading.value = false;
  }
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