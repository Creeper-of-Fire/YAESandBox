<template>
  <div>
    <h1>YAESandBox 前端</h1>

    <!-- SignalR 连接状态 -->
    <p>
      SignalR:
      <span v-if="narrativeStore.isSignalRConnecting">连接中...</span>
      <span v-else-if="narrativeStore.isSignalRConnected" style="color: green;">已连接</span>
      <span v-else style="color: red;">已断开</span>
      <button @click="narrativeStore.connectSignalR" :disabled="narrativeStore.isSignalRConnected || narrativeStore.isSignalRConnecting">连接</button>
      <button @click="narrativeStore.disconnectSignalR" :disabled="!narrativeStore.isSignalRConnected">断开</button>
    </p>

    <!-- 加载/保存 -->
    <div>
      <button @click="handleLoadClick" :disabled="narrativeStore.isLoadingAction">加载存档</button>
      <input type="file" ref="fileInput" @change="handleFileSelected" accept=".json" style="display: none;" />
      <button @click="narrativeStore.saveState()" :disabled="narrativeStore.isLoadingAction || !narrativeStore.isSignalRConnected">保存存档</button>
      <span v-if="narrativeStore.isLoadingAction"> 操作中...</span>
    </div>

    <!-- 加载状态 -->
    <p v-if="narrativeStore.isLoadingBlocks || narrativeStore.isLoadingTopology">
      正在加载核心数据...
    </p>

    <!-- 冲突提示 -->
    <div v-if="activeConflict" style="border: 2px solid orange; padding: 10px; margin: 10px 0;">
      <h3>检测到冲突 (Block: {{ activeConflict.blockId }})</h3>
      <p>请解决冲突后提交。</p>
      <pre>AI 指令: {{ JSON.stringify(activeConflict.aiCommands, null, 2) }}</pre>
      <pre>用户指令: {{ JSON.stringify(activeConflict.userCommands, null, 2) }}</pre>
      <button @click="resolveSampleConflict(activeConflict)" :disabled="narrativeStore.isLoadingAction">
        (示例) 接受 AI 指令解决冲突
      </button>
      <button @click="narrativeStore.activeConflict = null">暂时忽略</button> {/* 不推荐 */}
    </div>

    <!-- 主 Block 流 -->
    <div class="block-stream" v-if="narrativeStore.topology && currentPathBlocks.length > 0">
      <h2>当前路径</h2>
      <div v-for="block in currentPathBlocks" :key="block.blockId" class="block-bubble">
        <BlockBubble :block-id="block.blockId" />
      </div>
    </div>
    <div v-else-if="!narrativeStore.isLoadingBlocks && !narrativeStore.isLoadingTopology">
      <p>没有可显示的 Block。尝试触发第一个工作流？</p>
      <button @click="triggerFirstWorkflow" v-if="narrativeStore.rootBlockId">开始</button>
    </div>

    <!-- 微工作流测试 -->
    <div style="margin-top: 20px; border: 1px solid #ccc; padding: 10px;">
      <h3>微工作流测试 (润色)</h3>
      <textarea v-model="textToPolish" rows="3" style="width: 90%;"></textarea>
      <button @click="polishText" :disabled="!narrativeStore.currentPathLeafId || !narrativeStore.isSignalRConnected">✨ 润色</button>
      <div v-if="polishResult">
        <h4>润色结果:</h4>
        <p v-if="polishResult.status === 'Streaming'" style="color: gray;">处理中...</p>
        <p v-if="polishResult.status === 'Error'" style="color: red;">错误: {{ polishResult.content }}</p>
        <p v-if="polishResult.status === 'Complete'">{{ polishResult.content }}</p>
      </div>
    </div>

  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useNarrativeStore } from '@/stores/narrativeStore.js';
import BlockBubble from '@/components/BlockBubble.vue'; // 假设你有一个 BlockBubble 组件
import type { ConflictDetectedDto, BlockDetailDto } from '@/types/generated/api.ts';
import { StreamStatus } from '@/types/generated/api.ts';

const narrativeStore = useNarrativeStore();
const fileInput = ref<HTMLInputElement | null>(null);
const textToPolish = ref("这是一段需要润色的示例文本。");
const POLISH_TARGET_ID = "text-polisher-output"; // 微工作流目标 ID

// --- 计算属性 ---

// 获取当前路径上的 Block 详细信息
const currentPathBlocks = computed((): BlockDetailDto[] => {
  const ids = narrativeStore.getCurrentPathBlockIds;
  return ids.map(id => narrativeStore.getBlockById(id)).filter(Boolean) as BlockDetailDto[];
});

// 获取当前激活的冲突
const activeConflict = computed(() => narrativeStore.getActiveConflict);

// 获取润色结果
const polishResult = computed(() => narrativeStore.getMicroWorkflowUpdate(POLISH_TARGET_ID));


// --- 生命周期钩子 ---
onMounted(async () => {
  // 组件挂载时尝试连接 SignalR 并加载初始数据
  if (!narrativeStore.isSignalRConnected) {
    await narrativeStore.connectSignalR();
  }
  // 只有连接成功后才加载数据
  if (narrativeStore.isSignalRConnected) {
    if (!narrativeStore.topology) {
      await narrativeStore.fetchTopology();
    }
    if (Object.keys(narrativeStore.blocks).length === 0) {
      await narrativeStore.fetchBlocks();
    }
  }
});

// --- 方法 ---

// 加载文件处理
const handleLoadClick = () => {
  fileInput.value?.click();
};
const handleFileSelected = (event: Event) => {
  const target = event.target as HTMLInputElement;
  if (target.files && target.files[0]) {
    narrativeStore.loadState(target.files[0]);
  }
};

// 示例：解决冲突（简单地接受 AI 的指令）
const resolveSampleConflict = (conflict: ConflictDetectedDto) => {
  if (!conflict || !conflict.requestId || !conflict.blockId) return;
  // 在实际应用中，你需要一个 UI 来让用户选择或合并指令
  const resolvedCommands = conflict.aiCommands ?? []; // 这里简单接受 AI 的
  narrativeStore.resolveConflict(conflict.requestId, conflict.blockId, resolvedCommands);
};

// 示例：触发第一个工作流 (如果根节点存在)
const triggerFirstWorkflow = () => {
  if (narrativeStore.rootBlockId) {
    narrativeStore.triggerMainWorkflow(
        narrativeStore.rootBlockId,
        "DefaultStartWorkflow", // 假设有一个默认的开始工作流
        { prompt: "在一个宁静的幻想村庄开始故事。" } // 示例参数
    );
  }
};

// 示例：调用文本润色微工作流
const polishText = () => {
  const contextBlockId = narrativeStore.currentPathLeafId;
  if (!contextBlockId) {
    alert("请先选择一个 Block 作为上下文！");
    return;
  }
  narrativeStore.triggerMicroWorkflow(
      contextBlockId,
      POLISH_TARGET_ID, // 目标元素 ID
      "PolishTextWorkflow", // 假设的微工作流名称
      { text: textToPolish.value } // 传递需要润色的文本
  );
};

// 监视连接状态变化，成功连接后加载数据
watch(() => narrativeStore.isSignalRConnected, async (isConnected) => {
  if (isConnected) {
    console.log("连接成功，开始加载初始数据...");
    if (!narrativeStore.topology) {
      await narrativeStore.fetchTopology();
    }
    if (Object.keys(narrativeStore.blocks).length === 0) {
      await narrativeStore.fetchBlocks();
    }
  }
});

// 监视润色结果，完成后更新输入框 (可选)
watch(polishResult, (newResult) => {
  if (newResult?.status === StreamStatus.COMPLETE && newResult.content) {
    // textToPolish.value = newResult.content; // 取消注释以自动更新输入框
  }
})

</script>

<style scoped>
.block-stream {
  margin-top: 20px;
  border: 1px solid #eee;
  padding: 10px;
}
.block-bubble {
  margin-bottom: 15px;
  padding: 10px;
  border-radius: 8px;
  background-color: #f9f9f9;
  border: 1px solid #ddd;
}
/* 可以添加更多样式 */
</style>