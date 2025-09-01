<!-- ContentRenderer.vue -->
<template>
  <div class="content-renderer-wrapper">
    <template v-for="(node, index) in filteredNodes" :key="index">
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
        <n-alert :bordered="false" class="error-alert" type="error">
          <template #header>
            <strong>内容解析错误</strong>
          </template>
          <p>以下内容块无法被正确解析：</p>
          <pre class="error-code-block"><code>{{ node.rawContent }}</code></pre>
          <details class="error-details">
            <summary>查看技术细节</summary>
            <p>{{ node.message }}</p>
          </details>
        </n-alert>
      </template>
    </template>
  </div>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {parseContent} from './core/contentParser';
import {contractsMap, resolveComponent} from './core/componentRegistry';
import type {AstNode} from './types';
import {NAlert, useThemeVars} from 'naive-ui';

const props = defineProps({
  content: {type: String, default: ''}
});
const themeVars = useThemeVars();
const nodesToRender = computed<AstNode[]>(() => parseContent(props.content, contractsMap.value));

const filteredNodes = computed(() => {
  return nodesToRender.value.filter(node => {
    // 如果节点不是文本节点，则保留
    if (node.type !== 'text') {
      return true;
    }
    // 如果是文本节点，检查其内容修剪后是否为空
    // 如果修剪后为空（即只包含空白字符），则过滤掉该节点
    return node.content.trim() !== '';
  });
});
</script>

<style scoped>
.content-renderer-wrapper {
  /*
    pre-wrap:
    - 保留空白符序列。
    - 文本行会正常换行。
    - 保留换行符。
    这使得文本节点内部有意义的换行得以保留。
  */
  white-space: pre-wrap;
  word-break: break-word; /* 确保长单词也能正常换行 */
}

.error-alert {
  margin: 8px 0;
  border-radius: v-bind('themeVars.borderRadius');
  background-color: v-bind('themeVars.errorColorSuppl'); /* 使用柔和的错误背景色 */
}

.error-code-block {
  background-color: v-bind('themeVars.codeColor');
  padding: 8px 12px;
  border-radius: 4px;
  color: v-bind('themeVars.errorColor'); /* 错误文本颜色 */
  white-space: pre-wrap;
  word-break: break-all;
  border: 1px solid v-bind('themeVars.borderColor');
}

.error-details {
  margin-top: 8px;
  font-size: 12px;
  color: v-bind('themeVars.textColor3');
}

.error-details summary {
  cursor: pointer;
  font-weight: bold;
}
</style>

