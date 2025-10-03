<script lang="ts" setup>
import {computed} from 'vue';
import {marked} from 'marked';
import DOMPurify from 'dompurify';

const props = defineProps<{
  markdownContent: string | undefined | null;
}>();

const renderedHtml = computed(() =>
{
  if (!props.markdownContent) return '';

  // 配置 marked
  const rawHtml = marked.parse(props.markdownContent, {
    gfm: true,       // 启用 GitHub Flavored Markdown
    breaks: true,    // 将换行符渲染为 <br>
    async: false,    // 使用同步解析，对于小型渲染更简单
  });

  // 使用 DOMPurify 清理，防止 XSS
  return DOMPurify.sanitize(String(rawHtml));
});
</script>

<template>
  <!--
    这个 div 是渲染内容的根元素，
    所有的样式都将作用于这个元素内部的标签。
  -->
  <div class="markdown-body" v-html="renderedHtml"></div>
</template>
<!-- 这个 style 块不是 scoped 的，这样才能正确应用到 v-html 渲染出的内容上 -->
<style>
.markdown-body {
  line-height: 1.6;
  font-size: 1rem; /* 根据你的应用调整基础字号 */
  color: var(--text-color-primary);
}
.markdown-body h1,
.markdown-body h2,
.markdown-body h3 {
  margin-top: 1.5em;
  margin-bottom: 0.75em;
  font-weight: 600;
  border-bottom: 1px solid var(--border-color-divider);
  padding-bottom: 0.3em;
}

.markdown-body p {
  margin-bottom: 1em;
}

.markdown-body ul, .markdown-body ol {
  padding-left: 1.5em;
  margin-bottom: 1em;
}

.markdown-body li > p {
  /* 修复列表项内段落的多余间距 */
  margin-bottom: 0.25em;
}

.markdown-body a {
  color: var(--text-color-link);
  text-decoration: none;
}

.markdown-body a:hover {
  text-decoration: underline;
}

.markdown-body code {
  background-color: var(--bg-color-code);
  padding: 0.2em 0.4em;
  border-radius: 3px;
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, Courier, monospace;
  font-size: 0.9em;
}

/* 针对 `<code>` 标签的通用样式 */
.markdown-body code {
  background-color: var(--bg-color-code);
  padding: 0.2em 0.4em;
  border-radius: 3px;
  font-family: monospace;
}

/* 针对 `<pre>` 块中的 `<code>` 标签 (即代码块) */
.markdown-body pre {
  background-color: var(--bg-color-code-block); /* 给代码块一个不同的背景色 */
  padding: 1em;
  border-radius: 6px;
  /* 核心：让 pre 元素自己处理溢出 */
  overflow-x: auto;
  /* 重置 pre 默认的 margin */
  margin-top: 0;
  margin-bottom: 1em;
}

.markdown-body pre code {
  /* 重置 pre > code 的内联样式，因为父元素 pre 已经有了 padding */
  padding: 0;
  background-color: transparent;
}

.markdown-body blockquote {
  border-left: 0.25em solid var(--border-color-medium);
  padding: 0 1em;
  color: var(--text-color-muted);
  margin-left: 0;
  margin-right: 0;
}
</style>