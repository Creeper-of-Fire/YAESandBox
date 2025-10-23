<!-- VarSpecPopover.vue (新基础组件) -->
<template>
  <n-popover :placement="placement" :style="{ maxWidth: '300px' }" trigger="hover">
    <!--
      #trigger 插槽的内容由父组件通过默认插槽提供。
      我们将父组件塞进来的所有东西，都放到 n-popover 的 trigger 位置。
    -->
    <template #trigger>
      <slot></slot>
    </template>

    <!--
      浮动卡片的内容是固定的，所以直接写在这里。
      这是所有组件共享的、不变的部分。
    -->
    <n-flex vertical>
      <n-text strong>变量名：{{ varName || '未命名' }}</n-text>
      <n-text>类型: {{ specDef.typeName }}</n-text>

      <!-- 只有当 isOptional 字段存在时，才渲染这一行 -->
      <n-text v-if="isOptional !== undefined">
        可选性:
        <n-tag :size="size" :type="isOptional ? 'warning' : 'success'">{{ isOptional ? '可选' : '必需' }}</n-tag>
      </n-text>

      <n-text depth="3">描述: {{ specDef.description || '无' }}</n-text>

      <!-- 结构详情区，仅当为复杂类型时显示 -->
      <template v-if="isComplexType">
        <n-divider style="margin-top: 8px; margin-bottom: 8px;">结构详情</n-divider>
        <n-tree
            :data="treeData"
            :render-label="renderLabel"
            :selectable="false"
            block-line
            default-expand-all
        />
      </template>
    </n-flex>
  </n-popover>
</template>

<script lang="tsx" setup>
import {NFlex, NPopover, NTag, NText, type TreeOption} from 'naive-ui';
import type {ListVarSpecDef, RecordVarSpecDef, VarSpecDef} from "#/types/generated/workflow-config-api-client";
import type {Placement} from "vueuc/lib/binder/src/interface";
import {computed} from "vue";

// 定义一个扩展 TreeOption 的接口，以便在渲染函数中获得更强的类型提示
interface VarSpecTreeOption extends TreeOption
{
  nodeName: string;
  nodeTypeName: string;
  children?: VarSpecTreeOption[];
}

const props = withDefaults(defineProps<{
  specDef: VarSpecDef;
  varName?: string | null;
  isOptional?: boolean;
  placement?: Placement;
  size?: 'small' | 'medium' | 'large';
}>(), {
  varName: null,
  isOptional: undefined,
  placement: 'right',
  size: 'small',
});

/**
 * 判断是否为复杂类型（Record 或 List）
 */
const isComplexType = computed(() => 'properties' in props.specDef || 'elementDef' in props.specDef);

/**
 * 递归函数：将 VarSpecDef 转换为 NTree 所需的数据结构
 * @param spec - 当前变量类型定义
 * @param name - 变量或属性的名称
 * @param keyPrefix - 用于生成唯一 key 的前缀
 */
function specToTreeOption(spec: VarSpecDef, name: string, keyPrefix: string): VarSpecTreeOption
{
  const option: VarSpecTreeOption = {
    key: keyPrefix,
    nodeName: name,
    nodeTypeName: spec.typeName,
  };

  // 如果是 Record 类型
  if ('properties' in spec)
  {
    const recordSpec = spec as RecordVarSpecDef;
    if (recordSpec.properties && Object.keys(recordSpec.properties).length > 0)
    {
      option.children = Object.entries(recordSpec.properties).map(([propName, propSpec]) =>
          specToTreeOption(propSpec, propName, `${keyPrefix}.${propName}`)
      );
    }
  }
  // 如果是 List 类型
  else if ('elementDef' in spec)
  {
    const listSpec = spec as ListVarSpecDef;
    option.children = [
      specToTreeOption(listSpec.elementDef, '[ ] 元素', `${keyPrefix}[]`)
    ];
  }

  return option;
}

/**
 * 计算属性，生成树状数据
 */
const treeData = computed<VarSpecTreeOption[]>(() =>
{
  if (!isComplexType.value)
  {
    return [];
  }
  // 创建一个顶层节点来表示整个结构
  return [specToTreeOption(props.specDef, props.varName || '根节点', 'root')];
});

/**
 * 自定义节点渲染函数
 */
const renderLabel = ({option}: { option: VarSpecTreeOption }) =>
{
  return (
      <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            width: '100%'
          }}
      >
        <span>{option.nodeName}</span>
        <NTag size="tiny" type="info" bordered={false}>
          {option.nodeTypeName}
        </NTag>
      </div>
  );
};
</script>