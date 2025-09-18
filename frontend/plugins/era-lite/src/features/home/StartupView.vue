<!-- src/features/home/StartupView.vue -->
<template>
  <n-flex
      :size="24"
      align="center"
      justify="center"
      style="height: 100%;"
      vertical
  >
    <n-h1 style="font-size: 48px; margin-bottom: 48px;">Era-Lite</n-h1>
    <n-space size="large" style="width: 300px;" vertical>
      <n-button
          :disabled="!saveService.lastActiveSlotName.value"
          block
          size="large"
          type="primary"
          @click="handleContinue"
      >
        继续上次游戏
      </n-button>
      <n-button block size="large" @click="handleNewGame">
        开始新游戏
      </n-button>
      <n-button
          block
          size="large"
          @click="showSaveManager = true"
      >
        读取/管理存档
      </n-button>
    </n-space>

    <n-divider style="width: 300px; margin-top: 48px;">
      设置
    </n-divider>

    <n-flex align="center" justify="center" style="width: 300px;">
      <label for="auto-load-switch" style="cursor: pointer;">自动加载上次存档</label>
      <n-switch id="auto-load-switch" v-model:value="autoLoadEnabled"/>
      <n-popover trigger="hover">
        <template #trigger>
          <n-icon :component="HelpCircleIcon" style="cursor: help; color: #999; margin-left: 8px;"/>
        </template>
        <span>启用后，进入此页面将自动加载上次游玩的存档。</span>
      </n-popover>
    </n-flex>
  </n-flex>

  <n-modal v-model:show="showSaveManager" preset="card" style="width: 800px" title="存档管理">
    <!-- 在这里，我们复用已有的 SavePresenterCard 组件 -->
    <SavePresenterCard/>
  </n-modal>

  <!-- 新建游戏时的名称输入弹窗 -->
  <n-modal v-model:show="showNewGameModal" preset="dialog" title="创建新存档">
    <n-input
        v-model:value="newGameName"
        placeholder="请输入新存档的名称"
        @keydown.enter="confirmNewGame"
    />
    <template #action>
      <n-button @click="showNewGameModal = false">取消</n-button>
      <n-button type="primary" @click="confirmNewGame">确定</n-button>
    </template>
  </n-modal>
</template>

<script lang="ts" setup>
import {ref, watch} from 'vue';
import {NButton, NFlex, NH1, NInput, NModal, NSpace, useMessage} from 'naive-ui';
import {HelpCircleIcon} from "@yaesandbox-frontend/shared-ui/icons";
import {useScopedStorage} from "@yaesandbox-frontend/core-services/composables";
import {useGameSaveService} from "@yaesandbox-frontend/core-services/playerSave";
import SavePresenterCard from "#/share/SavePresenterCard.vue";

const message = useMessage();
const saveService = useGameSaveService();
const autoLoadEnabled = useScopedStorage('startup:auto-load-enabled', false);

const showSaveManager = ref(false);
const showNewGameModal = ref(false);
const newGameName = ref('新的冒险');

// TODO 这里有bug，子路由切换，有可能是创建createScopedPersistentState时，会触发
// 2. 实现自动加载的核心逻辑
watch(
    // 监听 saveService 是否初始化完成
    () => saveService.isInitialized.value,
    (isReady) =>
    {
      // 只有在初始化完成后才执行检查
      if (isReady)
      {
        // 检查开关是否开启，并且确实有上次的存档记录
        if (autoLoadEnabled.value && saveService.lastActiveSlotName.value)
        {
          message.loading('正在自动加载上次游戏...', {duration: 1500});
          // 延迟一小段时间给用户看清提示
          setTimeout(() =>
          {
            saveService.loadLastGame();
          }, 500);
        }
      }
    },
    // immediate: true 确保组件挂载后立即执行一次检查
    {immediate: true}
);

async function handleContinue()
{
  await saveService.loadLastGame();
}

function handleNewGame()
{
  showNewGameModal.value = true;
}

async function confirmNewGame()
{
  if (!newGameName.value.trim())
  {
    message.error('存档名不能为空');
    return;
  }
  await saveService.startNewGame(newGameName.value);
  showNewGameModal.value = false;
}
</script>