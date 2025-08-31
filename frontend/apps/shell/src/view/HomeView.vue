<template>
  <div class="home-view">
    <n-card title="欢迎使用 YAESandBox" hoverable>
      <n-space vertical size="large">
        <p>
          这是一个灵活的工作流与 AI 服务编排平台。
        </p>
        <p>
          你可以从顶部的导航栏开始探索不同的功能模块。
        </p>

        <n-divider />

        <n-button type="primary" secondary @click="goToAbout">
          关于本应用 (About)
        </n-button>
        <!-- 未来还可以添加更多链接，比如设置、帮助文档等 -->
      </n-space>
    </n-card>
  </div>
</template>

<script lang="ts" setup>
import { useRouter } from 'vue-router';

const router = useRouter();

const goToAbout = () => {
  router.push({ name: 'App_Shell_HomeView_About' });
};
</script>

<style scoped>
.home-view {
  display: flex;
  justify-content: center;
  align-items: center;
  padding: 40px;
  height: 100%;
}
</style>