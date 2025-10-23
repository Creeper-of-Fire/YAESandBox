<!-- StartupView.vue -->
<template>
  <div>
    <n-flex
        :size="24"
        align="center"
        justify="center"
        style="height: 100%;"
        vertical
    >
      <n-h1 style="font-size: 48px; margin-bottom: 48px;">{{ appTitle }}</n-h1>
      <n-space size="large" style="width: 300px;" vertical>
        <n-button
            :disabled="!canContinue"
            block
            size="large"
            type="primary"
            @click="handleContinue"
        >
          继续上次游戏 ({{ saveService.lastActiveSlotName.value || '无记录' }})
        </n-button>
        <n-button block size="large" @click="handleNewGame">
          开始新游戏
        </n-button>
        <n-button
            block
            size="large"
            @click="handleShowSaveManager"
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
  </div>
</template>

<script lang="ts" setup>
import {NButton, NFlex, NH1, NInput, NModal, NSpace} from 'naive-ui';
import {HelpCircleIcon} from "@yaesandbox-frontend/shared-ui/icons";
import SavePresenterCard from "./SavePresenterCard.vue";
import {useStartupLogic} from "@yaesandbox-frontend/core-services/player-save";

defineProps<{
  appTitle: string;
}>();

const {
  autoLoadEnabled,
  showSaveManager,
  showNewGameModal,
  newGameName,
  canContinue,
  handleContinue,
  handleNewGame,
  handleShowSaveManager,
  confirmNewGame,
  saveService,
} = useStartupLogic();
</script>