<template>
  <div style="height: 600px; border: 1px solid #ccc; overflow: hidden;">
    <n-virtual-list
        :items="mockBubbles"
        :item-size="100" 
    :item-resizable="true" 
    style="max-height: 100%;"
    item-class="bubble-item-wrapper" 
    >
    <template #default="{ item }">
      <!-- 模拟 BlockBubble 组件 -->
      <div class="mock-bubble">
        <p>ID: {{ item.id }}</p>
        <pre>{{ item.content }}</pre>
        <p style="font-size: 0.8em; color: gray;">(Height might vary)</p>
      </div>
    </template>
    </n-virtual-list>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { NVirtualList } from 'naive-ui'; // 导入组件

// 模拟 BlockBubble 数据
interface MockBubble {
  id: number;
  content: string;
}

const mockBubbles = ref<MockBubble[]>([]);

// 生成大量不同长度内容的数据
for (let i = 0; i < 1000; i++) {
  const contentLength = Math.floor(Math.random() * 300) + 50; // 随机内容长度 50-350
  let content = `模拟内容 ${i + 1}: `;
  content += '文本 '.repeat(contentLength);
  mockBubbles.value.push({ id: i, content });
}
</script>

<style scoped>
.mock-bubble {
  border: 1px solid lightblue;
  padding: 10px;
  margin-bottom: 5px; /* 外边距确保 bubble 之间有间隔 */
  background-color: #f0f8ff;
  box-sizing: border-box; /* 确保 padding 和 border 包含在高度内 */
}
.mock-bubble pre {
  white-space: pre-wrap;
  word-wrap: break-word;
  margin: 5px 0;
}
.bubble-item-wrapper {
  padding: 5px; /* 给包装元素加 padding 避免 margin collapse 问题 */
  box-sizing: border-box;
}
</style>