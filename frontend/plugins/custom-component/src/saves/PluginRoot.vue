<template>
  <!-- 当存档服务正在初始化和加载工作区时，显示一个加载界面 -->
  <div v-if="!isWorkspaceReady" class="loading-overlay">
    <n-spin size="large" />
    <p style="margin-top: 16px;">正在初始化组件工作区...</p>
  </div>
  <!-- 工作区加载完毕后，渲染真正的编辑器 UI -->
  <ComponentEditor v-else />
</template>

<script lang="ts" setup>
import { computed, onMounted } from 'vue';
import { NSpin } from 'naive-ui';
import { createAndProvideComponentSaveService } from '#/saves/useComponentSaveStore';
import ComponentEditor from '#/views/ComponentEditor.vue';

// 为我们的编辑器工作区定义一个固定的存档名称
const EDITOR_WORKSPACE_SLOT_NAME = 'component-editor-workspace';

// 1. 创建并 Provide 存档服务实例，供其下的所有子组件使用。
const saveService = createAndProvideComponentSaveService();

// 2. 定义一个计算属性，用于追踪我们的工作区是否已加载并激活。
const isWorkspaceReady = computed(() => saveService.activeSlot.value?.name === EDITOR_WORKSPACE_SLOT_NAME);

// 3. 当组件挂载时，执行一次性的初始化设置。
onMounted(async () => {
  // 如果已经就绪（例如在开发模式热重载时），则无需任何操作。
  if (isWorkspaceReady.value) {
    return;
  }

  // 初始化服务，获取所有存档槽位的列表。
  await saveService.initialize();

  // 查找我们指定的工作区存档。
  const existingWorkspace = saveService.slots.value.find(s => s.name === EDITOR_WORKSPACE_SLOT_NAME);

  if (existingWorkspace) {
    // 如果存在，就加载它。
    console.log(`发现已存在的工作区，正在加载...`);
    await saveService.loadGame(existingWorkspace.id);
  } else {
    // 如果不存在（首次使用），就创建一个新的。
    console.log(`未找到工作区，正在创建一个新的...`);
    await saveService.startNewGame(EDITOR_WORKSPACE_SLOT_NAME);
  }

  console.log("组件工作区已准备就绪。");
});
</script>

<style scoped>
.loading-overlay {
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  height: 100%;
  width: 100%;
}
</style>