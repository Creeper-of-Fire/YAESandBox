<!-- src/app-workbench/components/.../ModuleEditor.vue -->
<template>
  <div class="editor-target-renderer">
    <div v-if="module && selectedModuleSchema">
      <n-flex align="center" justify="space-between" style="margin-bottom: 16px;">
        <div>
          <n-h4>
            配置模块: {{ module.name }}
            <n-popover
                v-if="hasConsumedVariables || hasProducedVariables"
                :show="isPopoverVisible"
                :style="{maxWidth: '400px'}"
                :trigger="'manual'"
                placement="right-start"
                @clickoutside="handleClickOutside"
            >
              <template #trigger>
                <n-button
                    :focusable="false"
                    circle
                    :type="isPopoverPinned ? 'info' : 'default'"
                    style="margin-left: 8px;"
                    text
                    @mouseenter="handleMouseEnter"
                    @mouseleave="handleMouseLeave"
                    @click.stop="handleTriggerClick"
                >
                  <template #icon>
                    <n-icon>
                      <InfoIcon/>
                    </n-icon>
                  </template>
                </n-button>
              </template>

              <!-- 给 Popover 的内容根元素也绑定上相同的事件监听器 -->
              <div
                  @mouseenter="handleMouseEnter"
                  @mouseleave="handleMouseLeave"
              >

                <n-flex size="small" vertical>
                  <div v-if="moduleAnalysisResult &&hasConsumedVariables">
                    <strong>输入变量:</strong>
                    <n-flex style="margin-top: 4px;">
                      <n-tag v-for="variable in moduleAnalysisResult.consumedVariables" :key="variable">{{ variable }}</n-tag>
                    </n-flex>
                  </div>
                  <div v-if="moduleAnalysisResult &&hasProducedVariables">
                    <strong>输出变量:</strong>
                    <n-flex style="margin-top: 4px;">
                      <n-tag v-for="variable in moduleAnalysisResult.producedVariables" :key="variable">{{ variable }}</n-tag>
                    </n-flex>
                  </div>
                </n-flex>
              </div>
            </n-popover>
          </n-h4>
          <n-p depth="3" style="margin-top: -8px;">
            模块类型: {{ moduleTypeLabel }}
          </n-p>
        </div>
        <n-form-item label="启用此模块" label-placement="left" style="margin-bottom: 0;">
          <n-switch v-model:value="module.enabled"/>
        </n-form-item>
      </n-flex>

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
import {computed, ref} from 'vue';
import {NEmpty, NH4, NP, NSpin} from 'naive-ui';
import {useWorkbenchStore} from "@/app-workbench/stores/workbenchStore.ts";
import type {AbstractModuleConfig} from "@/app-workbench/types/generated/workflow-config-api-client";
import DynamicFormRenderer from "@/app-workbench/features/schema-viewer/DynamicFormRenderer.vue";
import {useDebounceFn} from "@vueuse/core";
import {InfoIcon} from "naive-ui/lib/_internal/icons";
import type {ModuleEditorContext} from "@/app-workbench/components/module/editor/ModuleEditorContext.ts";
import {useModuleAnalysis} from "@/app-workbench/composables/useModuleAnalysis.ts";

// --- Props ---
const props = defineProps<{
  moduleContext: ModuleEditorContext;
}>();

// --- 状态 ---
const isPopoverVisible = ref(false); // 总开关
const isPopoverPinned = ref(false);  // 固定状态

let hideTimer: number | null = null; // 延迟隐藏定时器

// 核心函数：当鼠标进入任何一个“安全区域”（按钮或浮层）时调用
function handleMouseEnter()
{
  if (hideTimer)
  {
    clearTimeout(hideTimer);
    hideTimer = null;
  }
  isPopoverVisible.value = true;
}

// 核心函数：当鼠标离开任何一个“安全区域”时调用
function handleMouseLeave()
{
  // 如果已经被用户点击固定，则什么都不做
  if (isPopoverPinned.value)
  {
    return;
  }
  // 启动一个延迟隐藏
  hideTimer = window.setTimeout(() =>
  {
    isPopoverVisible.value = false;
  }, 200);
}

function handleTriggerClick() {
  // 清除任何可能存在的隐藏定时器，确保点击后不会意外关闭
  if (hideTimer) {
    clearTimeout(hideTimer);
    hideTimer = null;
  }

  // 点击的作用只有一个：将 Popover 的状态设置为“已固定”
  // 如果它已经是固定的，则这次点击不执行任何状态变更。
  if (!isPopoverPinned.value) {
    isPopoverPinned.value = true;
  }

  // 确保 Popover 在点击后是可见的
  isPopoverVisible.value = true;
}

function handleClickOutside()
{
  // 只有在固定的情况下，点击外部才生效
  if (isPopoverPinned.value)
  {
    hideTimer = window.setTimeout(() =>
    {
      isPopoverPinned.value = false;
      isPopoverVisible.value = false;
    }, 100);
  }
}

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