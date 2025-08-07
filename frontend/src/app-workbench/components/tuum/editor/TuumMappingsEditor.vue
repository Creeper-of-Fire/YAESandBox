<!-- src/app-workbench/components/editor/TuumMappingsEditor.vue -->
<template>
  <div class="tuum-mappings-editor">
    <!-- 输入映射 -->
    <n-card :bordered="true" size="small" title="输入映射">
      <template #header-extra>
        <n-button dashed size="small" @click="addInputMappingRow">
          <template #icon> <n-icon :component="AddIcon"/> </template>
          手动添加映射
        </n-button>
      </template>

      <!-- 1. 缺少必要输入的警告 -->
      <n-alert v-if="missingInputs.length > 0" :show-icon="true" title="缺少必要的输入映射" type="error">
        <p style="margin-top: 4px;">此枢机中的符文需要以下输入，但尚未配置映射来源：</p>
        <n-tag v-for="input in missingInputs" :key="input" style="margin-right: 8px; margin-top: 4px;" type="error">
          {{ input }}
        </n-tag>
      </n-alert>

      <!-- 2. 输入映射列表 -->
      <div class="mappings-list">
        <div v-for="row in inputMappingRows" :key="row.globalVar" class="mapping-row">
          <div class="mapping-key">
            <n-auto-complete
                :value="row.globalVar"
                :options="availableGlobalVarsOptions"
                placeholder="全局变量 (来源)"
                @update:value="newValue => handleUpdateInputKey(row.globalVar, newValue)"
            />
            <n-tooltip v-if="duplicateInputGlobals.has(row.globalVar)">
              <template #trigger>
                <n-icon :component="AlertCircleIcon" color="#d03050" class="status-icon"/>
              </template>
              全局变量来源重复，这将导致映射冲突。
            </n-tooltip>
          </div>
          <n-icon :component="ArrowForwardIcon" class="arrow-icon"/>
          <div class="mapping-value">
            <n-dynamic-tags
                :value="row.localVars"
                @update:value="(newValues: string[]) => handleUpdateInputValues(row.globalVar, newValues)"
            >
              <template #input="{ submit, deactivate }">
                <n-auto-complete
                    :options="requiredInputsOptions"
                    size="small"
                    @select="submit($event)"
                    @blur="deactivate"
                />
              </template>
            </n-dynamic-tags>
          </div>
          <n-button text type="error" @click="deleteInputMappingRow(row.globalVar)" class="delete-button">
            <template #icon> <n-icon :component="DeleteIcon"/> </template>
          </n-button>
        </div>
        <n-empty v-if="inputMappingRows.length === 0" description="暂无输入映射" class="empty-placeholder"/>
      </div>
    </n-card>

    <!-- 输出映射 -->
    <n-card :bordered="true" size="small" style="margin-top: 16px;" title="输出映射">
      <template #header-extra>
        <n-button dashed size="small" @click="addOutputMappingRow">
          <template #icon> <n-icon :component="AddIcon"/> </template>
          添加输出映射
        </n-button>
      </template>

      <!-- 输出映射列表 -->
      <div class="mappings-list">
        <div v-for="row in outputMappingRows" :key="row.localVar" class="mapping-row">
          <div class="mapping-key">
            <n-auto-complete
                :value="row.localVar"
                :options="producibleOutputsOptions"
                placeholder="内部变量 (来源)"
                @update:value="newValue => handleUpdateOutputKey(row.localVar, newValue)"
            />
            <n-tooltip v-if="duplicateOutputLocals.has(row.localVar)">
              <template #trigger>
                <n-icon :component="AlertCircleIcon" color="#d03050" class="status-icon"/>
              </template>
              内部变量来源重复，这将导致映射冲突。
            </n-tooltip>
          </div>
          <n-icon :component="ArrowForwardIcon" class="arrow-icon" style="transform: rotate(0)"/>
          <div class="mapping-value">
            <n-dynamic-tags
                :value="row.globalVars"
                @update:value="(newValues: string[]) => handleUpdateOutputValues(row.localVar, newValues)"
            />
          </div>
          <n-button text type="error" @click="deleteOutputMappingRow(row.localVar)" class="delete-button">
            <template #icon> <n-icon :component="DeleteIcon"/> </template>
          </n-button>
        </div>
        <n-empty v-if="outputMappingRows.length === 0" description="暂无输出映射" class="empty-placeholder"/>
      </div>
    </n-card>
  </div>
</template>

<script lang="ts" setup>
import {computed, h} from 'vue';
import {NAlert, NAutoComplete, NButton, NCard, NDynamicTags, NEmpty, NIcon, NTag, NTooltip} from 'naive-ui';
import {AddIcon, AlertCircleIcon, ArrowForwardIcon, DeleteIcon} from '@/utils/icons';

// --- 类型定义 ---
interface InputMappingRow {
  globalVar: string;
  localVars: string[];
}
interface OutputMappingRow {
  localVar: string;
  globalVars: string[];
}

// --- 组件 Props 和 Emits ---
const props = defineProps<{
  inputMappings: Record<string, string[]>;
  outputMappings: Record<string, string[]>;
  requiredInputs: string[];
  producibleOutputs: string[];
  availableGlobalVars?: string[];
}>();

const emit = defineEmits(['update:inputMappings', 'update:outputMappings']);

// --- 计算属性 (ViewModel) ---
const inputMappingRows = computed<InputMappingRow[]>(() =>
    Object.entries(props.inputMappings).map(([globalVar, localVars]) => ({ globalVar, localVars }))
);
const outputMappingRows = computed<OutputMappingRow[]>(() =>
    Object.entries(props.outputMappings).map(([localVar, globalVars]) => ({ localVar, globalVars }))
);

// --- 计算属性 (用于UI和校验) ---
const availableGlobalVarsOptions = computed(() => props.availableGlobalVars?.map(v => ({label: v, value: v})) ?? []);
const requiredInputsOptions = computed(() => props.requiredInputs.map(v => ({label: v, value: v})));
const producibleOutputsOptions = computed(() => props.producibleOutputs.map(v => ({label: v, value: v})));

const mappedLocalInputs = computed(() => new Set(Object.values(props.inputMappings).flat()));
const missingInputs = computed(() => props.requiredInputs.filter(req => !mappedLocalInputs.value.has(req)));

const duplicateInputGlobals = computed(() => findDuplicates(Object.keys(props.inputMappings)));
const duplicateOutputLocals = computed(() => findDuplicates(Object.keys(props.outputMappings)));

function findDuplicates(arr: string[]): Set<string> {
  const seen = new Set<string>();
  const duplicates = new Set<string>();
  arr.forEach(item => {
    if (item.trim() === '') return;
    if (seen.has(item)) {
      duplicates.add(item);
    } else {
      seen.add(item);
    }
  });
  return duplicates;
}

// --- 事件处理器 (Input Mappings) ---
function addInputMappingRow() {
  const newMappings = { ...props.inputMappings, '': [] };
  emit('update:inputMappings', newMappings);
}

function deleteInputMappingRow(globalVar: string) {
  const newMappings = { ...props.inputMappings };
  delete newMappings[globalVar];
  emit('update:inputMappings', newMappings);
}

function handleUpdateInputKey(oldKey: string, newKey: string) {
  if (oldKey === newKey) return;
  const newMappings = { ...props.inputMappings };
  // 检查新键是否已存在
  if (newKey in newMappings) {
    // 可以选择合并或提示错误，这里我们先阻止覆盖
    console.warn(`映射键 "${newKey}" 已存在。`);
    return;
  }
  newMappings[newKey] = newMappings[oldKey];
  delete newMappings[oldKey];
  emit('update:inputMappings', newMappings);
}

function handleUpdateInputValues(globalVar: string, newValues: string[]) {
  const newMappings = { ...props.inputMappings, [globalVar]: newValues };
  emit('update:inputMappings', newMappings);
}

// --- 事件处理器 (Output Mappings) ---
function addOutputMappingRow() {
  const newMappings = { ...props.outputMappings, '': [] };
  emit('update:outputMappings', newMappings);
}

function deleteOutputMappingRow(localVar: string) {
  const newMappings = { ...props.outputMappings };
  delete newMappings[localVar];
  emit('update:outputMappings', newMappings);
}

function handleUpdateOutputKey(oldKey: string, newKey: string) {
  if (oldKey === newKey) return;
  const newMappings = { ...props.outputMappings };
  if (newKey in newMappings) {
    console.warn(`映射键 "${newKey}" 已存在。`);
    return;
  }
  newMappings[newKey] = newMappings[oldKey];
  delete newMappings[oldKey];
  emit('update:outputMappings', newMappings);
}

function handleUpdateOutputValues(localVar: string, newValues: string[]) {
  const newMappings = { ...props.outputMappings, [localVar]: newValues };
  emit('update:outputMappings', newMappings);
}
</script>

<style scoped>
.tuum-mappings-editor {
  background-color: #fcfcfd;
  padding: 12px;
  border-radius: 4px;
  border: 1px solid #f0f0f0;
}

.mappings-list {
  margin-top: 12px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.mapping-row {
  display: grid;
  grid-template-columns: 1fr auto 1fr auto;
  gap: 8px;
  align-items: center;
  padding: 8px;
  border: 1px solid #eef2f5;
  border-radius: 4px;
  background-color: #fff;
}

.mapping-key, .mapping-value {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-grow: 1;
}

.arrow-icon {
  font-size: 16px;
  color: #aaa;
  transform: rotate(180deg);
}

.status-icon {
  font-size: 16px;
  cursor: help;
}

.delete-button {
  margin-left: 8px;
}

.empty-placeholder {
  margin: 16px 0;
}
</style>