<!-- src/app-workbench/components/.../RuneItemRenderer.vue -->
<template>
  <ConfigItemBase
      v-model:enabled="rune.enabled"
      :highlight-color-calculator="props.rune.runeType"
      :icon = "runeIcon"
      :is-selected="isSelected"
      is-draggable
      @click="handleItemClick"
      @dblclick="handleDoubleClick"
  >
    <template #content="{ titleClass }">
      <!-- 折叠按钮，仅当为 TuumRune 时显示 -->
      <n-button v-if="hasInnerTuum" :focusable="false" text @click.stop="toggleExpansion">
        <template #icon>
          <n-icon :component="isExpanded ? KeyboardArrowUpIcon : KeyboardArrowDownIcon"/>
        </template>
      </n-button>

      <span :class="titleClass" class="rune-name">{{ rune.name }}</span>
    </template>

    <template #actions>
      <ValidationStatusIndicator v-if="validationInfo" :validation-info="validationInfo"/>

      <n-popover v-if="rulesForThisRune" trigger="hover">
        <template #trigger>
          <n-icon :color="normalIconColor" :component="InfoIcon"/>
        </template>
        <!-- 动态生成提示内容 -->
        <n-ul>
          <n-li v-for="ruleText in ruleDescriptions" :key="ruleText">{{ ruleText }}</n-li>
        </n-ul>
      </n-popover>

      <n-popover
          :disabled="!showAnalysisPopover"
          trigger="hover">
        <template #trigger>
          <n-icon :color="analysisIconColor" :component="FindInPageIcon"/>
        </template>
        <div v-if="hasConsumedVariables&&runeAnalysisResult">
          输入:
          <div v-for="variable in runeAnalysisResult.consumedVariables" :key="variable.name">
            <VarWithSpecTag :is-optional="variable.isOptional" :spec-def="variable.def" :var-name="variable.name"/>
          </div>
        </div>
        <div v-if="hasProducedVariables&&runeAnalysisResult">
          输出:
          <div v-for="variable in runeAnalysisResult.producedVariables" :key="variable.name">
            <VarWithSpecTag :spec-def="variable.def" :var-name="variable.name"/>
          </div>
        </div>
      </n-popover>

      <!-- "更多" 操作的下拉菜单 -->
      <ConfigItemActionsMenu :actions="itemActions"/>
    </template>
  </ConfigItemBase>

  <!-- 当存在 innerTuum 时，复用 CollapsibleConfigList -->
  <n-collapse-transition v-if="hasInnerTuum" :show="isExpanded">
    <CollapsibleConfigList
        v-if="innerTuum"
        v-model:items="innerTuum.runes"
        empty-description="拖拽符文到此处"
        group-name="runes-group"
    >
      <template #item="{ element: runeItem }">
        <!-- 递归渲染 RuneItemRenderer，并传递禁用状态 -->
        <RuneItemRenderer
            :parent-tuum="innerTuum"
            :rune="runeItem"
        />
      </template>
    </CollapsibleConfigList>
  </n-collapse-transition>
</template>

<script lang="ts" setup>
import ConfigItemBase from '#/components/share/renderer/ConfigItemBase.vue';
import type {AbstractRuneConfig, TuumConfig} from '#/types/generated/workflow-config-api-client';
import {computed, inject, provide, ref, toRef} from "vue";
import {useWorkbenchStore} from "#/stores/workbenchStore.ts";
import {IsParentDisabledKey} from "#/utils/injectKeys.ts";
import {FindInPageIcon, InfoIcon, KeyboardArrowDownIcon, KeyboardArrowUpIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {useRuneAnalysis} from "#/composables/useRuneAnalysis.ts";
import {useConfigItemActions} from "#/composables/useConfigItemActions.ts";
import ConfigItemActionsMenu from "#/components/share/ConfigItemActionsMenu.vue";
import {useThemeVars} from "naive-ui";
import CollapsibleConfigList from "#/components/share/renderer/CollapsibleConfigList.vue";
import {useSelectedConfig} from "#/services/editor-context/useSelectedConfig.ts";
import ValidationStatusIndicator from "#/components/share/validationInfo/ValidationStatusIndicator.vue";
import {useValidationInfo} from "#/components/share/validationInfo/useValidationInfo.ts";
import VarWithSpecTag from "#/components/share/varSpec/VarWithSpecTag.vue";

// 定义 Props 和 Emits
const props = defineProps<{
  rune: AbstractRuneConfig;
  parentTuum: TuumConfig | null;
  isParentDisabled?: boolean;
}>();

const {
  analysisResult: runeAnalysisResult,
  hasConsumedVariables,
  hasProducedVariables
} = useRuneAnalysis(
    computed(() => props.rune)
);
const messagesRef = computed(() => runeAnalysisResult.value?.runeMessages);
const {validationInfo} = useValidationInfo(messagesRef);

// 创建一个计算属性来控制分析结果 Popover 的显示
const showAnalysisPopover = computed(() =>
    !!runeAnalysisResult.value && (hasConsumedVariables.value || hasProducedVariables.value)
);

// 使用 Naive UI 的 useThemeVars hook
const themeVars = useThemeVars();

const normalIconColor = computed(() => themeVars.value.textColor2); // 正常状态的颜色
const dimIconColor = computed(() => themeVars.value.textColor3);    // 暗淡状态的颜色

// 为分析图标创建一个动态的颜色计算属性
const analysisIconColor = computed(() =>
{
  // 如果有输入或输出变量信息，使用正常颜色；否则，使用暗淡颜色。
  return showAnalysisPopover.value ? normalIconColor.value : dimIconColor.value;
});

const selfId = computed(() => props.rune.configId);
const {updateSelectedConfig, isSelected} = useSelectedConfig(selfId);

function handleItemClick()
{
  updateSelectedConfig(props.rune);
}

const workbenchStore = useWorkbenchStore();

// 获取当前符文的元数据
const metadataForThisRune = computed(() =>
    workbenchStore.runeMetadata[props.rune.runeType]
);

const rulesForThisRune = computed(() => metadataForThisRune.value?.rules);

const runeIcon = computed(() => metadataForThisRune.value?.icon);

const runeClassLabel = computed(() => metadataForThisRune.value?.classLabel);

const {actions: itemActions} = useConfigItemActions({
  itemRef: toRef(props, 'rune'),
  parentContextRef: computed(() =>
      props.parentTuum
          ? {parent: props.parentTuum, list: props.parentTuum.runes}
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
  if (rules.singleInTuum) descriptions.push('此符文在每个枢机中只能使用一次。');
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


// --- InnerTuum部分 ---

// 1. **约定检测**：检查此符文是否为 TuumRune
const hasInnerTuum = computed(() => 'innerTuum' in props.rune && !!(props.rune as any).innerTuum);
const innerTuum = computed(() => hasInnerTuum.value ? (props.rune as any).innerTuum as TuumConfig : null);

// 2. 状态：控制内部列表的展开/折叠
const isExpanded = ref(true);

// 3. 方法：处理双击和按钮点击事件
function handleDoubleClick()
{
  if (hasInnerTuum.value)
  {
    isExpanded.value = !isExpanded.value;
  }
}

function toggleExpansion()
{
  if (hasInnerTuum.value)
  {
    isExpanded.value = !isExpanded.value;
  }
}

// 1. 注入来自父容器（Tuum 或另一个 Rune）的禁用状态
// 提供一个 ref(false) 作为默认值，用于顶层符文或在上下文中找不到提供者的情况
const isParentDisabled = inject(IsParentDisabledKey, ref(false));

// 2. 计算出此符文最终的有效禁用状态
const isEffectivelyDisabled = computed(() =>
{
  // 自身的禁用状态 OR 父容器的禁用状态
  return !props.rune.enabled || isParentDisabled.value;
});

// 3. (关键的递归步骤) 为自己的后代提供其自身的“有效禁用状态”
// 这样，禁用链就可以无限传递下去
provide(IsParentDisabledKey, isEffectivelyDisabled);

</script>