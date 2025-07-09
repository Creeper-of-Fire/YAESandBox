<!-- src/app-workbench/components/editor/StepMappingsEditor.vue -->
<template>
  <!-- TODO StepMappingsEditor的逻辑存在问题，availableGlobalVarsForStep应当最多是用于舒适提示的，而不是仅能使用availableGlobalVarsForStep作为映射，这违背了我们的“弱检测/后端检测”原则 -->
  <div class="step-mappings-editor">
    <n-card :bordered="true" size="small" title="输入映射">
      <!-- 1. 未满足的输入 (错误警告) -->
      <n-alert v-if="missingInputs.length > 0" :show-icon="true" title="缺少必要的输入" type="error">
        <p>此步骤中的模块需要以下输入，但尚未配置映射：</p>
        <n-tag v-for="input in missingInputs" :key="input" style="margin-right: 8px; margin-top: 4px;" type="error">
          {{ input }}
        </n-tag>
      </n-alert>

      <!-- 2. 自定义和默认映射列表 -->
      <div v-for="(local, global) in inputMappings" :key="global" class="mapping-item">
        <span class="global-var">{{ global }}</span>
        <n-icon class="arrow-icon">
          <ArrowForwardIcon/>
        </n-icon>
        <span class="local-var">{{ local }}</span>

        <!-- TODO: 添加编辑和删除按钮 -->
        <n-button-group size="tiny" style="margin-left: auto;">
          <n-button circle>
            <template #icon>
              <n-icon :component="EditIcon"/>
            </template>
          </n-button>
          <n-button circle type="error">
            <template #icon>
              <n-icon :component="DeleteIcon"/>
            </template>
          </n-button>
        </n-button-group>
      </div>

      <!-- TODO: "添加映射" 按钮 -->
      <n-button block dashed style="margin-top: 12px;">添加输入映射</n-button>

    </n-card>

    <n-card :bordered="true" size="small" style="margin-top: 16px;" title="输出映射">
      <!-- 输出映射的实现相对简单，可以先用占位符 -->
      <n-empty description="输出映射待实现"/>
    </n-card>
  </div>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {NAlert, NButton, NButtonGroup, NCard, NEmpty, NIcon, NTag} from 'naive-ui';
import {ArrowForwardIcon, DeleteIcon, EditIcon} from '@/utils/icons.ts';

const props = defineProps<{
  // 使用 v-model 来双向绑定映射数据
  inputMappings: Record<string, string>;
  outputMappings: Record<string, string>;

  // 从父组件计算好的上下文信息
  requiredInputs: string[]; // 所有模块需要的输入变量集合
  availableGlobalVars?: string[]; // 此步骤可用的全局变量集合，如果为undefined代表此时不存在上下文
}>();

const emit = defineEmits(['update:inputMappings', 'update:outputMappings']);

// 计算属性：找出哪些必需的输入还没有被映射
const missingInputs = computed(() =>
{
  const mappedLocals = Object.values(props.inputMappings);
  return props.requiredInputs.filter(req => !mappedLocals.includes(req));
});


// TODO: 实现编辑、删除、添加映射的逻辑函数
</script>

<style scoped>
.step-mappings-editor {
  margin-bottom: 12px;
  background-color: #fcfcfd;
  padding: 12px;
  border-radius: 4px;
  border: 1px solid #f0f0f0;
}

.mapping-item {
  display: flex;
  align-items: center;
  padding: 4px 0;
  font-family: monospace;
  font-size: 13px;
}

.arrow-icon {
  margin: 0 12px;
  color: #aaa;
}

.global-var {
  color: #2080f0; /* Naive 主题蓝 */
  font-weight: 500;
}

.local-var {
  color: #18a058; /* Naive 主题绿 */
}
</style>