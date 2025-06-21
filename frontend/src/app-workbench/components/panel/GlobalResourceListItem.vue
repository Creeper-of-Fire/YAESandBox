<!-- src/app-workbench/components/GlobalResourceListItem.vue -->
<template>
  <div v-if="item.isSuccess" class="resource-item" @dblclick="$emit('start-editing', { type, id })">
    <span>{{ item.data.name }}</span>
    <n-button text @click="$emit('start-editing', { type, id })">编辑</n-button>
  </div>
  <div v-else class="resource-item-damaged">
    <n-icon :component="LinkOffIcon" color="#d03050"/>
    <n-tooltip trigger="hover">
      <template #trigger>
        <span class="damaged-text">{{ id }} (已损坏)</span>
      </template>
      {{ item.errorMessage }}
    </n-tooltip>
    <n-button text @click="$emit('show-error-detail', item.errorMessage, item.originJsonString)">
      详情
    </n-button>
  </div>
</template>

<script setup lang="ts">
import { NButton, NIcon, NTooltip } from 'naive-ui';
import { LinkOffOutlined as LinkOffIcon } from '@vicons/material'; // 导入断开链接图标
import type { ConfigType, ConfigObject } from '@/app-workbench/services/EditSession.ts';
import type { GlobalResourceItem } from '@/types/ui.ts'; // 导入全局资源项类型

// 定义组件的 props
defineProps<{
  id: string; // 原始 Record 的 key (资源的唯一ID)
  item: GlobalResourceItem<ConfigObject>; // 包含成功/失败状态和数据的资源项
  type: ConfigType; // 资源的类型 ('workflow', 'step', 'module')
}>();

// 定义组件触发的事件
defineEmits<{
  (e: 'start-editing', payload: { type: ConfigType; id: string }): void; // 双击或点击“编辑”时触发
  (e: 'show-error-detail', errorMessage: string, originJsonString: string | null | undefined): void; // 点击“详情”时触发
}>();
</script>

<style scoped>
/* 正常资源项的样式 */
.resource-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 4px 8px;
  border-radius: 4px;
  cursor: pointer; /* 可点击编辑 */
}

.resource-item:hover {
  background-color: #f0f2f5; /* 悬停背景色 */
}

/* 损坏资源项的样式 */
.resource-item-damaged {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 4px 8px;
  border-radius: 4px;
  cursor: default; /* 损坏项不可直接编辑，光标为默认 */
  color: #d03050; /* 错误红色 */
}

/* 损坏项名称旁边的文本样式 */
.damaged-text {
  margin-left: 8px;
  flex-grow: 1; /* 占据剩余空间 */
  overflow: hidden; /* 隐藏溢出内容 */
  text-overflow: ellipsis; /* 溢出时显示省略号 */
  white-space: nowrap; /* 不换行 */
}
</style>