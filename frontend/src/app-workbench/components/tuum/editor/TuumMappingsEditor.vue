<!-- src/app-workbench/components/editor/TuumMappingsEditor.vue -->
<template>
  <div class="tuum-mappings-editor">
    <!-- 输入映射 -->
    <n-card :bordered="true" size="small" title="输入映射">
      <template #header-extra>
        <n-button dashed size="small" @click="addInputMappingRow">
          <template #icon>
            <n-icon :component="AddIcon"/>
          </template>
          手动添加映射
        </n-button>
      </template>

      <!-- 1. 缺少必要输入的警告 -->
      <n-alert v-if="missingRequiredInternalInputs.length > 0" :show-icon="true" title="缺少必要的内部输入" type="error">
        <p style="margin-top: 4px;">此枢机需要以下内部变量，但它们既未被内部生产，也未从外部映射：</p>
        <n-tag v-for="input in missingRequiredInternalInputs" :key="input" style="margin-right: 8px; margin-top: 4px;" type="error">
          {{ input }}
        </n-tag>
      </n-alert>

      <!-- 2. 输入映射列表 -->
      <div class="mappings-list">
        <div v-for="row in inputMappingRows" :key="row.globalVar" class="mapping-row">
          <div class="mapping-key">
            <VariableTag
                :available-options="consumedEndpointOptions"
                :name="row.globalVar"
                :spec="consumedEndpointMap.get(row.globalVar)"
                @update:name="newVal => handleUpdateInputKey(row.globalVar, newVal)"
            />
          </div>
          <n-icon :component="ArrowForwardIcon" class="arrow-icon" style="transform: rotate(0)"/>
          <div class="mapping-value">
            <VariableTagSelector
                v-model:model-value="row.localVars"
                :available-specs="analysisResult?.internalConsumedSpecs ?? []"
                :restrict-to-list="true"
                placeholder="+ 添加目标"
                @update:model-value="newValues => handleUpdateInputValues(row.globalVar, newValues)"
            />
          </div>
          <n-button class="delete-button" text type="error" @click="deleteInputMappingRow(row.globalVar)">
            <template #icon>
              <n-icon :component="DeleteIcon"/>
            </template>
          </n-button>
        </div>
        <n-empty v-if="inputMappingRows.length === 0" class="empty-placeholder" description="暂无输入映射"/>
      </div>
    </n-card>

    <!-- 输出映射 -->
    <n-card :bordered="true" size="small" style="margin-top: 16px;" title="输出映射">
      <template #header-extra>
        <n-button dashed size="small" @click="addOutputMappingRow">
          <template #icon>
            <n-icon :component="AddIcon"/>
          </template>
          添加输出映射
        </n-button>
      </template>

      <!-- 输出映射列表 -->
      <div class="mappings-list">
        <div v-for="row in outputMappingRows" :key="row.localVar" class="mapping-row">
          <div class="mapping-key">
            <VariableTag
                :available-options="internalProducedVarsOptions"
                :name="row.localVar"
                :spec="internalProducedSpecMap.get(row.localVar)"
                @update:name="newVal => handleUpdateOutputKey(row.localVar, newVal)"
            />
          </div>
          <n-icon :component="ArrowForwardIcon" class="arrow-icon"/>
          <div class="mapping-value">
            <VariableTagSelector
                v-model:model-value="row.globalVars"
                :available-specs="analysisResult?.producedEndpoints ?? []"
                :restrict-to-list="false"
                placeholder="+ 添加端点"
                @update:model-value="newValues => handleUpdateOutputValues(row.localVar, newValues)"
            />
          </div>
          <n-button class="delete-button" text type="error" @click="deleteOutputMappingRow(row.localVar)">
            <template #icon>
              <n-icon :component="DeleteIcon"/>
            </template>
          </n-button>
        </div>
        <n-empty v-if="outputMappingRows.length === 0" class="empty-placeholder" description="暂无输出映射"/>
      </div>
    </n-card>
  </div>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {NAlert, NButton, NCard, NEmpty, NIcon, NTag} from 'naive-ui';
import {AddIcon, ArrowForwardIcon, DeleteIcon} from '@/utils/icons';
import type {TuumAnalysisResult} from "@/app-workbench/types/generated/workflow-config-api-client";
import VariableTagSelector from "@/app-workbench/components/tuum/editor/VariableTagSelector.vue";
import VariableTag from "@/app-workbench/components/tuum/editor/VariableTag.vue";

// --- 类型定义 ---
interface InputMappingRow
{
  id: symbol;
  globalVar: string;
  localVars: string[];
}

interface OutputMappingRow
{
  id: symbol;
  localVar: string;
  globalVars: string[];
}

// --- 组件 Props 和 Emits ---
const props = defineProps<{
  inputMappings: Record<string, string[]>;
  outputMappings: Record<string, string[]>;
  availableGlobalVars?: string[];
  analysisResult: TuumAnalysisResult | null;
}>();

const emit = defineEmits(['update:inputMappings', 'update:outputMappings']);

// --- 计算属性 (ViewModel) ---
const inputMappingRows = computed<InputMappingRow[]>(() =>
    Object.entries(props.inputMappings).map(([globalVar, localVars]) => ({
      id: Symbol(),
      globalVar,
      localVars
    }))
);

// 为 outputMappingRows 也添加唯一的 id
const outputMappingRows = computed<OutputMappingRow[]>(() =>
    Object.entries(props.outputMappings).map(([localVar, globalVars]) => ({
      id: Symbol(),
      localVar,
      globalVars
    }))
);


// 为单选 VariableTag 提供补全列表的计算属性
const consumedEndpointOptions = computed(() => props.analysisResult?.consumedEndpoints.map(s => ({label: s.name, value: s.name})) ?? []);
const internalProducedVarsOptions = computed(() => props.analysisResult?.internalProducedSpecs.map(s => ({
  label: s.name,
  value: s.name
})) ?? []);

// 为 VariableTag 提供快速查找 Spec 的 Map
function createSpecMap<T extends { name: string }>(specs: T[] | undefined): Map<string, T> {
  const map = new Map<string, T>();
  specs?.forEach(spec => {
    map.set(spec.name, spec);
  });
  return map;
}

const consumedEndpointMap = computed(() => createSpecMap(props.analysisResult?.consumedEndpoints));
const internalProducedSpecMap = computed(() => createSpecMap(props.analysisResult?.internalProducedSpecs));

// 错误-缺失的必需内部输入: 找出那些必需但未被映射的内部变量
const missingRequiredInternalInputs = computed(() =>
{
  if (!props.analysisResult) return [];

  // 1. 找出所有分析出的必需内部变量 (net requirements)
  const requiredInternalVars = new Set(
      props.analysisResult.internalConsumedSpecs
          .filter(spec => !spec.isOptional) // isOptional=false 意味着是净需求
          .map(spec => spec.name)
  );

  // 2. 找出当前所有已被映射的内部变量
  const mappedInternalVars = new Set(Object.values(props.inputMappings).flat());

  // 3. 返回二者的差集
  return [...requiredInternalVars].filter(req => !mappedInternalVars.has(req));
});


const duplicateInputGlobals = computed(() => findDuplicates(Object.keys(props.inputMappings)));
const duplicateOutputLocals = computed(() => findDuplicates(Object.keys(props.outputMappings)));

function findDuplicates(arr: string[]): Set<string>
{
  const seen = new Set<string>();
  const duplicates = new Set<string>();
  arr.forEach(item =>
  {
    if (item.trim() === '') return;
    if (seen.has(item))
    {
      duplicates.add(item);
    }
    else
    {
      seen.add(item);
    }
  });
  return duplicates;
}

// --- 事件处理器 (Input Mappings) ---
function addInputMappingRow()
{
  const newMappings = {...props.inputMappings, '': []};
  emit('update:inputMappings', newMappings);
}

function deleteInputMappingRow(globalVar: string)
{
  const newMappings = {...props.inputMappings};
  delete newMappings[globalVar];
  emit('update:inputMappings', newMappings);
}

function handleUpdateInputKey(oldKey: string, newKey: string)
{
  if (oldKey === newKey || !newKey) return;
  const newMappings = {...props.inputMappings};
  // 检查新键是否已存在
  if (newKey in newMappings)
  {
    // 可以选择合并或提示错误，这里我们先阻止覆盖
    console.warn(`映射键 "${newKey}" 已存在。`);
    return;
  }
  newMappings[newKey] = newMappings[oldKey];
  delete newMappings[oldKey];
  emit('update:inputMappings', newMappings);
}

function handleUpdateInputValues(globalVar: string, newValues: string[])
{
  const newMappings = {...props.inputMappings, [globalVar]: newValues};
  emit('update:inputMappings', newMappings);
}

// --- 事件处理器 (Output Mappings) ---
function addOutputMappingRow()
{
  const newMappings = {...props.outputMappings, '': []};
  emit('update:outputMappings', newMappings);
}

function deleteOutputMappingRow(localVar: string)
{
  const newMappings = {...props.outputMappings};
  delete newMappings[localVar];
  emit('update:outputMappings', newMappings);
}

function handleUpdateOutputKey(oldKey: string, newKey: string)
{
  if (oldKey === newKey || !newKey) return;
  const newMappings = {...props.outputMappings};
  if (newKey in newMappings)
  {
    console.warn(`映射键 "${newKey}" 已存在。`);
    return;
  }
  newMappings[newKey] = newMappings[oldKey];
  delete newMappings[oldKey];
  emit('update:outputMappings', newMappings);
}

function handleUpdateOutputValues(localVar: string, newValues: string[])
{
  const newMappings = {...props.outputMappings, [localVar]: newValues};
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