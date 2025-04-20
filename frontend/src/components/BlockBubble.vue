<template>
  <div
      class="block-bubble-container"
      :class="{
      'is-loading': isLoading && !streamingContent, // 初始加载时
      'is-streaming': !!streamingContent,           // 正在流式输出时
      'is-error': isError,
      'is-resolving-conflict': isResolvingConflict,
      'is-current-leaf': isCurrentLeaf // 可以给当前路径的最后一个节点加点特殊样式
    }"
  >
    <!-- 调试信息 (可选) -->
    <div class="debug-info">
      Block ID: {{ blockId }} | Status: {{ status || 'N/A' }} | Parent: {{ parentBlockId || 'None' }}
    </div>

    <!-- 兄弟节点分页器 (SiblingPager) -->
    <div v-if="totalSiblings > 1" class="sibling-pager">
      <button @click="pageLeft" :disabled="!canPageLeft || narrativeStore.isLoadingAction"><</button>
      <span>分支 {{ currentSiblingIndex + 1 }}/{{ totalSiblings }}</span>
      <button @click="pageRight" :disabled="!canPageRight || narrativeStore.isLoadingAction">></button>
    </div>

    <!-- 主要内容区域 -->
    <div class="block-content">
      <!-- 加载状态 (初始加载，非流式) -->
      <div v-if="isLoading && !streamingContent" class="loading-indicator">
        <p>正在加载 Block 内容...</p>
      </div>

      <!-- 错误状态 -->
      <div v-else-if="isError" class="error-message">
        <p>加载此 Block 时出错。</p>
        <!-- 可以添加重试按钮 -->
        <button @click="regenerate" :disabled="narrativeStore.isLoadingAction">尝试重新生成</button>
      </div>

      <!-- 冲突状态 -->
      <div v-else-if="isResolvingConflict" class="conflict-message">
        <p><strong>存在冲突!</strong></p>
        <p>请在侧边栏或弹窗中解决冲突。</p>
        <!-- 可以显示一个简化版的冲突信息或链接 -->
        <button @click="viewConflictDetails" :disabled="!activeConflict">查看冲突详情</button>
      </div>

      <!-- 流式内容 -->
      <div v-else-if="streamingContent" class="streaming-content">
        <pre>{{ streamingContent }}</pre>
        <span class="streaming-indicator">▋</span> <!-- 模拟光标 -->
      </div>

      <!-- 正常内容 -->
      <div v-else-if="block" class="final-content">
        <!-- 使用 pre 标签保留换行和空格 -->
        <pre>{{ displayContent }}</pre>
      </div>

      <!-- Block 数据尚未加载 -->
      <div v-else class="loading-placeholder">
        <p>等待 Block 数据...</p>
      </div>
    </div>

    <!-- 交互操作区域 -->
    <div class="block-actions">
      <!-- 触发生成下一个 Block /* 只有 Idle 状态可以生成下一个 */ -->
      <button
          @click="generateNext"
          :disabled="isLoading || isError || isResolvingConflict || narrativeStore.isLoadingAction || !block"
          v-if="status === 'Idle'" 
      class="generate-next-button"
      >
      生成下一个
      </button>

      <!-- 其他操作 (可选) /* Idle 或 Error 状态可以重新生成 */ -->
      <button
          @click="regenerate"
          :disabled="isLoading || isResolvingConflict || narrativeStore.isLoadingAction || !block"
          v-if="status === 'Idle' || status === 'Error'"
      class="action-button"
      title="重新生成当前 Block 内容"
      >
      🔄 重新生成
      </button>
      <!-- /* 允许删除 Idle 或 Error 状态的 */-->
      <button
          @click="deleteThisBlock"
          :disabled="isLoading || isResolvingConflict || narrativeStore.isLoadingAction || !block || blockId === narrativeStore.rootBlockId"
          v-if="status === 'Idle' || status === 'Error'"
      class="action-button delete-button"
      title="删除当前 Block (及其子节点)"
      >
      🗑️ 删除
      </button>

      <!-- 也可以在这里放编辑按钮，打开对应的面板 -->
      <!-- <button @click="openEditorPanel">编辑</button> -->
    </div>

  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useNarrativeStore } from '@/stores/narrativeStore';
import { BlockStatusCode } from '@/types/generated/api.ts'; // 引入 Enum
import type { BlockDetailDto, ConflictDetectedDto } from '@/types/generated/api.ts'; // 引入类型

const props = defineProps<{
  blockId: string;
}>();

const narrativeStore = useNarrativeStore();

// --- Computed Properties ---

/** 获取当前 Block 的详细数据 */
const block = computed<BlockDetailDto | undefined>(() => narrativeStore.getBlockById(props.blockId));

/** 获取当前 Block 的状态码 */
const status = computed<BlockStatusCode | undefined>(() => narrativeStore.getBlockStatus(props.blockId));

/** 获取当前 Block 的流式内容 */
const streamingContent = computed<string | undefined>(() => narrativeStore.getStreamingContent(props.blockId));

/** 是否处于加载状态 */
const isLoading = computed(() => status.value === BlockStatusCode.LOADING);

/** 是否处于错误状态 */
const isError = computed(() => status.value === BlockStatusCode.ERROR);

/** 是否处于冲突解决状态 */
const isResolvingConflict = computed(() => status.value === BlockStatusCode.RESOLVING_CONFLICT);

/** 获取父 Block ID */
const parentBlockId = computed(() => block.value?.parentBlockId);

/** 获取所有兄弟节点 ID (包括自身) */
const siblings = computed(() => {
  if (!block.value) return []; // 如果 block 数据还没加载，返回空
  return narrativeStore.getSiblingIdsOf(props.blockId);
});

/** 当前 Block 在兄弟节点中的索引 */
const currentSiblingIndex = computed(() => siblings.value.findIndex(id => id === props.blockId));

/** 兄弟节点的总数 */
const totalSiblings = computed(() => siblings.value.length);

/** 是否可以向左翻页 */
const canPageLeft = computed(() => currentSiblingIndex.value > 0);

/** 是否可以向右翻页 */
const canPageRight = computed(() => currentSiblingIndex.value < totalSiblings.value - 1);

/** 获取最终要显示的内容 (非流式) */
const displayContent = computed(() => {
  // TODO: 实现更复杂的渲染逻辑 (Markdown, 格式块等)
  // 目前仅返回原始 content
  return block.value?.blockContent ?? "";
});

/** 检查此 Block 是否是当前路径的叶节点 */
const isCurrentLeaf = computed(() => narrativeStore.currentPathLeafId === props.blockId);

/** 获取当前激活的冲突 */
const activeConflict = computed<ConflictDetectedDto | null>(() => narrativeStore.getActiveConflict);


// --- Methods ---

/** 触发生成下一个 Block */
const generateNext = () => {
  if (!block.value || narrativeStore.isLoadingAction || status.value !== BlockStatusCode.IDLE) return;
  console.log(`BlockBubble: 请求在 ${props.blockId} 下生成下一个 Block`);
  // 使用默认工作流和简单参数作为示例
  narrativeStore.triggerMainWorkflow(
      props.blockId,
      "DefaultContinueWorkflow", // 假设的默认继续工作流
      { contextPrompt: "根据当前内容继续故事发展。" } // 示例参数
  );
};

/** 切换到左侧的兄弟节点 */
const pageLeft = () => {
  if (!canPageLeft.value || narrativeStore.isLoadingAction) return;
  const previousSiblingId = siblings.value[currentSiblingIndex.value - 1];
  console.log(`BlockBubble: 请求切换到兄弟节点 ${previousSiblingId}`);
  narrativeStore.switchToSibling(previousSiblingId);
};

/** 切换到右侧的兄弟节点 */
const pageRight = () => {
  if (!canPageRight.value || narrativeStore.isLoadingAction) return;
  const nextSiblingId = siblings.value[currentSiblingIndex.value + 1];
  console.log(`BlockBubble: 请求切换到兄弟节点 ${nextSiblingId}`);
  narrativeStore.switchToSibling(nextSiblingId);
};

/** 重新生成当前 Block */
const regenerate = () => {
  if (!block.value || narrativeStore.isLoadingAction || (status.value !== BlockStatusCode.IDLE && status.value !== BlockStatusCode.ERROR)) return;
  console.log(`BlockBubble: 请求重新生成 Block ${props.blockId}`);
  narrativeStore.regenerateBlock(
      props.blockId,
      "DefaultRegenerateWorkflow", // 假设的重新生成工作流
      { originalContent: block.value.blockContent } // 示例参数
  );
};

/** 删除当前 Block */
const deleteThisBlock = () => {
  if (!block.value || narrativeStore.isLoadingAction || props.blockId === narrativeStore.rootBlockId || (status.value !== BlockStatusCode.IDLE && status.value !== BlockStatusCode.ERROR)) return;
  if (confirm(`确定要删除 Block "${props.blockId}" 及其所有子节点吗？此操作不可撤销。`)) {
    console.log(`BlockBubble: 请求删除 Block ${props.blockId}`);
    narrativeStore.deleteBlock(props.blockId, true, false); // 递归删除，非强制
  }
};

/** 查看冲突详情 (需要 App.vue 或其他组件配合实现) */
const viewConflictDetails = () => {
  // 这个方法可以 emit 一个事件，让父组件打开冲突解决面板
  // 或者如果冲突信息在 store 中是全局唯一的，可以直接操作 store 状态让面板显示
  if (activeConflict.value) {
    console.log("BlockBubble: 请求查看冲突详情", activeConflict.value);
    // 示例：假设 App.vue 监听这个状态变化来显示冲突面板
    // narrativeStore.showConflictPanel = true; // 需要在 store 中添加这样的状态
    alert("请在主界面查看冲突详情并解决。\n(这里仅为按钮示例)");
  }
};

</script>

<style scoped>
.block-bubble-container {
  border: 1px solid #ccc;
  border-radius: 8px;
  padding: 15px;
  margin-bottom: 15px;
  background-color: #fff;
  transition: background-color 0.3s ease, border-color 0.3s ease;
  position: relative; /* 为了调试信息和分页器的定位 */
}

/* 状态样式 */
.block-bubble-container.is-loading {
  background-color: #f0f0f0;
  border-color: #ddd;
}
.block-bubble-container.is-streaming {
  background-color: #e8f4ff; /* 淡蓝色背景表示正在流式输出 */
  border-color: #b3d7ff;
}
.block-bubble-container.is-error {
  background-color: #fff0f0;
  border-color: #ffcccc;
}
.block-bubble-container.is-resolving-conflict {
  background-color: #fff8e1;
  border-color: #ffecb3;
  border-left: 5px solid orange;
}
.block-bubble-container.is-current-leaf {
  border: 2px solid #4CAF50; /* 给当前叶节点一个醒目的边框 */
  box-shadow: 0 0 5px rgba(76, 175, 80, 0.5);
}


.debug-info {
  font-size: 0.7em;
  color: #aaa;
  position: absolute;
  top: 5px;
  right: 10px;
  background-color: rgba(255, 255, 255, 0.8);
  padding: 0 5px;
  border-radius: 3px;
}

.sibling-pager {
  font-size: 0.9em;
  color: #666;
  margin-bottom: 10px;
  padding-bottom: 5px;
  border-bottom: 1px dashed #eee;
  display: flex;
  align-items: center;
  justify-content: center; /* 居中显示 */
}

.sibling-pager button {
  background: none;
  border: 1px solid #ccc;
  border-radius: 4px;
  padding: 2px 8px;
  margin: 0 10px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.sibling-pager button:hover:not(:disabled) {
  background-color: #eee;
}

.sibling-pager button:disabled {
  color: #ccc;
  cursor: not-allowed;
  border-color: #eee;
}

.sibling-pager span {
  font-weight: bold;
}

.block-content {
  margin-bottom: 15px;
  min-height: 50px; /* 避免内容为空时塌陷 */
}

.loading-indicator p,
.loading-placeholder p,
.error-message p,
.conflict-message p {
  color: #888;
  margin: 10px 0;
}
.error-message p {
  color: #d32f2f;
}
.conflict-message p {
  color: #f57c00;
}


.streaming-content pre,
.final-content pre {
  white-space: pre-wrap; /* 保留换行和空格 */
  word-wrap: break-word; /* 允许长单词换行 */
  font-family: inherit; /* 继承容器字体 */
  margin: 0; /* 去掉 pre 的默认 margin */
  font-size: 1em;
  line-height: 1.5;
}

.streaming-indicator {
  display: inline-block;
  animation: blink 1s infinite;
  color: #333;
  font-weight: bold;
}

@keyframes blink {
  0%, 100% { opacity: 1; }
  50% { opacity: 0; }
}


.block-actions {
  display: flex;
  gap: 10px; /* 按钮之间的间距 */
  flex-wrap: wrap; /* 按钮多时换行 */
  border-top: 1px solid #eee;
  padding-top: 10px;
  margin-top: 10px;
}

.block-actions button {
  padding: 6px 12px;
  border-radius: 4px;
  cursor: pointer;
  border: 1px solid #ccc;
  background-color: #f8f8f8;
  transition: background-color 0.2s, border-color 0.2s;
}

.block-actions button:hover:not(:disabled) {
  background-color: #eee;
  border-color: #bbb;
}

.block-actions button:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.generate-next-button {
  background-color: #4CAF50; /* 绿色 */
  color: white;
  border-color: #4CAF50;
}
.generate-next-button:hover:not(:disabled) {
  background-color: #45a049;
  border-color: #45a049;
}

.delete-button {
  background-color: #f44336; /* 红色 */
  color: white;
  border-color: #f44336;
}
.delete-button:hover:not(:disabled) {
  background-color: #d32f2f;
  border-color: #d32f2f;
}

.action-button {
  /* 可以给普通操作按钮一些默认样式 */
}

</style>