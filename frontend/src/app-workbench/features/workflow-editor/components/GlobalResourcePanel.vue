<!-- START OF FILE: src/app-workbench/features/workflow-editor/components/GlobalResourcePanel.vue -->
<template>
  <div class="global-resource-panel">
    <n-h4>全局资源</n-h4>
    <n-spin :show="aggregatedIsLoading">
      <!-- 状态一：正在加载 -->
      <div v-if="aggregatedIsLoading" class="panel-state-wrapper">
        <n-spin size="small"/>
        <span style="margin-left: 8px;">正在加载...</span>
      </div>

      <!-- 状态二：加载出错 -->
      <div v-else-if="aggregatedError" class="panel-state-wrapper">
        <n-alert title="加载错误" type="error" :show-icon="true">
          无法加载全局资源。
        </n-alert>
        <!-- 将重试按钮放在 alert 下方，作为独立的错误恢复操作 -->
        <n-button @click="executeAll" block secondary strong style="margin-top: 12px;">
          重试
        </n-button>
      </div>

      <!-- 状态三：加载成功，显示数据 -->
      <div v-else>
        <n-collapse arrow-placement="right">
          <!-- ====================================================== -->
          <!--                  工作流列表渲染部分                    -->
          <!-- ====================================================== -->
          <n-collapse-item title="工作流" name="workflows">
            <div v-if="workflows && Object.keys(workflows).length > 0">
              <!--
                【核心】: 使用 v-for="(item, id) in workflows" 来遍历 Record。
                - `item` 是 GlobalResourceItem<WorkflowProcessorConfig> 对象
                - `id` 是该项在 Record 中的 key (即工作流的 configId)
                - `:key="id"` 使用 id 作为循环的唯一标识
              -->
              <div v-for="(item, id) in workflows" :key="id">

                <!-- Case 1: 渲染【成功】加载的项 -->
                <div
                    v-if="item.isSuccess"
                    class="resource-item"
                    @dblclick="startEditing('workflow', id)"
                >
                  <span>{{ item.data.name }}</span>
                  <n-button text @click="startEditing('workflow', id)">编辑</n-button>
                </div>

                <!-- Case 2: 渲染【已损坏】的项 -->
                <div v-else class="resource-item-damaged">
                  <n-icon :component="LinkOffIcon" color="#d03050"/>
                  <n-tooltip trigger="hover">
                    <template #trigger>
                      <span class="damaged-text">{{ id }} (已损坏)</span>
                    </template>
                    {{ item.errorMessage }}
                  </n-tooltip>
                  <n-button
                      text
                      @click="showErrorDetail(item.errorMessage,item.originJsonString)"
                  >
                    详情
                  </n-button>
                </div>

              </div>
            </div>
            <div v-else>
              <!-- 如果 Record 为空或不存在，则显示 Empty 状态 -->
              <n-empty small description="无全局工作流"/>
            </div>
          </n-collapse-item>

          <!-- ====================================================== -->
          <!--                    步骤列表渲染部分                      -->
          <!-- ====================================================== -->
          <n-collapse-item title="步骤" name="steps">
            <div v-if="steps && Object.keys(steps).length > 0">
              <div v-for="(item, id) in steps" :key="id">
                <!-- 成功项 -->
                <div v-if="item.isSuccess" class="resource-item" @dblclick="startEditing('step', id)">
                  <span>{{ item.data.name }}</span>
                  <n-button text @click="startEditing('step', id)">编辑</n-button>
                </div>
                <!-- 损坏项 -->
                <div v-else class="resource-item-damaged">
                  <n-icon :component="LinkOffIcon" color="#d03050"/>
                  <n-tooltip trigger="hover">
                    <template #trigger>
                      <span class="damaged-text">{{ id }} (已损坏)</span>
                    </template>
                    {{ item.errorMessage }}
                  </n-tooltip>
                  <n-button
                      text
                      @click="showErrorDetail(item.errorMessage,item.originJsonString)"
                  >
                    详情
                  </n-button>
                </div>
              </div>
            </div>
            <n-empty v-else small description="无全局步骤"/>
          </n-collapse-item>

          <!-- ====================================================== -->
          <!--                    模块列表渲染部分                      -->
          <!-- ====================================================== -->
          <n-collapse-item title="模块" name="modules">
            <div v-if="modules && Object.keys(modules).length > 0">
              <div v-for="(item, id) in modules" :key="id">
                <!-- 成功项 -->
                <div v-if="item.isSuccess" class="resource-item" @dblclick="startEditing('module', id)">
                  <span>{{ item.data.name }}</span>
                  <n-button text @click="startEditing('module', id)">编辑</n-button>
                </div>
                <!-- 损坏项 -->
                <div v-else class="resource-item-damaged">
                  <n-icon :component="LinkOffIcon" color="#d03050"/>
                  <n-tooltip trigger="hover">
                    <template #trigger>
                      <span class="damaged-text">{{ id }} (已损坏)</span>
                    </template>
                    {{ item.errorMessage }}
                  </n-tooltip>
                  <n-button
                      text
                      @click="showErrorDetail(item.errorMessage,item.originJsonString)"
                  >
                    详情
                  </n-button>
                </div>
              </div>
            </div>
            <n-empty v-else small description="无全局模块"/>
          </n-collapse-item>

        </n-collapse>
      </div>
    </n-spin>
  </div>
</template>

<script setup lang="ts">
import {computed, h, onMounted} from 'vue';
import {useWorkbenchStore} from '@/app-workbench/features/workflow-editor/stores/workbenchStore.ts';
import {NAlert, NButton, NCollapse, NCollapseItem, NEmpty, NH4, NIcon, NSpin, useDialog} from 'naive-ui';
import type {ConfigType} from "@/app-workbench/features/workflow-editor/services/EditSession.ts";
import {LinkOffOutlined as LinkOffIcon} from '@vicons/material';


const emit = defineEmits<{
  (e: 'start-editing', payload: { type: ConfigType; id: string }): void;
}>();

const workbenchStore = useWorkbenchStore();
const dialog = useDialog();

// 获取异步数据访问器
const workflowsAsync = workbenchStore.globalWorkflowsAsync;
const stepsAsync = workbenchStore.globalStepsAsync;
const modulesAsync = workbenchStore.globalModulesAsync;
// 解构出完整的异步状态机
const workflows = computed(() => workflowsAsync.state);
const steps = computed(() => stepsAsync.state);
const modules = computed(() => modulesAsync.state);

/**
 * 聚合的加载状态。
 * 只要任何一个资源正在加载，整个面板就处于加载状态。
 */
const aggregatedIsLoading = computed(() =>
    workflowsAsync.isLoading ||
    stepsAsync.isLoading ||
    modulesAsync.isLoading
);

/**
 * 聚合的错误状态。
 * 返回第一个遇到的错误对象。
 */
const aggregatedError = computed(() =>
    workflowsAsync.error ||
    stepsAsync.error ||
    modulesAsync.error
);

/**
 * 聚合的执行函数。
 * 调用此函数会触发所有资源的加载（或重试）。
 */
function executeAll() {
  // 依次调用每个 execute 方法。
  workflowsAsync.execute();
  stepsAsync.execute();
  modulesAsync.execute();
}


// 组件挂载时触发数据加载
onMounted(() => {
  executeAll()
});

function showErrorDetail(errorMessage: string, originJsonString: string | null | undefined) {
  const totalMessage = `错误信息: ${errorMessage}\n\n原始JSON: ${originJsonString || '无'}`;
  const messageLines = totalMessage.split('\n');

  dialog.error({
    title: '错误详情',
    content: () => h(
        'div', // 外层容器，可以是一个 div 或者 Fragment
        null,
        messageLines.map(line => h('div', null, line))
        ),
    positiveText: '确定'
  });
}

function startEditing(type: ConfigType, id: string) {
  emit('start-editing', {type, id});
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

.resource-item-damaged {
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