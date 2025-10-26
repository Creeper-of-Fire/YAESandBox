<!-- src/app-workbench/components/.../WorkflowItemRenderer.vue -->
<template>
  <div class="workflow-item-renderer">
    <!-- 工作流入口参数编辑器 -->
    <n-card size="small" style="margin-bottom: 16px;" title="工作流入口参数">
      <n-text depth="3" style="font-size: 12px; display: block; margin-bottom: 8px;">
        定义工作流启动时需要从外部传入的参数名称。这些参数后续可以在枢机的输入映射中使用。
      </n-text>
      <n-dynamic-tags v-model:value="workflowInputsRef"/>
    </n-card>

    <n-card size="small" style="margin-bottom: 16px;" title="工作流标签">
      <n-text depth="3" style="font-size: 12px; display: block; margin-bottom: 8px;">
        为工作流添加描述性标签，便于在选择器中进行分类和筛选。
      </n-text>
      <n-dynamic-tags v-model:value="workflowTagsRef"/>
    </n-card>

    <WorkflowAnalysisPanel :workflow="workflow"/>

    <!-- 工作流的枢机列表 (可拖拽排序，接受来自全局资源的枢机) -->
    <CollapsibleConfigList
        v-model:items="workflow.tuums"
        empty-description="拖拽枢机到此处"
        group-name="tuums-group"
        class="workflow-tuum-list"
    >
      <template #item="{ element: tuumItem }">
        <!-- 在工作流列表里，使用默认行为的 TuumItemRenderer -->
        <TuumItemRenderer
            :parent-workflow="workflow"
            :tuum="tuumItem"
        />
      </template>
    </CollapsibleConfigList>
  </div>
</template>

<script lang="ts" setup>
import type {WorkflowConfig} from "#/types/generated/workflow-config-api-client";
import TuumItemRenderer from '../tuum/TuumItemRenderer.vue';
import {computed} from 'vue';
import CollapsibleConfigList from "#/components/share/renderer/CollapsibleConfigList.vue";
import WorkflowAnalysisPanel from "#/components/workflow/analysis/WorkflowAnalysisPanel.vue";

// 定义组件的 Props
const props = defineProps<{
  workflow: WorkflowConfig;
}>();

const workflowInputsRef = computed({
  get: () => props.workflow?.workflowInputs || [],
  set: (value) => props.workflow.workflowInputs = Array.isArray(value) ? value : []
});

const workflowTagsRef = computed({
  get: () => props.workflow?.tags || [],
  set: (value) => {
    if (props.workflow) {
      props.workflow.tags = Array.isArray(value) ? value : [];
    }
  }
});

</script>

<style scoped>
.workflow-tuum-list {
  margin-left: 0;
}
</style>