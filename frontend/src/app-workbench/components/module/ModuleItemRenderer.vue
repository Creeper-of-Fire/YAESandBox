<!-- src/app-workbench/components/.../ModuleItemRenderer.vue -->
<template>
  <ConfigItemBase
      :highlight-color="moduleTypeColor"
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
    </template>
  </ConfigItemBase>
</template>

<script lang="ts" setup>
import ConfigItemBase from '@/app-workbench/components/share/renderer/ConfigItemBase.vue';
import type {AbstractModuleConfig} from '@/app-workbench/types/generated/workflow-config-api-client';
import {computed, inject} from "vue";
import {useWorkbenchStore} from "@/app-workbench/stores/workbenchStore.ts";
import {InfoIcon} from "naive-ui/lib/_internal/icons";
import ColorHash from "color-hash";
import {SelectedConfigItemKey} from "@/app-workbench/utils/injectKeys.ts";
import {FindInPageIcon} from "@/utils/icons.ts";
import {useModuleAnalysis} from "@/app-workbench/composables/useModuleAnalysis.ts";

// 定义 Props 和 Emits
const props = defineProps<{
  module: AbstractModuleConfig;
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

const colorHash = new ColorHash({
  lightness: [0.7, 0.75, 0.8],
  saturation: [0.7, 0.8, 0.9],
  hash: 'bkdr'
});
const moduleTypeColor = computed(() =>
{
  return colorHash.hex(props.module.moduleType);
});

// 将规则对象转换为用户可读的文本描述
const ruleDescriptions = computed(() =>
{
  const descriptions: string[] = [];
  const rules = rulesForThisModule.value;
  if (!rules) return descriptions;

  if (rules.noConfig) descriptions.push('此模块不能有配置。')
  if (rules.singleInStep) descriptions.push('此模块在每个步骤中只能使用一次。');
  if (rules.inLastStep) descriptions.push('此模块必须位于工作流的最后一个步骤中。');
  if (rules.inFrontOf) descriptions.push(`必须位于 ${rules.inFrontOf.join(', ')} 模块之前。`);
  if (rules.behind) descriptions.push(`必须位于 ${rules.behind.join(', ')} 模块之后。`);

  return descriptions;
});

</script>

<!-- 这个组件不需要自己的 style 标签，因为它只是 ConfigItemBase 的一个薄封装，样式由 ConfigItemBase 提供 -->