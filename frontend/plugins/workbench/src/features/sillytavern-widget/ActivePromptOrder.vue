<!-- ActivePromptOrder.vue -->
<template>
  <div class="active-prompt-order-container">
    <VueDraggable
        v-if="orderWithDetails.length > 0"
        :animation="150"
        :group="draggableGroupConfig"
        :model-value="orderWithDetails"
        class="prompts-list"
        handle=".drag-handle"
        ghost-class="ghost-item"
        item-key="identifier"
        @update:model-value="handleDragUpdate"
    >
      <div v-for="(item, index) in orderWithDetails" :key="item.identifier" class="draggable-list-item">
        <DraggablePromptItem
            :enabled="item.orderInfo.enabled"
            :is-selected="false"
            :prompt-item="item.details"
            context="order"
            @edit="handleEdit"
            @unlink="handleUnlink(index)"
            @update:enabled="(newVal) => handleEnableToggle(index, newVal)"
        />
      </div>
    </VueDraggable>

    <div v-else class="empty-state">
      <n-empty description="当前顺序为空">
        <template #extra>
          <n-text depth="3">
            从左侧的“可用提示词”列表中拖拽项目到此处来添加。
          </n-text>
        </template>
      </n-empty>
    </div>
  </div>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {NEmpty, NText, useDialog, useThemeVars} from 'naive-ui';
import {VueDraggable} from 'vue-draggable-plus';
import DraggablePromptItem from './DraggablePromptItem.vue';
import type {OrderItem, PromptItem} from './sillyTavernPreset';

// 1. 定义 Props 和 Emits
const props = defineProps<{
  order: OrderItem[]; // 当前角色的顺序列表 (v-model)
  prompts: PromptItem[]; // 完整的提示词定义池
}>();

const emit = defineEmits<{
  (e: 'update:order', value: OrderItem[]): void;
  (e: 'edit', identifier: string): void;
}>();

// 2. 数据处理与转换
// 为了高效查找，将 prompts 数组转换为一个以 identifier 为键的 Map
const promptsMap = computed(() => new Map(props.prompts.map(p => [p.identifier, p])));

// 将 order 数组与 promptsMap 结合，生成一个包含完整信息的列表用于渲染
const orderWithDetails = computed(() =>
{
  return props.order
      .map(orderInfo =>
      {
        const details = promptsMap.value.get(orderInfo.identifier);
        // 如果在定义池中找不到对应的 prompt (数据不一致)，则过滤掉
        if (!details) return null;
        return {
          orderInfo, // { identifier, enabled }
          details,   // 完整的 PromptItem
          identifier: orderInfo.identifier, // for item-key
        };
      })
      .filter(item => item !== null);
});


// 3. 配置拖拽行为
const draggableGroupConfig = {
  name: 'silly-tavern-prompts', // 必须与 AvailablePrompts.vue 中的组名相同
  // pull 和 put 默认为 true，允许列表内排序和接收新项
};


// 4. 事件处理器

/**
 * 统一处理拖拽更新（包括排序和添加新项）
 * @param newItemsArray - vue-draggable-plus 传递的更新后的数组。
 *   这个数组的元素可能是我们自定义的 "orderWithDetails" 对象，
 *   也可能是刚从左侧拖入的原始 PromptItem 对象。
 */
const handleDragUpdate = (newItemsArray: (typeof orderWithDetails.value[0] | PromptItem)[]) => {
  const newOrder = newItemsArray.map(item => {
    // 检查这个 item 是否是我们已经处理过的、带有 'orderInfo' 的对象
    if ('orderInfo' in item && item.orderInfo) {
      // 如果是，说明它是一个已存在的项（可能只是位置变了），直接返回它的 OrderItem 部分
      return (item as typeof orderWithDetails.value[0]).orderInfo;
    }

    // 如果没有 'orderInfo'，说明这是一个刚从左侧拖入的、原始的 PromptItem 对象
    // 我们需要将它转换为 OrderItem 格式
    const newPromptItem = item as PromptItem;
    const newOrderItem: OrderItem = { // 也可以在这里显式声明
      identifier: newPromptItem.identifier,
      enabled: true, // 新添加的项默认启用
    };
    return newOrderItem;
  });

  emit('update:order', newOrder);
};


/**
 * 当一个新项从 AvailablePrompts 拖入时被调用
 * @param event - 包含新项索引和被拖拽的 DOM 元素的信息
 */
const handleDragAdd = (event: { newIndex: number }) =>
{
  // `vue-draggable-plus` 在 @add 事件发生时，已经修改了 v-model 绑定的数组。
  // 但它放入的是从左侧克隆过来的完整的 PromptItem 对象。
  // 我们需要找到这个新添加的对象，并将它转换为我们需要的 OrderItem 格式。

  // 我们从 props.order 获取已经更新（但格式不正确）的数组
  const currentOrder = [...props.order];
  const newItem = currentOrder[event.newIndex] as unknown as PromptItem;

  // 转换对象格式
  const newOrderItem: OrderItem = {
    identifier: newItem.identifier,
    enabled: true, // 新添加的项默认启用
  };

  // 替换掉数组中格式不正确的项
  currentOrder[event.newIndex] = newOrderItem;

  emit('update:order', currentOrder);
};


/**
 * 处理启用/禁用开关的切换
 */
const handleEnableToggle = (index: number, newValue: boolean) =>
{
  const newOrder = [...props.order];
  if (newOrder[index])
  {
    newOrder[index].enabled = newValue;
    emit('update:order', newOrder);
  }
};

const dialog = useDialog();
/**
 * 处理取消链接 (Unlink)，带有确认对话框
 */
const handleUnlink = (index: number) =>
{
  if (!props.order[index]) return;
  const itemToUnlinkIdentifier = props.order[index].identifier;
  const itemDetails = promptsMap.value.get(itemToUnlinkIdentifier);

  dialog.warning({
    title: '确认取消链接',
    content: `你确定要从当前激活顺序中移除 "${itemDetails?.name ?? '该项'}" 吗？它的定义仍然会保留在左侧的可用列表中。`,
    positiveText: '确定移除',
    negativeText: '取消',
    onPositiveClick: () =>
    {
      const newOrder = [...props.order];
      newOrder.splice(index, 1);
      emit('update:order', newOrder);
    },
  });
};

/**
 * 将编辑事件冒泡给父组件
 */
const handleEdit = (identifier: string) =>
{
  emit('edit', identifier);
};

const themeVars = useThemeVars();
</script>

<style scoped>
/* 样式与 AvailablePrompts.vue 保持一致，以实现视觉统一 */
.active-prompt-order-container {
  border: 1px solid v-bind('themeVars.borderColor');
  border-radius: v-bind('themeVars.borderRadius');
  background-color: v-bind('themeVars.baseColor');
  padding: 8px;
  height: 100%;
  min-height: 400px;
  overflow-y: auto;
}

.prompts-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.draggable-list-item {
  transition: all 0.2s ease-in-out;
  cursor: grab;
}

.draggable-list-item:active {
  cursor: grabbing;
}

.ghost-item {
  opacity: 0.5;
  /* 确保 ghost 元素有内容和高度 */
  content: '';
  min-height: 50px;
  background: v-bind('themeVars.actionColor');
  border: 1px dashed v-bind('themeVars.primaryColor');
  border-radius: 4px;
  box-sizing: border-box;
}

.empty-state {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 100%;
  min-height: 400px;
}
</style>