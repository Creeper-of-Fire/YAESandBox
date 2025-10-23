<template>
  <div>
    <!-- 场景需求提示 -->
    <n-alert v-if="filter.expectedInputs?.length || filter.requiredTags?.length" style="margin-bottom: 1rem;" title="场景需求" type="info">
      <n-space vertical>
        <div v-if="filter.expectedInputs?.length">
          此任务期望生成器能处理以下输入：
          <n-space :style="{ marginTop: '8px' }">
            <n-tag v-for="input in filter.expectedInputs" :key="input" round type="success">{{ input }}</n-tag>
          </n-space>
        </div>
        <div v-if="filter.requiredTags?.length">
          并期望生成器包含以下标签：
          <n-space :style="{ marginTop: '8px' }">
            <n-tag v-for="tag in filter.requiredTags" :key="tag" type="info">{{ tag }}</n-tag>
          </n-space>
        </div>
      </n-space>
    </n-alert>

    <!-- 配置区域 -->
    <n-card size="small" style="margin-bottom: 1rem;">
      <n-flex justify="space-around">
        <!-- 输入匹配模式配置 -->
        <n-flex :size="8" vertical>
          <n-text strong>输入参数匹配</n-text>
          <n-radio-group v-model:value="inputMode" name="input-mode">
            <n-popover trigger="hover">
              <template #trigger>
                <n-radio value="strict">严格</n-radio>
              </template>
              必须完美匹配所有输入，不多不少。
            </n-popover>
            <n-popover trigger="hover">
              <template #trigger>
                <n-radio value="normal">普通</n-radio>
              </template>
              可以接受场景提供的额外输入（被忽略）。
            </n-popover>
            <n-popover trigger="hover">
              <template #trigger>
                <n-radio value="relaxed">宽松</n-radio>
              </template>
              允许工作流缺少部分输入。
            </n-popover>
          </n-radio-group>
        </n-flex>

        <n-divider vertical/>

        <!-- 标签匹配模式配置 -->
        <n-flex :size="8" vertical>
          <n-text strong>标签匹配</n-text>
          <n-radio-group v-model:value="tagMode" name="tag-mode">
            <n-popover trigger="hover">
              <template #trigger>
                <n-radio value="need">必须</n-radio>
              </template>
              工作流必须包含所有场景需求的标签。
            </n-popover>
            <n-popover trigger="hover">
              <template #trigger>
                <n-radio value="prefer">偏好</n-radio>
              </template>
              仅将标签作为排序依据，不强制要求。
            </n-popover>
          </n-radio-group>
        </n-flex>
      </n-flex>
    </n-card>

    <n-list bordered clickable hoverable>
      <n-list-item v-for="workflow in workflows" :key="workflow.id"
                   @click="handleSelect(workflow.id)">
        <n-thing :title="workflow.resource.name">
          <template #description>
            <n-space align="center" size="small" wrap>
              <!-- 渲染输入参数 -->
              <n-tag v-for="(status, name) in workflow.matchDetails.inputs" :key="name" :type="getInputTagType(status)" round
                     size="small">
                {{ name }}
              </n-tag>
              <!-- 渲染标签 -->
              <n-tag v-for="(status, name) in workflow.matchDetails.tags" :key="name" :type="getTagTagType(status)"
                     size="small">
                {{ name }}
              </n-tag>
            </n-space>
          </template>
        </n-thing>
      </n-list-item>
      <n-empty v-if="!workflows.length" description="在当前模式下没有找到匹配的工作流" style="padding: 2rem;"/>
    </n-list>
  </div>
</template>

<script lang="ts" setup>
import {NList, NListItem, NPopover, NSpace, NTag, NThing} from 'naive-ui';
import {type EnhancedWorkflow, type InputMatchingMode, type TagMatchingMode, type WorkflowFilter} from '../../composables.ts';

const inputMode = defineModel<InputMatchingMode>('inputMode', { required: true });
const tagMode = defineModel<TagMatchingMode>('tagMode', { required: true });

// 定义 Props & Emits
const props = defineProps<{
  workflows: EnhancedWorkflow[];
  filter: WorkflowFilter;
}>();

const emit = defineEmits<{
  (e: 'select', id: string): void;
}>();

const handleSelect = (id: string) => emit('select', id);

function getInputTagType(status: 'matched' | 'unprovided' | 'unconsumed'): 'success' | 'error' | 'default'
{
  switch (status)
  {
    case 'matched':
      return 'success';
    case 'unprovided':
      return 'error';
    case 'unconsumed':
      return 'default';
  }
}

function getTagTagType(status: 'matched' | 'missing' | 'extra'): 'info' | 'warning' | 'default'
{
  switch (status)
  {
    case 'matched':
      return 'info';
    case 'missing':
      return 'warning';
    case 'extra':
      return 'default';
  }
}
</script>