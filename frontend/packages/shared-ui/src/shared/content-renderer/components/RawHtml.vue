<!-- RawHtml.vue -->
<template>
  <!--
    使用一个简单的 <span> 或 <div> 作为容器。
    v-html 指令会将其内容替换为 rawContent prop 中的原始 HTML。
    警告：只在受信任的内容上使用 v-html，以避免 XSS 攻击。
    在这个系统中，内容源是可控的，所以是安全的。
  -->
  <span v-html="rawContent"></span>
</template>

<script lang="ts" setup>
// 这个组件从 ContentRenderer 接收 rawContent prop。
defineProps<{
  rawContent: string;
}>();
</script>