<template>
  <!-- TODO 改成Naive样式 -->
  <div
      class="block-bubble-container"
      :class="{
      'is-loading': isLoading, // Class for styling loading state (e.g., adding indicator)
      'is-error': isError,
      'is-resolving-conflict': isResolvingConflict,
      'is-current-leaf': isCurrentLeaf
    }"
  >
    <!-- 调试信息 (可选) -->
    <div class="debug-info">
      ID: {{ blockId }} | Status: {{ status || 'N/A' }} | Parent: {{ parentNode?.id || 'None' }}
    </div>

    <!-- 兄弟节点分页器 (SiblingPager) -->
    <div v-if="totalSiblings > 1" class="sibling-pager">
      <button @click="pageLeft" :disabled="!canPageLeft || globalLoadingAction"><</button>
      <span>分支 {{ currentSiblingIndex + 1 }}/{{ totalSiblings }}</span>
      <button @click="pageRight" :disabled="!canPageRight || globalLoadingAction">></button>
    </div>

    <!-- 主要内容区域 -->
    <div class="block-content">
      <!-- 加载指示器 (当 isLoading 为 true 时显示) -->
      <div v-if="isLoading" class="loading-indicator" title="正在加载...">
        ⏳
        <!-- 或者使用 SVG/CSS 加载动画 -->
      </div>

      <!-- 错误状态消息 (优先于内容显示) -->
      <div v-if="isError" class="error-message">
        <p>加载此 Block 时出错。</p>
        <button @click="regenerate" :disabled="globalLoadingAction">尝试重新生成</button>
      </div>

      <!-- 冲突状态消息 (优先于内容显示) -->
      <div v-else-if="isResolvingConflict" class="conflict-message">
        <p><strong>存在冲突!</strong></p>
        <p>请在外部面板解决冲突。</p>
        <!-- <button @click="viewConflictDetails" :disabled="!activeConflict">查看冲突详情</button> -->
      </div>

      <!-- Block 内容 (始终尝试渲染，即使在加载中) -->
      <div v-if="blockDetail" class="content-display">
        <pre>{{ displayContent }}</pre>
      </div>
      <!-- Block 详情尚未加载时的占位符 (仅在非错误/冲突时显示) -->
      <div v-else-if="!isLoading && !isError && !isResolvingConflict" class="content-placeholder">
        <p>等待 Block 内容...</p>
      </div>
    </div>

    <!-- 交互操作区域 -->
    <div class="block-actions">
      <!-- 触发生成下一个 Block -->
      <button
          @click="generateNext"
          :disabled="status !== BlockStatusCode.IDLE || globalLoadingAction"
          class="generate-next-button"
          title="生成下一个 Block"
      >
        生成下一个
      </button>

      <!-- 重新生成 -->
      <button
          @click="regenerate"
          :disabled="!(status === BlockStatusCode.IDLE || status === BlockStatusCode.ERROR) || globalLoadingAction"
          class="action-button"
          title="重新生成当前 Block 内容"
      >
        🔄 重新生成(WIP)
      </button>

      <!-- 删除 -->
      <button
          @click="deleteThisBlock"
          :disabled="!(status === BlockStatusCode.IDLE || status === BlockStatusCode.ERROR) || globalLoadingAction || blockId === topologyStore.rootNode?.id"
          class="action-button delete-button"
          title="删除当前 Block (及其子节点)"
      >
        🗑️ 删除
      </button>

      <!-- 其他可能的按钮，例如触发编辑 -->
      <!-- <button :disabled="globalLoadingAction">编辑</button> -->
    </div>

  </div>
</template>

<script setup lang="ts">
import {computed, onMounted} from 'vue';
import {v4 as uuidv4} from 'uuid';
import {useTopologyStore} from '@/stores/topologyStore';
import {useBlockContentStore} from '@/stores/blockContentStore';
import {useBlockStatusStore} from '@/stores/blockStatusStore';
import {BlockStatusCode, type ConflictDetectedDto, type RegenerateBlockRequestDto} from '@/types/generated/api'; // 引入 Enum
import type {ProcessedBlockNode} from '@/stores/topologyStore'; // 引入处理后的节点类型
import {signalrService} from '@/services/signalrService'; // <--- 导入 signalrService
import {BlockManagementService} from '@/types/generated/api'; // <--- 导入用于删除的 REST API Service

const props = defineProps<{
  blockId: string;
}>();

// --- 获取 Stores ---
const topologyStore = useTopologyStore();
const blockContentStore = useBlockContentStore();
const blockStatusStore = useBlockStatusStore();

// --- Computed Properties ---

/** 获取当前 Block 的处理后节点引用 */
const blockNode = computed<ProcessedBlockNode | undefined>(() => topologyStore.getNodeById(props.blockId));

/** 获取当前 Block 的内容详情 DTO */
const blockDetail = computed(() => blockContentStore.getBlockById(props.blockId));

/** 获取当前 Block 的状态码 */
const status = computed<BlockStatusCode | undefined>(() => blockStatusStore.getBlockStatus(props.blockId));

// --- 基于状态码的计算属性 ---
/** 是否处于加载状态 */
const isLoading = computed(() => status.value === BlockStatusCode.LOADING);
/** 是否处于错误状态 */
const isError = computed(() => status.value === BlockStatusCode.ERROR);
/** 是否处于冲突解决状态 */
const isResolvingConflict = computed(() => status.value === BlockStatusCode.RESOLVING_CONFLICT);

/** 获取冲突详情 */
const conflictDetails = computed<ConflictDetectedDto | undefined>(() => blockDetail.value?.conflictDetected);

/** 获取要显示的内容 */
const displayContent = computed(() => blockDetail.value?.blockContent ?? ""); // 直接从详情获取

/** 获取父节点引用 */
const parentNode = computed(() => blockNode.value?.parent);

/** 获取兄弟节点引用列表 (包括自身) */
const siblings = computed<ProcessedBlockNode[]>(() => {
  // 如果有父节点，返回父节点的所有子节点；否则如果自身存在，返回自身数组；否则返回空
  return parentNode.value?.children ?? (blockNode.value ? [blockNode.value] : []);
});

/** 当前 Block 在兄弟节点中的索引 */
const currentSiblingIndex = computed(() => siblings.value.findIndex(node => node.id === props.blockId));

/** 兄弟节点的总数 */
const totalSiblings = computed(() => siblings.value.length);

/** 是否可以向左翻页 */
const canPageLeft = computed(() => currentSiblingIndex.value > 0);

/** 是否可以向右翻页 */
const canPageRight = computed(() => currentSiblingIndex.value < totalSiblings.value - 1);

/** 获取全局操作加载状态 */
const globalLoadingAction = computed(() => blockStatusStore.isLoadingAction);

/** 检查此 Block 是否是当前路径的叶节点 */
const isCurrentLeaf = computed(() => topologyStore.currentPathLeafId === props.blockId);

// 不再需要 activeConflict 的 computed，除非要在 Bubble 内显示冲突细节

// --- 新增：在挂载时检查并获取内容 ---
onMounted(() => {
  // 检查内容缓存中是否存在此 Block 的详情
  if (!blockContentStore.getBlockById(props.blockId)) {
    console.log(`BlockBubble [${props.blockId}] Mounted: 内容未缓存，触发获取...`);
    blockContentStore.fetchAllBlockDetails(props.blockId);
  } else {
    // console.log(`BlockBubble [${props.blockId}] Mounted: 内容已缓存。`);
  }
});

// --- Methods ---

/** 触发生成下一个 Block */
const generateNext = async () => {
  // 状态检查已通过 :disabled 完成，这里只需检查全局加载状态
  if (globalLoadingAction.value) return;
  console.log(`BlockBubble: 请求在 ${props.blockId} 下生成下一个 Block`);
  // 从 BlockStatusStore 触发 (简化示例，实际参数可能更复杂)
  await signalrService.triggerMainWorkflow(
      {
        requestId: uuidv4(),
        parentBlockId: props.blockId,
        workflowName: "DefaultContinueWorkflow",
        params: {contextPrompt: "继续故事。"}
      }
  );
};

/** 切换到左侧的兄弟节点 */
const pageLeft = () => {
  if (!canPageLeft.value || globalLoadingAction.value) return;
  const previousSiblingNode = siblings.value[currentSiblingIndex.value - 1];
  if (previousSiblingNode) {
    console.log(`BlockBubble: 请求切换到兄弟节点 ${previousSiblingNode.id}`);
    topologyStore.switchToSiblingNode(previousSiblingNode.id);
  }
};

/** 切换到右侧的兄弟节点 */
const pageRight = () => {
  if (!canPageRight.value || globalLoadingAction.value) return;
  const nextSiblingNode = siblings.value[currentSiblingIndex.value + 1];
  if (nextSiblingNode) {
    console.log(`BlockBubble: 请求切换到兄弟节点 ${nextSiblingNode.id}`);
    topologyStore.switchToSiblingNode(nextSiblingNode.id);
  }
};

/** TODO 以后可能我们还需要一个复制本block为兄弟节点，或者把对当前Block的修改应用到一个新生成的父节点上的功能。
 比如，可能我们对IDLE状态的Block的修改都是临时修改，而点击生成按钮就会自动应用，然后除了“应用并生成”以外还有一个“复制并生成”的按钮。
 **/

/** 重新生成当前 Block */
// TODO 目前逻辑是完全错误的，我们实际上需要获得父节点的相关内容之类的。而且父节点不可用时也需要锁死这个按钮。
const regenerate = async () => {
  if (globalLoadingAction.value) return;
  // 状态检查已在 :disabled 中完成
  console.log(`BlockBubble: 请求重新生成 Block ${props.blockId}`);
  await signalrService.regenerateBlock(
      {
        requestId: uuidv4(),
        blockId: props.blockId,
        workflowName: "DefaultRegenerateWorkflow",
        params: {originalContent: displayContent.value}
      }
  );
};

/** 删除当前 Block */
const deleteThisBlock = () => {
  if (globalLoadingAction.value || props.blockId === topologyStore.rootNode?.id) return;
  // 状态检查已在 :disabled 中完成
  if (confirm(`确定要删除 Block "${props.blockId}" 及其所有子节点吗？此操作不可撤销。`)) {
    console.log(`BlockBubble: 请求删除 Block ${props.blockId}`);
    BlockManagementService.deleteApiManageBlocks({blockId: props.blockId, recursive: true, force: false}); // 递归删除，非强制
  }
};

// viewConflictDetails 方法可以移除，冲突处理移到全局 UI

</script>

<style scoped>
.block-bubble-container {
  border: 1px solid #ccc;
  border-radius: 8px;
  padding: 15px;
  margin-bottom: 15px;
  background-color: #fff;
  transition: background-color 0.3s ease, border-color 0.3s ease;
  position: relative;
}

/* 状态样式 */
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
  border: 2px solid #4CAF50;
  box-shadow: 0 0 5px rgba(76, 175, 80, 0.5);
}

/* 给加载中状态添加视觉提示，但不改变背景 */
.block-bubble-container.is-loading .loading-indicator {
  display: inline-block; /* 或者其他需要的布局 */
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
  justify-content: center;
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
  min-height: 40px; /* 保持一定最小高度 */
  position: relative; /* 用于定位加载指示器 */
}

.loading-indicator {
  display: none; /* 默认隐藏 */
  position: absolute;
  top: 0px;
  left: -20px; /* 放在内容左侧外面一点 */
  font-size: 1.2em;
  color: #aaa;
  animation: spin 1.5s linear infinite; /* 添加旋转动画 */
}

@keyframes spin {
  0% {
    transform: rotate(0deg);
  }
  100% {
    transform: rotate(360deg);
  }
}


.error-message p,
.conflict-message p,
.content-placeholder p {
  color: #888;
  margin: 10px 0;
  font-style: italic;
}

.error-message p {
  color: #d32f2f;
  font-style: normal;
}

.conflict-message p {
  color: #f57c00;
  font-style: normal;
}

.error-message button {
  margin-left: 10px;
  font-size: 0.9em;
  padding: 2px 6px;
}


.content-display pre {
  white-space: pre-wrap;
  word-wrap: break-word;
  font-family: inherit;
  margin: 0;
  font-size: 1em;
  line-height: 1.5;
}

/* 如果需要在 Loading 时给内容区域一个视觉提示，可以这样做 */
.block-bubble-container.is-loading .content-display {
  opacity: 0.7; /* 内容稍微变淡 */
  /* 或者添加其他效果 */
}


.block-actions {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
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
  font-size: 0.9em;
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
  background-color: #4CAF50;
  color: white;
  border-color: #4CAF50;
}

.generate-next-button:hover:not(:disabled) {
  background-color: #45a049;
  border-color: #45a049;
}

.delete-button {
  background-color: #f44336;
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