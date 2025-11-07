<template>
  <div class="editor-target-renderer">
    <div v-if="rune && selectedRuneSchema">
      <!-- 头部信息 -->
      <n-flex align="center" justify="space-between" style="margin-bottom: 12px;">
        <div>
          <n-h4 style="margin-bottom: 4px;">
            配置符文: {{ rune.name }}
          </n-h4>
          <n-p depth="3" style="margin: 0;">
            符文类型: {{ runeTypeLabel }}
          </n-p>
        </div>
        <!-- 头部右侧按钮组 -->
        <n-flex align="center">
          <n-button @click="showTestModal = true">测试符文</n-button>
          <n-text style="margin-left: 16px; margin-right: 8px;">启用此符文</n-text>
          <n-switch v-model:value="rune.enabled"/>
        </n-flex>
      </n-flex>

      <!-- 变量显示区 -->
      <n-blockquote
          v-if="hasConsumedVariables || hasProducedVariables"
          class="variable-display-box"
      >
        <n-flex size="small" vertical>
          <div v-if="runeAnalysisResult && hasConsumedVariables">
            <strong>输入变量:</strong>
            <n-flex :wrap="true" style="margin-top: 4px;">
              <VarWithSpecTag
                  v-for="spec in runeAnalysisResult.consumedVariables"
                  :key="spec.name"
                  :is-optional="'isOptional' in spec ? spec.isOptional : undefined"
                  :spec-def="spec.def"
                  :tag-type="'isOptional' in spec ? (spec.isOptional ? 'default' : 'success') : 'info'"
                  :var-name="spec.name"
                  placement='top'
              />
            </n-flex>
          </div>
          <div v-if="runeAnalysisResult && hasProducedVariables">
            <strong>输出变量:</strong>
            <n-flex :wrap="true" style="margin-top: 4px;">
              <VarWithSpecTag
                  v-for="spec in runeAnalysisResult.producedVariables"
                  :key="spec.name"
                  :spec-def="spec.def"
                  :tag-type="'info'"
                  :var-name="spec.name"
                  placement='bottom'
              />
            </n-flex>
          </div>
        </n-flex>
      </n-blockquote>

      <!-- 动态表单渲染器 -->
      <DynamicFormRenderer
          :key="rune.configId"
          :model-value="rune"
          :schema="selectedRuneSchema"
          @update:model-value="handleFormUpdate"
      />

      <div v-if="hasInnerTuum && innerTuum && innerTuumContext" class="inner-tuum-editor-wrapper">
        <n-divider/>
        <n-h3>子枢机流程编辑器</n-h3>
        <TuumEditor
            :key="innerTuum.configId"
            :tuum-context="innerTuumContext"
        />
      </div>

    </div>
    <!-- 初始加载或未选中时的状态 -->
    <n-spin v-else-if="isLoadingSchema" description="正在加载符文配置模板..."/>
    <!-- 这个空状态理论上不会再显示，因为组件只在被选中时渲染 -->
    <n-empty v-else description="符文数据或模板未提供"/>

    <RuneTestModal
        v-if="rune"
        v-model:show="showTestModal"
        :rune="rune"
    />

  </div>
</template>

<script lang="ts" setup>
import {computed, ref} from 'vue';
import {NBlockquote, NEmpty, NFlex, NH4, NP, NSpin, NSwitch, NTag, useThemeVars} from 'naive-ui';
import {useWorkbenchStore} from "#/stores/workbenchStore.ts";
import type {AbstractRuneConfig, TuumConfig} from "#/types/generated/workflow-config-api-client";
import DynamicFormRenderer from "#/features/schema-viewer/DynamicFormRenderer.vue";
import {useDebounceFn} from "@vueuse/core";
import type {RuneEditorContext} from "#/components/rune/editor/RuneEditorContext.ts";
import {useRuneAnalysis} from "#/composables/useRuneAnalysis.ts";
import TuumEditor from "#/components/tuum/editor/TuumEditor.vue";
import type {TuumEditorContext} from "#/components/tuum/editor/TuumEditorContext.ts";
import VarWithSpecTag from "#/components/share/varSpec/VarWithSpecTag.vue";
import RuneTestModal from "#/components/rune/test/RuneTestModal.vue";

// --- Props ---
const props = defineProps<{
  runeContext: RuneEditorContext;
}>();

const showTestModal = ref(false);

// 检查是否存在 innerTuum 属性
const hasInnerTuum = computed(() => 'innerTuum' in props.runeContext.data && !!props.runeContext.data.innerTuum);

const innerTuum = computed(() =>
{
  return hasInnerTuum.value ? (props.runeContext.data as any).innerTuum as TuumConfig : null;
});

// 为内部的 TuumEditor 构建上下文
const innerTuumContext = computed((): TuumEditorContext | null =>
{
  if (!innerTuum.value) return null;

  return {
    // 内部枢机的配置数据
    data: innerTuum.value,
  };
});


const rune = computed(() =>
{
  return props.runeContext.data;
})

const {
  analysisResult: runeAnalysisResult,
  hasConsumedVariables,
  hasProducedVariables
} = useRuneAnalysis(
    computed(() => rune.value)
);

const workbenchStore = useWorkbenchStore();

const runeSchemas = computed(() => workbenchStore.runeSchemasAsync.state)
const isLoadingSchema = computed(() => workbenchStore.runeSchemasAsync.isLoading);

/**
 * 计算属性，用于获取符文类型的显示标签。
 */
const runeTypeLabel = computed(() =>
{
  if (!rune.value) return '';
  const runeType = rune.value.runeType;
  const metadata = workbenchStore.runeMetadata[runeType];
  return metadata?.classLabel || runeType;
});

// 计算属性：根据选中的符文类型，从 store 中获取对应的 schema
const selectedRuneSchema = computed(() =>
{
  // 直接使用 props.rune 来获取 schema-viewer
  if (!rune.value || !runeSchemas.value) return null;
  return runeSchemas.value[rune.value.runeType] || null;
});

/**
 * 当表单数据变化时，直接更新传入的符文对象。
 * @param updatedRuneData - 从 DynamicFormRenderer 返回的完整、更新后的符文对象。
 */
function handleFormUpdateRaw(updatedRuneData: AbstractRuneConfig)
{
  if (rune.value)
  {
    const newData = updatedRuneData as any;

    // Object.assign 会直接修改 props.rune 的属性，
    // 由于它是父组件状态树的一部分，Vue 的响应式系统会检测到变化。
    Object.assign(rune.value, newData);
  }
  else
  {
    console.error("更新失败：无法在当前会话中找到选中的符文。");
  }
}

// 创建一个防抖版本的更新函数，延迟 300 毫秒执行
const handleFormUpdate = useDebounceFn(handleFormUpdateRaw, 300);

const themeVars = useThemeVars();
</script>

<style scoped>
.editor-target-renderer {
  /* 从卡片样式改为更融合的背景 */
  background-color: v-bind('themeVars.cardColor');
  padding: 16px; /* 调整内边距 */
  border: 1px solid v-bind('themeVars.borderColor');
  border-top: none; /* 移除上边框，与上面的符文项更好地融合 */
  border-radius: 0 0 4px 4px; /* 只保留下方的圆角 */
  box-sizing: border-box;
}

/* 新增的变量显示区样式 */
.variable-display-box {
  padding: 12px;
  margin-bottom: 16px;
  max-height: 140px; /* 设置一个最大高度 */
  overflow-y: auto; /* 当内容超出时，显示垂直滚动条 */
  background-color: v-bind('themeVars.actionColor'); /* 使用一个柔和的背景色以示区别 */
}
</style>