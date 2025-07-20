<!-- src/app-workbench/components/.../ModuleEditor.vue -->
<template>
  <div class="editor-target-renderer">
    <div v-if="module && selectedModuleSchema">
      <n-flex justify="space-between" align="center" style="margin-bottom: 16px;">
        <div>
          <n-h4>配置模块: {{ module.name }}</n-h4>
          <n-p depth="3" style="margin-top: -8px;">
            模块类型: {{ moduleTypeLabel }}
          </n-p>
        </div>
        <n-form-item label="启用此模块" label-placement="left" style="margin-bottom: 0;">
          <n-switch v-model:value="module.enabled" />
        </n-form-item>
      </n-flex>


      <n-card
          v-if="moduleAnalysisResult && hasConsumedVariables"
          :content-style="{padding:0.3}"
          :header-style="{padding:0.5}"
          embedded
          size="small"
          title="输入:"
      >
        <n-flex>
          <n-tag v-for="variable in moduleAnalysisResult.consumedVariables" :key="variable">{{ variable }}</n-tag>
        </n-flex>
      </n-card>

      <n-card
          v-if="moduleAnalysisResult && hasProducedVariables"
          :content-style="{padding:0.3}"
          :header-style="{padding:0.5}"
          embedded
          size="small"
          title="输出:"
      >
        <n-flex>
          <n-tag v-for="variable in moduleAnalysisResult.producedVariables" :key="variable">{{ variable }}</n-tag>
        </n-flex>
      </n-card>
      <n-divider/>

      <DynamicFormRenderer
          :key="module.configId"
          :model-value="module"
          :schema="selectedModuleSchema"
          @update:model-value="handleFormUpdate"
      />

    </div>
    <!-- 初始加载或未选中时的状态 -->
    <n-spin v-else-if="isLoadingSchema" description="正在加载模块配置模板..."/>
    <!-- 这个空状态理论上不会再显示，因为组件只在被选中时渲染 -->
    <n-empty v-else description="模块数据或模板未提供"/>
  </div>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {NEmpty, NH4, NP, NSpin} from 'naive-ui';
import {useWorkbenchStore} from "@/app-workbench/stores/workbenchStore.ts";
import type {AbstractModuleConfig} from "@/app-workbench/types/generated/workflow-config-api-client";
import DynamicFormRenderer from "@/app-workbench/features/schema-viewer/DynamicFormRenderer.vue";
import {useDebounceFn} from "@vueuse/core";
import type {ModuleEditorContext} from "@/app-workbench/components/module/editor/ModuleEditorContext.ts";
import {useModuleAnalysis} from "@/app-workbench/composables/useModuleAnalysis.ts";

// --- Props ---
const props = defineProps<{
  moduleContext: ModuleEditorContext;
}>();

const {
  analysisResult: moduleAnalysisResult,
  hasConsumedVariables,
  hasProducedVariables
} = useModuleAnalysis(
    computed(() => props.moduleContext.data),
    computed(() => props.moduleContext.data.configId)
);


const workbenchStore = useWorkbenchStore();

const moduleSchemas = computed(() => workbenchStore.moduleSchemasAsync.state)
const isLoadingSchema = computed(() => workbenchStore.moduleSchemasAsync.isLoading);
const module = computed(() => props.moduleContext.data);
/**
 * 计算属性，用于获取模块类型的显示标签。
 */
const moduleTypeLabel = computed(() =>
{
  if (!module.value) return '';
  const moduleType = module.value.moduleType;
  const metadata = workbenchStore.moduleMetadata[moduleType];
  return metadata?.classLabel || moduleType;
});

// --- selectedModule 的计算属性被移除，因为我们直接使用 props.module ---

// 计算属性：根据选中的模块类型，从 store 中获取对应的 schema
const selectedModuleSchema = computed(() =>
{
  // 直接使用 props.module 来获取 schema-viewer
  if (!module.value || !moduleSchemas.value) return null;
  return moduleSchemas.value[module.value.moduleType] || null;
});

/**
 * 当表单数据变化时，直接更新传入的模块对象。
 * @param updatedModuleData - 从 DynamicFormRenderer 返回的完整、更新后的模块对象。
 */
function handleFormUpdateRaw(updatedModuleData: AbstractModuleConfig)
{
  if (module.value)
  {
    // Object.assign 会直接修改 props.module 的属性，
    // 由于它是父组件状态树的一部分，Vue 的响应式系统会检测到变化。
    Object.assign(module.value, updatedModuleData);
  }
  else
  {
    console.error("更新失败：无法在当前会话中找到选中的模块。");
  }
}

// 创建一个防抖版本的更新函数，延迟 300 毫秒执行
const handleFormUpdate = useDebounceFn(handleFormUpdateRaw, 300);
</script>

<style scoped>
.editor-target-renderer {
  /* 从卡片样式改为更融合的背景 */
  background-color: #fdfdfd;
  padding: 16px; /* 调整内边距 */
  border: 1px solid #f0f0f0;
  border-top: none; /* 移除上边框，与上面的模块项更好地融合 */
  border-radius: 0 0 4px 4px; /* 只保留下方的圆角 */
  box-sizing: border-box;
}
</style>