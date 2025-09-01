<!-- ContentRenderer.vue -->
<template>
  <template v-for="(node, index) in nodesToRender" :key="index">
    <!-- 1. 渲染纯文本节点 -->
    <template v-if="node.type === 'text'">{{ node.content }}</template>

    <!-- 2. 渲染已注册的自定义标签节点 -->
    <template v-else-if="node.type === 'tag'">
      <component
          :is="resolveComponent(node.tagName)!"
          v-if="resolveComponent(node.tagName)"
          :raw-content="node.innerContent"
          v-bind="node.attributes"
      >
        <!--
          通过默认 slot 将原始 innerContent 传递下去。
          这样设计最为灵活：
          - 简单的组件 (如 InfoPopup) 可以直接用 <slot /> 来渲染它。
          - 复杂的组件 (如 Collapse) 可以选择忽略 slot，使用 raw-content prop 并在内部递归渲染。
        -->
        <template #default>
          {{ node.innerContent }}
        </template>
      </component>
      <!-- Fallback: 如果组件未在注册表中找到，则将其作为纯文本渲染 -->
      <template v-else>
        {{ `<${node.tagName}>${node.innerContent}<` + `/` + `${node.tagName}>` }}
      </template>
    </template>

    <!-- 3. 渲染解析错误节点 -->
    <template v-else-if="node.type === 'error'">
      <n-alert :bordered="true" style="margin: 8px 0;" title="内容解析错误" type="error">
        <p style="font-family: monospace; white-space: pre-wrap; word-break: break-all;">
          {{ node.message }}
        </p>
        <template #header>
          <strong>内容解析错误</strong>
        </template>
        <p>以下内容块无法被正确解析：</p>
        <pre
            style="background-color: #f8f8f8; padding: 8px; border-radius: 4px; color: #d93026; white-space: pre-wrap; word-break: break-all;"><code>{{
            node.rawContent
          }}</code></pre>
      </n-alert>
    </template>
  </template>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {parseContent} from './core/contentParser';
import {contractsMap, resolveComponent} from './core/componentRegistry';
import type {AstNode} from './types';

const props = defineProps({
  content: {type: String, default: ''}
});

const nodesToRender = computed<AstNode[]>(() => parseContent(props.content, contractsMap.value));
</script>

