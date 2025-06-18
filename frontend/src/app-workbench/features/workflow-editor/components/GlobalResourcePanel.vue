<!-- START OF FILE: src/app-workbench/features/workflow-editor/components/GlobalResourcePanel.vue -->
<template>
  <div class="global-resource-panel">
    <n-h4>全局资源</n-h4>
    <n-spin :show="isLoading">
      <!-- 状态一：正在加载 -->
      <div v-if="isLoading" class="panel-state-wrapper">
        <n-spin size="small" />
        <span style="margin-left: 8px;">正在加载...</span>
      </div>

      <!-- 状态二：加载出错 -->
      <div v-else-if="error" class="panel-state-wrapper">
        <n-alert title="加载错误" type="error" :show-icon="true">
          无法加载全局资源。
        </n-alert>
        <!-- 将重试按钮放在 alert 下方，作为独立的错误恢复操作 -->
        <n-button @click="execute" block secondary strong style="margin-top: 12px;">
          重试
        </n-button>
      </div>

      <!-- 状态三：加载成功，显示数据 -->
      <div v-else>
        <!-- 工作流列表 -->
        <n-collapse arrow-placement="right">
          <n-collapse-item title="工作流" name="workflows">
            <div v-if="Object.keys(workflows || {}).length > 0">
              <div
                  v-for="wf in workflows"
                  :key="wf.name"
                  class="resource-item"
                  @dblclick="startEditing('workflow', wf.name)"
              >
                <span>{{ wf.name }}</span>
                <n-button text @click="startEditing('workflow', wf.name)">编辑</n-button>
              </div>
            </div>
            <n-empty v-else small description="无全局工作流" />
          </n-collapse-item>

          <!-- TODO: 添加步骤和模块的列表 -->
          <n-collapse-item title="步骤 (待实现)" name="steps"></n-collapse-item>
          <n-collapse-item title="模块 (待实现)" name="modules"></n-collapse-item>

        </n-collapse>
      </div>
    </n-spin>
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue';
import { useWorkbenchStore } from '@/app-workbench/features/workflow-editor/stores/workbenchStore.ts';
import { NH4, NSpin, NAlert, NButton, NCollapse, NCollapseItem, NEmpty } from 'naive-ui';
import type {ConfigType} from "@/app-workbench/features/workflow-editor/services/EditSession.ts";

const emit = defineEmits<{
  (e: 'start-editing', payload: { type: ConfigType; id: string }): void;
}>();

const workbenchStore = useWorkbenchStore();

// 获取异步数据访问器
const workflowsAsync = workbenchStore.globalWorkflowsAsync;
// 解构出完整的异步状态机
const { data: workflows, isLoading, error, execute } = workbenchStore.globalWorkflowsAsync;
// ... 同样需要为 steps 和 modules 解构 ...

// 组件挂载时触发数据加载
onMounted(() => {
  workflowsAsync.execute();
  // TODO: 也需要为步骤和模块执行
});

function startEditing(type: ConfigType, id: string) {
  emit('start-editing', { type, id });
}
</script>

<style scoped>
.resource-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 4px 8px;
  border-radius: 4px;
  cursor: pointer;
}
.resource-item:hover {
  background-color: #f0f2f5;
}
</style>
<!-- END OF FILE -->