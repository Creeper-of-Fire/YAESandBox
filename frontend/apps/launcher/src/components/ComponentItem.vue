<script lang="ts" setup>
import {computed, ref} from 'vue';
import type {Component} from '../stores/updaterStore';
import {useUpdaterStore} from '../stores/updaterStore';
import BaseModal from "./BaseModal.vue";
import MarkdownRenderer from "./MarkdownRenderer.vue";
import {launcherName} from "../utils/constant.ts";

const props = defineProps<{
  component: Component
}>();
const store = useUpdaterStore();

const showNotesModal = ref(false);
const showDescriptionModal = ref(false);

// 计算属性：为 description 创建单行摘要
const descriptionSummary = computed(() =>
{
  const description = props.component.description;
  if (!description) return '';

  // 找到第一个换行符的位置
  const firstLineBreakIndex = description.indexOf('\n');

  // 如果没有换行符，整段都是第一行
  if (firstLineBreakIndex === -1)
  {
    return description;
  }

  // 提取第一行内容
  return description.substring(0, firstLineBreakIndex).trim();
});

const buttonText = computed(() =>
{
  switch (props.component.status)
  {
    case 'not_installed':
      return '安装';
    case 'update_available':
      return '更新';
    case 'uptodate':
      return '重新安装';
    case 'downloading':
      return '下载中...';
    case 'pending_install':
      return '等待安装';
    case 'installing':
      return '安装中...';
    case 'error':
      return '重试';
    default:
      return '操作';
  }
});

const isButtonDisabled = computed(() =>
{
  // 1. 如果正在安装（文件操作），则禁用所有按钮
  if (store.isInstalling)
  {
    return true;
  }
  // 2. 如果组件自身正在下载或等待安装，则禁用它自己的按钮
  if (['downloading', 'pending_install'].includes(props.component.status))
  {
    return true;
  }
  // 3. 否则，按钮可用
  return false;
});

function handleAction()
{
  if (isButtonDisabled.value)
    return;
  if (props.component.id === launcherName)
  {
    store.updateLauncher();
  }
  else
  {
    // 对于其他所有组件，点击总是触发下载流程
    store.downloadComponent(props.component.id);
  }
}
</script>

<template>
  <li class="component-list-item">
    <div class="info">
      <div class="info-header">
        <span class="name">{{ component.name }}</span>
      </div>

      <div class="version-info">
        <span v-if="component.localVersion">
          <span class="version-tag local-version">v{{ component.localVersion }}</span>
        </span>
        <span v-if="component.status === 'update_available'" class="arrow">→</span>
        <span v-if="component.status === 'update_available' || !component.localVersion">
          <span class="version-tag remote-version">v{{ component.remoteVersion }}</span>
        </span>
        <!-- "查看版本说明" 按钮 -->
        <button v-if="component.notes" class="details-button" @click="showNotesModal = true">
          {{ component.status === 'update_available' ? '更新说明' : '版本说明' }}
        </button>
      </div>

      <!-- description 摘要和详情按钮 -->
      <div v-if="component.description" class="description-summary">
        <!-- p 标签使用 CSS 进行单行截断 -->
        <p class="summary-text">{{ descriptionSummary }}</p>
        <!-- a 标签替代 button，并绑定点击事件 -->
        <a class="details-link" href="#" @click.prevent="showDescriptionModal = true">查看详情</a>
      </div>

      <div v-if="component.status === 'downloading'" class="progress-bar-container">
        <progress :value="component.progress.percentage" max="100"></progress>
        <span class="progress-text">{{ component.progress.text }}</span>
      </div>
      <div v-if="component.error" class="error-text">
        错误: {{ component.error }}
      </div>
    </div>
    <div class="status-action">
      <span :class="['status-tag', `status-${component.status}`]">{{ component.statusText }}</span>
      <button :disabled="isButtonDisabled" class="button-primary" @click="handleAction">
        {{ buttonText }}
      </button>
    </div>
  </li>

  <!-- Description 模态框 -->
  <BaseModal v-model="showDescriptionModal" :title="`${component.name} - 详情`">
    <MarkdownRenderer :markdown-content="component.description"/>
  </BaseModal>

  <!-- Notes 模态框 -->
  <BaseModal v-model="showNotesModal" :title="`${component.name} - 版本说明`">
    <MarkdownRenderer :markdown-content="component.notes"/>
  </BaseModal>
</template>

<style scoped>
/* --- 布局与基础样式 --- */
.component-list-item {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 0.75rem 0;
  border-bottom: 1px solid var(--border-color-soft);
}

.component-list-item:last-child {
  border-bottom: none;
}

.info {
  display: flex;
  flex-direction: column;
  flex-grow: 1;
  padding-right: 1rem;
  min-width: 0;
}

.name {
  font-weight: 600;
  font-size: 1rem;
  color: var(--text-color-accent);
}

/* --- 右侧状态和操作按钮 --- */
.status-action {
  display: flex;
  align-items: center;
  gap: 1rem;
  user-select: none;
  flex-shrink: 0;
  justify-content: flex-end;
}

.status-tag {
  font-size: 0.7em;
  padding: 0.2em 0.6em;
  border-radius: 12px;
  font-weight: bold;
  color: var(--text-color-inverted);
  width: 50px; /* 固定宽度以便对齐 */
  text-align: center;
  white-space: nowrap;
}

.status-uptodate {
  background-color: var(--color-success);
}

.status-update_available {
  background-color: var(--color-warning);
  color: var(--text-color-on-warning);
}

.status-not_installed {
  background-color: var(--color-secondary);
}

.status-downloading, .status-installing, .status-pending_install {
  background-color: var(--color-brand);
}

.status-error {
  background-color: var(--color-danger);
}

/* --- 描述信息 --- */
.info-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

/* --- Description 摘要 --- */
.description-summary {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.summary-text {
  margin: 0;
  font-size: 0.9rem;
  color: var(--text-color-muted);
  white-space: nowrap; /* 强制单行 */
  overflow: hidden;
  text-overflow: ellipsis; /* 超出部分显示省略号 */
}

/* --- 版本信息 --- */
.version-info {
  display: flex;
  align-items: center;
  gap: 0.15rem;
  font-size: 0.95rem; /* 增大字体 */
  color: var(--text-color-secondary);
}

.version-tag {
  font-weight: 600;
  padding: 0.1em 0.4em;
  border-radius: 4px;
  font-family: monospace;
}

.local-version {
  background-color: var(--bg-color-code);
  color: var(--text-color-muted);
  text-decoration: line-through;
}

.remote-version {
  background-color: var(--bg-color-success-soft);
  color: var(--color-success);
}

.arrow {
  font-weight: bold;
  font-size: 1.1em;
}

/* --- 详情按钮 --- */
.details-button {
  background: none;
  border: 1px solid var(--color-brand);
  color: var(--color-brand);
  padding: 0.2rem 0.6rem;
  font-size: 0.8rem;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
  flex-shrink: 0; /* 防止按钮被压缩 */
}

.details-button:hover {
  background-color: var(--bg-color-hover);
}

/* --- 超链接样式的 "详情" 按钮 ---  */
.details-link {
  color: var(--text-color-link);
  text-decoration: none;
  font-size: 0.85rem;
  cursor: pointer;
  border-bottom: 1px solid transparent;
  transition: all 0.2s;
  white-space: nowrap; /* 防止链接自身换行 */
  flex-shrink: 0; /* 防止被压缩 */
}

.details-link:hover {
  text-decoration: underline;
  color: var(--text-color-link-hover);
}

/* --- 模态框 (Modal) 样式 --- */
.modal-body {
  flex-grow: 1;
  overflow-y: auto;
  line-height: 1.6;
}

.progress-bar-container {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-top: 0.5rem;
}

progress {
  width: 150px;
}

.progress-text {
  font-size: 0.8em;
  color: var(--text-color-secondary);
  min-width: 120px;
}

.error-text {
  color: var(--color-danger);
  font-size: 0.85em;
  margin-top: 0.5rem;
}
</style>

