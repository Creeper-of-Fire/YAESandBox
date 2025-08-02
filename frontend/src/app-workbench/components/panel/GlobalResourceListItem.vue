<!-- src/app-workbench/components/.../GlobalResourceListItem.vue -->
<template>
  <div v-if="item.isSuccess"
       ref="listItemRef"
       class="resource-item"
       @dblclick="$emit('start-editing', { type, id })"
       @contextmenu.prevent="$emit('contextmenu', { type, id, name: item.data.name, event: $event })"
  >

    <span class="item-name">
        {{ item.data.name }}
      <!-- 如果变脏，显示一个小星号 -->
        <span v-if="isDirty" style="color: #f0a020;">*</span>
    </span>

    <!-- 将 text 按钮替换为更显眼的 secondary 图标按钮 -->
    <n-popover trigger="hover">
      <template #trigger>
        <n-button
            circle
            secondary
            size="small"
            strong
            type="primary"
            @click.stop="$emit('start-editing', { type, id })"
        >
          <template #icon>
            <n-icon :component="EditIcon"/>
          </template>
        </n-button>
      </template>
      编辑 “{{ item.data.name }}”
    </n-popover>
  </div>
  <div v-else class="resource-item-damaged"
       ref="listItemRef"
       @contextmenu.prevent="$emit('contextmenu', { type, id, name: id, isDamaged: true, event: $event })"
  >
    <n-icon :component="LinkOffIcon" color="#d03050"/>
    <n-popover trigger="hover">
      <template #trigger>
        <span class="damaged-text">{{ id }} (已损坏)</span>
      </template>
      {{ item.errorMessage }}
    </n-popover>
    <!-- 同样美化损坏项的“详情”按钮 -->
    <n-popover trigger="hover">
      <template #trigger>
        <n-button
            circle
            secondary
            size="small"
            strong
            type="error"
            @click.stop="$emit('show-error-detail', item.errorMessage, item.originJsonString)"
        >
          <template #icon>
            <n-icon :component="FindInPageIcon"/>
          </template>
        </n-button>
      </template>
      查看错误详情
    </n-popover>
  </div>
</template>

<script lang="ts" setup>
import {NButton, NIcon} from 'naive-ui';
import { onLongPress } from '@vueuse/core'
import {EditIcon, FindInPageIcon, LinkOffIcon} from '@/utils/icons';
import type {ConfigObject, ConfigType} from '@/app-workbench/services/EditSession';
import type {GlobalResourceItem} from '@/types/ui';
import {computed, ref} from "vue";
import {useWorkbenchStore} from "@/app-workbench/stores/workbenchStore.ts";

const props = defineProps<{
  id: string; // 原始 Record 的 key (资源的唯一ID)
  item: GlobalResourceItem<ConfigObject>; // 包含成功/失败状态和数据的资源项
  type: ConfigType; // 资源的类型 ('workflow', 'step', 'rune')
}>();

const emit = defineEmits<{
  (e: 'start-editing', payload: { type: ConfigType; id: string }): void; // 双击或点击“编辑”时触发
  (e: 'show-error-detail', errorMessage: string, originJsonString: string | null | undefined): void; // 点击“详情”时触发
  (e: 'contextmenu', payload: { type: ConfigType; id: string; name: string; isDamaged?: boolean; event: MouseEvent | PointerEvent }): void;
}>();

// *** 检查当前项是否变脏 ***
const workbenchStore = useWorkbenchStore();
const isDirty = computed(() => workbenchStore.isDirty(props.id));

// 实现长按逻辑
const listItemRef = ref<HTMLElement | null>(null);

onLongPress( // <-- 2. 修正钩子名称
    listItemRef,
    (event: PointerEvent) => {
      // 长按触发时，总是发出 contextmenu 事件
      // 我们在事件的 payload 中携带了所有必要的信息
      emit('contextmenu', {
        type: props.type,
        id: props.id,
        name: props.item.isSuccess ? props.item.data.name : props.id, // 根据成功/失败状态决定名称
        isDamaged: !props.item.isSuccess,
        event: event,
      });
    },
    { delay: 500 }
);

</script>

<style scoped>
/* 正常资源项的样式 */
.resource-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 6px 8px; /* 增加一点垂直内边距以适应更大的按钮 */
  border-radius: 4px;
  cursor: pointer; /* 可点击编辑 */
}

.resource-item.is-dirty {
  background-color: #f0a0201a; /* 浅橙黄色背景 */
}

.resource-item.is-dirty .item-name {
  font-weight: 500; /* 加粗一点点 */
}

.resource-item:hover {
  background-color: #f0f2f5; /* 悬停背景色 */
}

/* 让名称和按钮之间的空间更大，避免误触 */
.item-name {
  flex-grow: 1;
  margin-right: 8px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.resource-item-damaged {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 6px 8px; /* 同样增加垂直内边距 */
  border-radius: 4px;
  cursor: default; /* 损坏项不可直接编辑，光标为默认 */
  color: #d03050; /* 错误红色 */
}

.damaged-text {
  margin-left: 8px;
  margin-right: 8px;
  flex-grow: 1; /* 占据剩余空间 */
  overflow: hidden; /* 隐藏溢出内容 */
  text-overflow: ellipsis; /* 溢出时显示省略号 */
  white-space: nowrap; /* 不换行 */
}

</style>
<!-- END OF MODIFIED FILE -->