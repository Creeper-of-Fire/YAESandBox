<template>
  <n-card title="游戏日志" :bordered="false" content-style="padding: 12px;">
    <n-log :rows="10" log="[系统] 游戏已开始。以下为模拟日志。
[对话] 酒馆老板：今天想喝点什么？
[战斗] 你对史莱姆造成了 5 点伤害。"/>
  </n-card>
</template>

<script lang="ts" setup>
import { NCard, NLog } from 'naive-ui';
// 目前只是占位符
</script>