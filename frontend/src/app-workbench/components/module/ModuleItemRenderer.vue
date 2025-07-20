<!-- src/app-workbench/components/.../ModuleItemRenderer.vue -->
<template>
  <ConfigItemBase
      v-model:enabled="module.enabled"
      :highlight-color-calculator="props.module.moduleType"
      :is-selected="isSelected"
      is-draggable
      @click="updateSelectedConfig"
  >
    <template #content>
      <span v-if="moduleClassLabel" class="module-alias">{{ moduleClassLabel }}</span>
      <span class="module-name">{{ module.name }}</span>
    </template>

    <template #actions>
      <n-popover
          v-if="moduleAnalysisResult && (hasConsumedVariables || hasProducedVariables)"
          trigger="hover">
        <template #trigger>
          <n-icon :component="FindInPageIcon" style="color: #999; margin-left: 8px;"/>
        </template>
        <div v-if="hasConsumedVariables">
          <n-h4>输入:</n-h4>
          <n-ul>
            <n-li v-for="variable in moduleAnalysisResult.consumedVariables" :key="variable">{{ variable }}</n-li>
          </n-ul>
        </div>
        <div v-if="hasProducedVariables">
          <n-h4>输出:</n-h4>
          <n-ul>
            <n-li v-for="variable in moduleAnalysisResult.producedVariables" :key="variable">{{ variable }}</n-li>
          </n-ul>
        </div>
      </n-popover>
      <n-popover v-if="rulesForThisModule" trigger="hover">
        <template #trigger>
          <n-icon :component="InfoIcon" style="color: #999; margin-left: 8px;"/>
        </template>
        <!-- 动态生成提示内容 -->
        <n-ul>
          <n-li v-for="ruleText in ruleDescriptions" :key="ruleText">{{ ruleText }}</n-li>
        </n-ul>
      </n-popover>

      <!-- "更多" 操作的下拉菜单 -->
      <ConfigItemActionsMenu :actions="itemActions"/>
    </template>
  </ConfigItemBase>
</template>

<script lang="ts" setup>
import ConfigItemBase from '@/app-workbench/components/share/renderer/ConfigItemBase.vue';
import type {AbstractModuleConfig, StepProcessorConfig} from '@/app-workbench/types/generated/workflow-config-api-client';
import {computed, inject, toRef} from "vue";
import {useWorkbenchStore} from "@/app-workbench/stores/workbenchStore.ts";
import {InfoIcon} from "naive-ui/lib/_internal/icons";
import ColorHash from "color-hash";
import {SelectedConfigItemKey} from "@/app-workbench/utils/injectKeys.ts";
import {FindInPageIcon} from "@/utils/icons.ts";
import {useModuleAnalysis} from "@/app-workbench/composables/useModuleAnalysis.ts";
import {useConfigItemActions} from "@/app-workbench/composables/useConfigItemActions.ts";
import ConfigItemActionsMenu from "@/app-workbench/components/share/ConfigItemActionsMenu.vue";

// 定义 Props 和 Emits
const props = defineProps<{
  module: AbstractModuleConfig;
  parentStep: StepProcessorConfig | null;
}>();


const {
  analysisResult: moduleAnalysisResult,
  hasConsumedVariables,
  hasProducedVariables
} = useModuleAnalysis(
    computed(() => props.module),
    computed(() => props.module.configId)
);

const selectedConfigItem = inject(SelectedConfigItemKey);

function updateSelectedConfig()
{
  selectedConfigItem?.update({data: props.module});
}

const selectedConfig = selectedConfigItem?.data;

const isSelected = computed(() =>
{
  return selectedConfig?.value?.data.configId === props.module.configId;
});

const workbenchStore = useWorkbenchStore();

// 获取当前模块的元数据
const metadataForThisModule = computed(() =>
    workbenchStore.moduleMetadata[props.module.moduleType]
);

const rulesForThisModule = computed(() => metadataForThisModule.value?.rules);

const moduleClassLabel = computed(() => metadataForThisModule.value?.classLabel);

const {actions: itemActions} = useConfigItemActions({
  itemRef: toRef(props, 'module'),
  parentContextRef: computed(() =>
      props.parentStep
          ? {parent: props.parentStep, list: props.parentStep.modules}
          : null
  ),
});

/**
 * 辅助函数：根据模块类型名获取其别名
 * @param moduleType - 模块的原始类型名，例如 "PromptGenerationModuleConfig"
 * @returns 模块的别名，如果不存在则返回原始类型名
 */
function getModuleAlias(moduleType: string): string
{
  const metadata = workbenchStore.moduleMetadata[moduleType];
  const schema = workbenchStore.moduleSchemasAsync.state[moduleType];
  return metadata?.classLabel || schema?.title || moduleType;
}

// 将规则对象转换为用户可读的文本描述
const ruleDescriptions = computed(() =>
{
  const descriptions: string[] = [];
  const rules = rulesForThisModule.value;
  if (!rules) return descriptions;

  if (rules.noConfig) descriptions.push('此模块不能有配置。')
  if (rules.singleInStep) descriptions.push('此模块在每个步骤中只能使用一次。');
  if (rules.inLastStep) descriptions.push('此模块必须位于工作流的最后一个步骤中。');
  if (rules.inFrontOf && rules.inFrontOf.length > 0)
  {
    const aliases = rules.inFrontOf.map(getModuleAlias).join('、');
    descriptions.push(`必须位于「${aliases}」模块之前。`);
  }
  if (rules.behind && rules.behind.length > 0)
  {
    const aliases = rules.behind.map(getModuleAlias).join('、');
    descriptions.push(`必须位于「${aliases}」模块之后。`);
  }

  return descriptions;
});

</script>

<!-- 这个组件不需要自己的 style 标签，因为它只是 ConfigItemBase 的一个薄封装，样式由 ConfigItemBase 提供 -->