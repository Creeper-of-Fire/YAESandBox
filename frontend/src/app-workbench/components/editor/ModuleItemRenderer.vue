<!-- src/app-workbench/components/.../ModuleItemRenderer.vue -->
<template>
  <ConfigItemBase
      :is-selected="selectedModuleId === module.configId"
      :highlight-color="moduleTypeColor"
      is-draggable
      @click="$emit('update:selectedModuleId', module.configId)"
  >
    <template #content>
      <span v-if="moduleClassLabel" class="module-alias">{{ moduleClassLabel }}</span>
      <span class="module-name">{{ module.name }}</span>
    </template>

    <template #actions>
      <!-- 新增一个信息图标，用于显示规则提示 -->
      <n-tooltip v-if="rulesForThisModule" trigger="hover">
        <template #trigger>
          <n-icon :component="InfoIcon" style="color: #999; margin-left: 8px;"/>
        </template>
        <!-- 动态生成提示内容 -->
        <div v-for="ruleText in ruleDescriptions" :key="ruleText">
          • {{ ruleText }}
        </div>
      </n-tooltip>
    </template>
  </ConfigItemBase>
</template>

<script lang="ts" setup>
import ConfigItemBase from './ConfigItemBase.vue';
import type {AbstractModuleConfig} from '@/app-workbench/types/generated/workflow-config-api-client';
import {computed} from "vue";
import {useWorkbenchStore} from "@/app-workbench/stores/workbenchStore.ts";
import {InfoIcon} from "naive-ui/lib/_internal/icons";
import ColorHash from "color-hash";

// 定义 Props 和 Emits
const props = defineProps<{
  module: AbstractModuleConfig;
  selectedModuleId: string | null;
}>();

defineEmits(['update:selectedModuleId']);

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