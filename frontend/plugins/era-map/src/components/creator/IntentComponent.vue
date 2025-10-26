<template>
  <n-card :bordered="false" :title="cardTitle" size="small">
    <template #header-extra>
      <n-button v-if="instruction.status !== 'APPLIED'" text @click="discard">
        <template #icon>
          <n-icon :component="CloseIcon"/>
        </template>
      </n-button>
    </template>

    <!-- 状态 1: 等待用户输入 -->
    <div v-if="instruction.status === 'PENDING_USER_INPUT'">
      <n-input
          v-model:value="instruction.userInput.prompt"
          :autosize="{ minRows: 2 }"
          placeholder="输入你的想法，例如：一张刻有神秘符文的桌子"
          type="textarea"
      />
      <WorkflowSelectorButton
          :expected-outputs="['context']"
          :filter="workflowFilter"
          :storage-key="workflowStorageKey"
          style="margin-top: 8px;"
          @click="generate"/>
    </div>

    <!-- 状态 2: 正在生成 -->
    <div v-if="isGenerating">
      <n-progress :percentage="100" :processing="true" :show-indicator="false" type="line"/>
      <n-text depth="3" style="font-size: 12px; margin-top: 8px;">思考中...</n-text>
      <n-collapse v-if="thinkingProcess">
        <n-collapse-item name="1" title="查看思考过程">
          <pre style="white-space: pre-wrap; font-size: 12px;">{{ thinkingProcess }}</pre>
        </n-collapse-item>
      </n-collapse>
    </div>

    <!-- 状态 3: 提案已生成 -->
    <div v-if="hasValidProposal">
      <n-descriptions :column="1" bordered label-placement="top" size="small">
        <n-descriptions-item v-for="(value, key) in instruction.aiProposal" :key="key" :label="key">
          {{ value }}
        </n-descriptions-item>
      </n-descriptions>
      <n-space justify="end" style="margin-top: 12px;">
        <!-- TODO "重试"按钮需要一个 ref 来获取上次选择的工作流，但是实际上根本没有做 -->
        <WorkflowSelectorButton
            ref="retryButtonRef"
            :expected-outputs="['context']"
            :filter="workflowFilter"
            :storage-key="workflowStorageKey"
            @click="generate"/>
        <n-button size="small" type="primary" @click="applyProposal">应用</n-button>
      </n-space>
    </div>

    <!-- 状态 4: 已应用 -->
    <div v-if="instruction.status === 'APPLIED'">
      <n-log :log="`提案已成功应用到对象 ${targetObjectId}`"/>
    </div>

    <!-- 状态 5: 错误 -->
    <div v-if="instruction.status === 'ERROR'">
      <n-alert title="发生错误" type="error">
        {{ instruction.error }}
      </n-alert>
    </div>

  </n-card>
</template>

<script lang="ts" setup>
import {computed, ref, toRefs} from 'vue';
import {
  NAlert,
  NButton,
  NCard,
  NCollapse,
  NCollapseItem,
  NDescriptions,
  NDescriptionsItem,
  NIcon,
  NInput,
  NLog,
  NProgress,
  NSpace,
  NText
} from 'naive-ui';
import {Close as CloseIcon} from '@vicons/ionicons5';
import {useIntentComponent} from './useIntentComponent';
import {useWorldStateStore} from '#/stores/useWorldStateStore';
import {WorkflowSelectorButton} from '@yaesandbox-frontend/core-services/workflow'
import {type WorkflowFilter} from "@yaesandbox-frontend/core-services/composables";
import {type Instruction, InstructionType} from "#/components/creator/instruction.ts";

const props = defineProps<{
  instruction: Instruction;
}>();

const {instruction} = toRefs(props);
const worldState = useWorldStateStore();

const enrichObjectWorkflowFilter = ref<WorkflowFilter>({
  expectedInputs: ['user_prompt', 'object_type', 'existing_properties'],
  requiredTags: ['丰富对象'],
});

const initializeComponentWorkflowFilter = ref<WorkflowFilter>({
  expectedInputs: ['user_prompt', 'object_type', 'component_type'],
  requiredTags: ['属性组初始化'],
});

const workflowFilter = computed(() => instruction.value.type === InstructionType.INITIALIZE_COMPONENT
    ? initializeComponentWorkflowFilter.value
    : enrichObjectWorkflowFilter.value
)


const {
  isGenerating,
  hasValidProposal,
  thinkingProcess,
  generate,
  applyProposal,
  discard,
} = useIntentComponent(instruction);

// --- UI 计算属性 ---
const workflowStorageKey = computed(() => `intent-component-workflow--${instruction.value.type}`);
const targetObjectId = computed(() => instruction.value.context.targetObjectId);
const targetObject = computed(() => worldState.logicalGameMap?.findObjectById(targetObjectId.value || ''));

const cardTitle = computed(() =>
{
  if (!targetObject.value) return `指令 ${instruction.value.id.slice(0, 4)}`;
  const objectInfo = `${targetObject.value.type} (${targetObject.value.id.slice(0, 4)})`;

  if (instruction.value.type === InstructionType.INITIALIZE_COMPONENT)
  {
    return `初始化组件 [${instruction.value.context.componentType}] for ${objectInfo}`;
  }
  return `丰富对象: ${objectInfo}`;
});
</script>