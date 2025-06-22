<template>
  <div class="panel-container settings-panel">
    <n-h4 prefix="bar">应用设置</n-h4>
    <n-divider/>
    <!-- TODO: 在这里实现各种应用设置 -->
    <n-empty description="设置面板开发中..." style="margin-top: 20px;">
      <template #icon>
        <n-icon :component="SettingsIcon"/>
      </template>
    </n-empty>

    <n-h5>API 设置 (示例)</n-h5>
    <n-form>
      <n-form-item label="API Key">
        <n-input type="password" show-password-on="click" placeholder="输入你的 API Key"/>
      </n-form-item>
      <n-form-item label="API Endpoint">
        <n-input placeholder="输入 API 地址"/>
      </n-form-item>
    </n-form>

    <n-h5>界面设置 (示例)</n-h5>
    <n-form>
      <!--      <n-form-item label="左侧面板宽度">-->
      <!--        <n-input-number v-model:value="uiStore.leftPanelWidth" :min="200" :max="800" :step="10" />-->
      <!--      </n-form-item>-->
      <!--      <n-form-item label="右侧面板宽度">-->
      <!--        <n-input-number v-model:value="uiStore.rightPanelWidth" :min="200" :max="800" :step="10" />-->
      <!--      </n-form-item>-->
    </n-form>

  </div>
</template>

<script setup lang="ts">
import {NH4, NDivider, NEmpty, NIcon, NH5, NForm, NFormItem, NInput} from 'naive-ui';
import {SettingsOutline as SettingsIcon} from '@vicons/ionicons5';
import {useUiStore} from '@/app-game/stores/gameUiStore.ts'; // 引入 UI Store 修改面板宽度

const uiStore = useUiStore();

// TODO:
// 1. 定义需要的设置项 (如 API keys, 模型参数, UI 偏好等)
// 2. 使用 Naive UI 的表单组件创建设置界面
// 3. 将设置值存储到 Pinia Store (可能需要新的 settingsStore) 或 localStorage
// 4. 实现设置的加载和保存逻辑

</script>

<style scoped>
.panel-container {
  padding: 5px;
}

.n-h4, .n-h5 {
  margin-bottom: 10px;
}

.n-divider {
  margin-bottom: 15px;
}
</style>