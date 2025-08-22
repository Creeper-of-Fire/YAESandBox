<template>
  <div class="about-view">
    <n-card title="关于 YAESandBox" hoverable>
      <n-button text @click="$router.back()" style="margin-bottom: 20px;">
        <template #icon>
          <n-icon><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1024 1024"><path fill="currentColor" d="M224 480h640a32 32 0 1 1 0 64H224a32 32 0 0 1 0-64z"></path><path fill="currentColor" d="m237.248 512l265.408 265.344a32 32 0 0 1-45.312 45.312l-288-288a32 32 0 0 1 0-45.312l288-288a32 32 0 1 1 45.312 45.312L237.248 512z"></path></svg></n-icon>
        </template>
        返回
      </n-button>

      <!-- default-value 设置默认打开的 Tab -->
      <n-tabs type="line" animated default-value="project-license">
        <n-tab-pane name="project-license" tab="项目许可证">
          <n-spin :show="loadingProject">
            <n-log :log="projectLicense" language="text" trim />
          </n-spin>
        </n-tab-pane>

        <n-tab-pane name="frontend" tab="前端开源库">
          <n-spin :show="loadingFe">
            <n-log :log="frontendLicenses" language="text" trim />
          </n-spin>
        </n-tab-pane>

        <n-tab-pane name="backend" tab="后端开源库">
          <n-spin :show="loadingBe">
            <n-log :log="backendLicenses" language="json" trim />
          </n-spin>
        </n-tab-pane>
      </n-tabs>
    </n-card>
  </div>
</template>

<script lang="ts" setup>
import { ref, onMounted } from 'vue';

const projectLicense = ref('');
const frontendLicenses = ref('');
const backendLicenses = ref('');

const loadingProject = ref(true);
const loadingFe = ref(true);
const loadingBe = ref(true);

// 封装一个获取函数，避免重复代码
const fetchLicense = async (url: string, targetRef: typeof projectLicense, loaderRef: typeof loadingProject, errorMessage: string) => {
  try {
    const response = await fetch(url);
    if (!response.ok) throw new Error(`Failed to fetch ${url}`);
    targetRef.value = await response.text();
  } catch (error) {
    console.error(error);
    targetRef.value = errorMessage;
  } finally {
    loaderRef.value = false;
  }
};

onMounted(async () => {
  // 并行获取所有许可证文件，速度更快
  await Promise.all([
    fetchLicense(
        '/LICENSE.txt',
        projectLicense,
        loadingProject,
        '无法加载项目许可证文件。'
    ),
    fetchLicense(
        '/THIRD_PARTY_LICENSES.txt',
        frontendLicenses,
        loadingFe,
        '无法加载前端许可证文件。'
    ),
    // 对于 JSON 文件，我们需要稍作修改
    (async () => {
      try {
        const beResponse = await fetch('/licenses-backend.json');
        if (!beResponse.ok) throw new Error('Failed to fetch backend licenses');
        const beJson = await beResponse.json();
        backendLicenses.value = JSON.stringify(beJson, null, 2);
      } catch (error) {
        console.error(error);
        backendLicenses.value = '无法加载后端许可证文件。';
      } finally {
        loadingBe.value = false;
      }
    })()
  ]);
});
</script>

<style scoped>
.about-view {
  padding: 40px;
}
.n-log {
  height: 60vh;
}
</style>