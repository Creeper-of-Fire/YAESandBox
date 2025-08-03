<!-- src/app-workbench/components/.../WorkflowItemRenderer.vue -->
<template>
  <div class="workflow-item-renderer">
    <!-- 新增：工作流触发参数编辑器 -->
    <n-card size="small" style="margin-bottom: 16px;" title="工作流触发参数">
      <n-text depth="3" style="font-size: 12px; display: block; margin-bottom: 8px;">
        定义工作流启动时需要从外部传入的参数名称。这些参数后续可以在祝祷的输入映射中使用。
      </n-text>
      <n-dynamic-tags v-model:value="triggerParamsRef"/>
    </n-card>


    <!-- 工作流的祝祷列表 (可拖拽排序，接受来自全局资源的祝祷) -->
    <draggable
        v-if="workflow.tuums && workflow.tuums.length > 0"
        v-model="workflow.tuums"
        :animation="150"
        :group="{ name: 'tuums-group', put: ['tuums-group'] }"
        class="workflow-tuum-list-container"
        ghost-class="workbench-ghost-item"
        handle=".drag-handle"
        item-key="configId"
    >
      <div v-for="(tuumItem, index) in workflow.tuums" :key="tuumItem.configId" class="tuum-item">
        <!-- 在工作流列表里，使用默认行为的 TuumItemRenderer -->
        <div :key="tuumItem.configId" class="tuum-item-container">
          <TuumItemRenderer
              :available-global-vars-for-tuum="getAvailableVarsForTuum(index)"
              :parent-workflow="workflow"
              :tuum="tuumItem"
          />
        </div>
      </div>
    </draggable>
    <!-- 工作流祝祷列表为空时的提示 -->
    <n-empty v-else class="workflow-tuum-empty-placeholder" description="拖拽祝祷到此处" small/>
  </div>
</template>

<script lang="ts" setup>
import {NEmpty} from 'naive-ui';
import {VueDraggable as draggable} from 'vue-draggable-plus';
import type {WorkflowConfig} from "@/app-workbench/types/generated/workflow-config-api-client";
import TuumItemRenderer from '../tuum/TuumItemRenderer.vue';
import { computed } from 'vue';

// 定义组件的 Props
const props = defineProps<{
  workflow: WorkflowConfig;
}>();

const triggerParamsRef = computed({
  get: () => props.workflow?.triggerParams || [],
  set: (value) => props.workflow.triggerParams = Array.isArray(value) ? value : []
});

/**
 * 计算在指定索引的祝祷开始执行前，所有可用的全局变量。
 * @param tuumIndex - 祝祷在工作流中的索引。
 */
function getAvailableVarsForTuum(tuumIndex: number): string[]
{
  const triggerParamsArray = triggerParamsRef.value; // 使用计算属性
  const availableVars = new Set<string>(triggerParamsArray);

  if (props.workflow?.tuums)
  {
    for (let i = 0; i < tuumIndex; i++)
    {
      const precedingTuum = props.workflow.tuums[i];
      if (precedingTuum?.outputMappings)
      {
        Object.keys(precedingTuum.outputMappings).forEach(globalVar =>
        {
          availableVars.add(globalVar);
        });
      }
    }
  }

  return Array.from(availableVars);
}

</script>

<style scoped>
/* 工作流中的祝祷列表容器样式 */
.workflow-tuum-list-container {
  display: flex;
  flex-direction: column;
  gap: 16px; /* 祝祷之间的间距 */
  min-height: 50px;
  border: 1px dashed #dcdfe6;
  border-radius: 6px;
  padding: 8px;
  background-color: #fcfcfc;
}

/* 单个祝祷项在列表中的容器 */
.tuum-item-container {
  height: 100%;
  /* 未来可以添加样式 */
}

/* 工作流祝祷列表为空时的占位符样式 */
.workflow-tuum-empty-placeholder {
  padding: 20px;
  border: 1px dashed #dcdfe6;
  border-radius: 6px;
  background-color: #fcfcfc;
}
</style>