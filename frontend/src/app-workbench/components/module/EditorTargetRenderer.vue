<template>
  <div class="editor-target-renderer">
    <div v-if="selectedModule && selectedModuleSchema">
      <n-h4>配置模块: {{ selectedModule.name }}</n-h4>
      <n-p depth="3" style="margin-top: -8px; margin-bottom: 24px;">
        模块类型: {{ moduleTypeLabel }}
      </n-p>

      <!-- 这里放置 DynamicFormRenderer 组件 -->
      <DynamicFormRenderer
          :key="selectedModule.configId"
          :schema="selectedModuleSchema"
          :model-value="selectedModule"
          @update:model-value="handleFormUpdate"
      />

    </div>
    <!-- 初始加载或未选中时的状态 -->
    <n-spin v-else-if="isLoadingSchema" description="正在加载模块配置模板..."/>
    <n-empty v-else description="请在左侧结构树中选择一个模块进行配置"/>
  </div>
</template>

<script setup lang="ts">
import {computed} from 'vue';
import {NEmpty, NH4, NP} from 'naive-ui';
import type {EditSession} from "@/app-workbench/services/EditSession.ts";
import {useWorkbenchStore} from "@/app-workbench/stores/workbenchStore.ts";
import type {AbstractModuleConfig} from "@/app-workbench/types/generated/workflow-config-api-client";
import DynamicFormRenderer from "@/app-workbench/features/schema-viewer/DynamicFormRenderer.vue";
import {useDebounceFn} from "@vueuse/core";

const props = defineProps<{
  session: EditSession;
  selectedModuleId: string | null;
}>();

const workbenchStore = useWorkbenchStore();

const moduleSchemas = computed(() =>  workbenchStore.moduleSchemasAsync.state)
const isLoadingSchema = computed(() => workbenchStore.moduleSchemasAsync.isLoading);


/**
 * 计算属性，用于获取模块类型的显示标签。
 * 它会优先从元数据中查找 classLabel，如果不存在，则回退到显示原始的 moduleType。
 */
const moduleTypeLabel = computed(() =>
{
  if (!selectedModule.value) return '';
  const moduleType = selectedModule.value.moduleType;
  const metadata = workbenchStore.moduleMetadata[moduleType];
  return metadata?.classLabel || moduleType;
});

// 计算属性：根据 selectedModuleId 获取当前选中的模块对象
const selectedModule = computed<AbstractModuleConfig | null>(() => {
  if (!props.selectedModuleId || !props.session) return null;

  const draftData = props.session.getData().value;
  if (!draftData) return null;

  // 如果当前编辑的就是一个模块, 直接返回
  // @ts-ignore
  if (props.session.type === 'module' && draftData.configId === props.selectedModuleId) {
    return draftData as AbstractModuleConfig;
  }

  // 递归查找函数
  function findModuleIn(config: any): AbstractModuleConfig | null {
    if (!config) return null;

    if (config.configId === props.selectedModuleId && config.moduleType) {
      return config as AbstractModuleConfig;
    }

    // 检查 `steps` 属性 (用于 Workflow)
    if (config.steps && Array.isArray(config.steps)) {
      for (const step of config.steps) {
        const found = findModuleIn(step);
        if (found) return found;
      }
    }

    // 检查 `modules` 属性 (用于 Step)
    if (config.modules && Array.isArray(config.modules)) {
      for (const mod of config.modules) {
        const found = findModuleIn(mod);
        if (found) return found;
      }
    }

    return null;
  }

  return findModuleIn(draftData);
});

// 计算属性：根据选中的模块类型，从 store 中获取对应的 schema
const selectedModuleSchema = computed(() => {
  if (!selectedModule.value || !moduleSchemas.value) return null;
  return moduleSchemas.value[selectedModule.value.moduleType] || null;
});

/**
 * 当表单数据变化时，直接更新找到的模块对象。
 * @param updatedModuleData - 从 DynamicFormRenderer 返回的完整、更新后的模块对象。
 */
function handleFormUpdateRaw(updatedModuleData: AbstractModuleConfig) {
  if (selectedModule.value) {
    Object.assign(selectedModule.value, updatedModuleData);
  } else {
    console.error("更新失败：无法在当前会话中找到选中的模块。");
  }
}

// 创建一个防抖版本的更新函数，延迟 300 毫秒执行
// 这意味着只有在用户停止输入 300ms 后，才会真正去更新 Store
const handleFormUpdate = useDebounceFn(handleFormUpdateRaw, 300);
</script>

<style scoped>
.editor-target-renderer {
  background-color: #fff;
  padding: 24px;
  height: 100%;
  box-sizing: border-box;
  border-radius: 8px;
  overflow-y: auto; /* 允许表单内容滚动 */
}
</style>