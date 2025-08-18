<!-- ContentRenderer.vue -->
<template>
  <template v-for="(node, index) in nodesToRender" :key="index">
    <!-- 渲染纯文本节点 -->
    <template v-if="node.type === 'text'">{{ node.content }}</template>

    <!-- 渲染自定义标签节点 -->
    <template v-else-if="node.type === 'tag'">
      <component
          :is="resolveComponent(node.tagName)!"
          v-if="resolveComponent(node.tagName)"
          :raw-content="node.innerContent"
          v-bind="node.attributes"
      >
        <!--
          通过默认slot将原始innerContent传递下去
          简单组件可以直接使用 <slot/> 来渲染它
        -->
        <template #default>
          {{ node.innerContent }}
        </template>
      </component>
      <!-- Fallback: 组件未注册 -->
      <template v-else>
        {{ `<${node.tagName}>${node.innerContent}<` + `/` + node.tagName + `>` }}
      </template>
    </template>
  </template>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {parseContent} from './core/contentParser';
import {resolveComponent} from './core/componentRegistry';
import type {AstNode} from './types';

const props = defineProps({
  content: {type: String, default: ''}
});

const nodesToRender = computed<AstNode[]>(() => parseContent(props.content));
</script>

