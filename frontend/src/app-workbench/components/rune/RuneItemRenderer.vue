<!-- src/app-workbench/components/.../RuneItemRenderer.vue -->
<template>
  <ConfigItemBase
      v-model:enabled="rune.enabled"
      :highlight-color-calculator="props.rune.runeType"
      :is-selected="isSelected"
      is-draggable
      @click="updateSelectedConfig"
  >
    <template #content>
<!--      <span v-if="runeClassLabel" class="rune-alias">{{ runeClassLabel }}</span>-->
      <span class="rune-name">{{ rune.name }}</span>
    </template>

    <template #actions>
      <n-popover
          v-if="runeAnalysisResult && (hasConsumedVariables || hasProducedVariables)"
          trigger="hover">
        <template #trigger>
          <n-icon :component="FindInPageIcon" style="color: #999; margin-left: 8px;"/>
        </template>
        <div v-if="hasConsumedVariables">
          <n-h4>输入:</n-h4>
          <n-ul>
            <n-li v-for="variable in runeAnalysisResult.consumedVariables" :key="variable">{{ variable }}</n-li>
          </n-ul>
        </div>
        <div v-if="hasProducedVariables">
          <n-h4>输出:</n-h4>
          <n-ul>
            <n-li v-for="variable in runeAnalysisResult.producedVariables" :key="variable">{{ variable }}</n-li>
          </n-ul>
        </div>
      </n-popover>
      <n-popover v-if="rulesForThisRune" trigger="hover">
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
import type {AbstractRuneConfig, StepProcessorConfig} from '@/app-workbench/types/generated/workflow-config-api-client';
import {computed, inject, toRef} from "vue";
import {useWorkbenchStore} from "@/app-workbench/stores/workbenchStore.ts";
import {InfoIcon} from "naive-ui/lib/_internal/icons";
import ColorHash from "color-hash";
import {SelectedConfigItemKey} from "@/app-workbench/utils/injectKeys.ts";
import {FindInPageIcon} from "@/utils/icons.ts";
import {useRuneAnalysis} from "@/app-workbench/composables/useRuneAnalysis.ts";
import {useConfigItemActions} from "@/app-workbench/composables/useConfigItemActions.ts";
import ConfigItemActionsMenu from "@/app-workbench/components/share/ConfigItemActionsMenu.vue";

// 定义 Props 和 Emits
const props = defineProps<{
  rune: AbstractRuneConfig;
  parentStep: StepProcessorConfig | null;
}>();


const {
  analysisResult: runeAnalysisResult,
  hasConsumedVariables,
  hasProducedVariables
} = useRuneAnalysis(
    computed(() => props.rune),
    computed(() => props.rune.configId)
);

const selectedConfigItem = inject(SelectedConfigItemKey);

function updateSelectedConfig()
{
  selectedConfigItem?.update({data: props.rune});
}

const selectedConfig = selectedConfigItem?.data;

const isSelected = computed(() =>
{
  return selectedConfig?.value?.data.configId === props.rune.configId;
});

const workbenchStore = useWorkbenchStore();

// 获取当前符文的元数据
const metadataForThisRune = computed(() =>
    workbenchStore.runeMetadata[props.rune.runeType]
);

const rulesForThisRune = computed(() => metadataForThisRune.value?.rules);

const runeClassLabel = computed(() => metadataForThisRune.value?.classLabel);

const {actions: itemActions} = useConfigItemActions({
  itemRef: toRef(props, 'rune'),
  parentContextRef: computed(() =>
      props.parentStep
          ? {parent: props.parentStep, list: props.parentStep.runes}
          : null
  ),
});

/**
 * 辅助函数：根据符文类型名获取其别名
 * @param runeType - 符文的原始类型名，例如 "PromptGenerationRuneConfig"
 * @returns 符文的别名，如果不存在则返回原始类型名
 */
function getRuneAlias(runeType: string): string
{
  const metadata = workbenchStore.runeMetadata[runeType];
  const schema = workbenchStore.runeSchemasAsync.state[runeType];
  return metadata?.classLabel || schema?.title || runeType;
}

// 将规则对象转换为用户可读的文本描述
const ruleDescriptions = computed(() =>
{
  const descriptions: string[] = [];
  const rules = rulesForThisRune.value;
  if (!rules) return descriptions;

  if (rules.noConfig) descriptions.push('此符文不能有配置。')
  if (rules.singleInStep) descriptions.push('此符文在每个步骤中只能使用一次。');
  if (rules.inLastStep) descriptions.push('此符文必须位于工作流的最后一个步骤中。');
  if (rules.inFrontOf && rules.inFrontOf.length > 0)
  {
    const aliases = rules.inFrontOf.map(getRuneAlias).join('、');
    descriptions.push(`必须位于「${aliases}」符文之前。`);
  }
  if (rules.behind && rules.behind.length > 0)
  {
    const aliases = rules.behind.map(getRuneAlias).join('、');
    descriptions.push(`必须位于「${aliases}」符文之后。`);
  }

  return descriptions;
});

</script>

<!-- 这个组件不需要自己的 style 标签，因为它只是 ConfigItemBase 的一个薄封装，样式由 ConfigItemBase 提供 -->