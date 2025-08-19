<!-- src/app-workbench/components/tuum/editor/MappingListEditor.vue -->
<template>
  <n-card :bordered="true" size="small">
    <template #header>
      <n-flex align="center" justify="space-between">
        <span style="font-weight: bold; font-size: 16px;">{{ title }}</span>
        <n-button secondary size="small" type="primary" @click="addMapping">
          <template #icon>
            <n-icon :component="AddIcon"/>
          </template>
          添加映射
        </n-button>
      </n-flex>
    </template>

    <n-text depth="3" style="font-size: 12px; display: block; margin-bottom: 16px;">
      {{ description }}
    </n-text>

    <n-space v-if="localItems.length > 0" vertical>
      <!-- 表头 -->
      <n-flex class="header-row" justify="space-between">
        <n-text class="col-key">{{ keyHeader }}</n-text>
        <div class="col-arrow"></div>
        <n-text class="col-value">{{ valueHeader }}</n-text>
        <div class="col-action"></div>
      </n-flex>

      <!-- 映射行 -->
      <n-flex
          v-for="(item, index) in localItems"
          :key="index"
          align="center"
          class="mapping-row"
          justify="space-between"
      >
        <!-- Key 输入 -->
        <n-auto-complete
            v-model:value="item.internalName"
            :get-show="() => true"
            :options="getFilteredAndSortedOptions(item.internalName, keyOptionsWithMeta)"
            :placeholder="keyHeader"
            :render-label="renderAutocompleteOption"
            blur-after-select
            class="col-key"
            clearable>
          <template #suffix>
            <VarSpecTag
                v-if="findSelectedOption(item.internalName, keyOptionsWithMeta)"
                :is-optional="getSpecTagProps(findSelectedOption(item.internalName, keyOptionsWithMeta)!.meta).isOptional"
                :size="'small'"
                :spec-def="findSelectedOption(item.internalName, keyOptionsWithMeta)!.meta.def"
                :tag-type="getSpecTagProps(findSelectedOption(item.internalName, keyOptionsWithMeta)!.meta).tagType"
                :var-name="findSelectedOption(item.internalName, keyOptionsWithMeta)!.meta.name"
            />
          </template>
        </n-auto-complete>

        <!-- 箭头 -->
        <n-icon :component="ArrowForwardIcon" :size="16" class="col-arrow"/>

        <!-- Value 输入 -->
        <n-auto-complete
            v-model:value="item.endpointName"
            :options="getFilteredAndSortedOptions(item.endpointName, valueOptionsWithMeta)"
            :placeholder="valueHeader"
            :render-label="renderAutocompleteOption"
            blur-after-select
            class="col-value"
            clearable>
          <template #suffix>
            <VarSpecTag
                v-if="findSelectedOption(item.endpointName, valueOptionsWithMeta)"
                :is-optional="getSpecTagProps(findSelectedOption(item.endpointName, valueOptionsWithMeta)!.meta).isOptional"
                :size="'small'"
                :spec-def="findSelectedOption(item.endpointName, valueOptionsWithMeta)!.meta.def"
                :tag-type="getSpecTagProps(findSelectedOption(item.endpointName, valueOptionsWithMeta)!.meta).tagType"
                :var-name="findSelectedOption(item.endpointName, valueOptionsWithMeta)!.meta.name"
            />
          </template>
        </n-auto-complete>

        <!-- 操作 -->
        <n-button class="col-action" text type="error" @click="removeMapping(index)">
          <template #icon>
            <n-icon :component="TrashIcon" :size="16"/>
          </template>
        </n-button>
      </n-flex>
    </n-space>

    <n-empty v-else description="暂无映射，请点击右上角添加"/>
  </n-card>
</template>

<script lang="ts" setup>
import {computed, h, ref, type VNodeChild, watch} from 'vue';
import {NAutoComplete, NButton, NCard, NEmpty, NFlex, NIcon, NSpace, NText} from 'naive-ui';
import type {
  ConsumedSpec,
  ProducedSpec,
  TuumInputMapping,
  TuumOutputMapping
} from "#/types/generated/workflow-config-api-client";
import {AddIcon, ArrowForwardIcon, TrashIcon} from '@yaesandbox-frontend/shared-ui/icons';
import VarSpecTag from "#/components/share/VarSpecTag.vue";

// --- 类型定义 ---
type MappingItem = TuumOutputMapping | TuumInputMapping;
type SpecOption = ConsumedSpec | ProducedSpec; // 你的变量定义类型

const props = defineProps<{
  title: string;
  description: string;
  items: MappingItem[];
  keyHeader: string;
  valueHeader: string;
  keyOptions: SpecOption[];
  valueOptions: SpecOption[];
}>();

const emit = defineEmits<{
  (e: 'update:items', value: MappingItem[]): void;
}>();

const localItems = ref<MappingItem[]>([]);

// --- 内部状态 ---
// const localItems = computed({
//       get()
//       {
//         return props.items;
//       },
//       set(value)
//       {
//         emit('update:items', value);
//       }
//     }
// );

// --- 方法 ---
const addMapping = () =>
{
  localItems.value.push({internalName: '', endpointName: ''});
};

const removeMapping = (index: number) =>
{
  localItems.value.splice(index, 1);
};

// // // --- 同步与发射 ---
// 监听 props.items 的变化来更新内部状态
watch(() => props.items, (newVal) =>
{
  // 简单比较，避免不必要的更新和光标跳动
  if (JSON.stringify(newVal) !== JSON.stringify(localItems.value))
  {
    localItems.value = JSON.parse(JSON.stringify(newVal));
  }
}, {immediate: true, deep: true});

// 监听内部状态的变化来通知父组件
watch(localItems, (newVal) =>
{
  // 简单比较，避免不必要的更新和光标跳动
  if (JSON.stringify(newVal) !== JSON.stringify(props.items))
  {
    emit('update:items', newVal);
  }
}, {deep: true});

// --- 自动补全选项渲染 ---
// 为选项附加元数据，以便渲染函数使用
type SpecOptionWithMeta = { label: string, value: string, meta: SpecOption };
const toComputedOptions: (options: SpecOption[]) => SpecOptionWithMeta[]
    = (options: SpecOption[]) =>
{
  return options.map(spec => ({
    label: spec.name,
    value: spec.name,
    meta: spec,
  }));
};

// --- ✨ 过滤与排序方法 ✨ ---
const getFilteredAndSortedOptions = (inputValue: string, sourceOptions: SpecOptionWithMeta[]): SpecOptionWithMeta[] =>
{
  const lowerCaseInput = inputValue?.toLowerCase() || '';

  function sortOptions(options: SpecOptionWithMeta[]): SpecOptionWithMeta[]
  {
    return options.sort((a, b) =>
    {
      const aMeta = a.meta as ConsumedSpec;
      const bMeta = b.meta as ConsumedSpec;

      // 优先级 1: 必需状态 (必需的在前)
      const aIsRequired = !aMeta.isOptional;
      const bIsRequired = !bMeta.isOptional;
      if (aIsRequired !== bIsRequired)
      {
        return aIsRequired ? -1 : 1;
      }

      // 优先级 2: 匹配位置 (以输入开头的在前)
      const aStartsWith = a.label.toLowerCase().startsWith(lowerCaseInput);
      const bStartsWith = b.label.toLowerCase().startsWith(lowerCaseInput);
      if (aStartsWith !== bStartsWith)
      {
        return aStartsWith ? -1 : 1;
      }

      // 优先级 3: 字母顺序
      return a.label.localeCompare(b.label);
    });
  }

  if (!lowerCaseInput || lowerCaseInput === "" || lowerCaseInput.length <= 1)
    return sortOptions(sourceOptions);

  const filtered = lowerCaseInput
      ? sourceOptions.filter(opt => opt.label.toLowerCase().includes(lowerCaseInput))
      : [...sourceOptions]; // 如果输入为空，则显示所有选项

  return sortOptions(filtered);
};

const keyOptionsWithMeta = computed(() => toComputedOptions(props.keyOptions));
const valueOptionsWithMeta = computed(() => toComputedOptions(props.valueOptions));

// 渲染函数，实现 "n-tag + n-popover" 的效果
const renderAutocompleteOption = (option: SpecOptionWithMeta): VNodeChild =>
{
  const spec = option.meta;
  if (!spec)
  {
    // 如果没有元数据（例如用户自由输入的值），只渲染文本
    return option.label as string;
  }

  return h(
      NFlex,
      {justify: 'space-between', align: 'center', style: {width: '100%'}},
      {
        default: () => [
          h('span', {class: 'option-label'}, option.label as string),
          // ✨ 将计算好的 type 作为 prop 传入
          h(VarSpecTag, getSpecTagProps(spec))
        ]
      }
  );
};

/**
 * 根据输入值从选项列表中查找完整的选项对象。
 * @param value 当前输入框的值
 * @param options 所有可用选项的列表
 * @returns 匹配的选项对象，或 undefined
 */
const findSelectedOption = (value: string, options: SpecOptionWithMeta[]): SpecOptionWithMeta | undefined =>
{
  if (!value) return undefined;
  return options.find(opt => opt.value === value);
};

/**
 * 根据变量规格决定其标签的属性。
 * @param spec 变量规格对象
 * @returns Naive UI 的标签类型
 */
const getSpecTagProps = (spec: ConsumedSpec | ProducedSpec) =>
{
  const isOptional: boolean | undefined = 'isOptional' in spec ? spec.isOptional : undefined;
  const type: 'default' | 'success' | 'info' = 'isOptional' in spec
      ? spec.isOptional ? 'default' : 'success'
      : 'info';

  return {specDef: spec.def, varName: spec.name, tagType: type, isOptional: isOptional};
};

</script>

<style scoped>
.header-row .n-text {
  font-weight: bold;
}

.mapping-row {
  padding: 4px 0;
}

.col-key, .col-value {
  flex: 1;
}

.col-arrow {
  width: 32px;
  text-align: center;
}

.col-action {
  width: 32px;
  display: flex;
  justify-content: center;
  align-items: center;
}
</style>