<template>
  <div
      :class="{ 'dark-mode-active': modelValue }"
      :style="{ fontSize: (size / 3).toFixed(2) + 'px' }"
      class="toggle-container"
      title="切换主题"
  >
    <div class="components">
      <div class="main-button">
        <div class="moon"></div>
        <div class="moon"></div>
        <div class="moon"></div>
      </div>
      <div class="daytime-backgrond"></div>
      <div class="daytime-backgrond"></div>
      <div class="daytime-backgrond"></div>
      <div class="cloud">
        <div :ref="setCloudPartRef" class="cloud-son"></div>
        <div :ref="setCloudPartRef" class="cloud-son"></div>
        <div :ref="setCloudPartRef" class="cloud-son"></div>
        <div :ref="setCloudPartRef" class="cloud-son"></div>
        <div :ref="setCloudPartRef" class="cloud-son"></div>
        <div :ref="setCloudPartRef" class="cloud-son"></div>
      </div>
      <div class="cloud-light">
        <div :ref="setCloudPartLightRef" class="cloud-son"></div>
        <div :ref="setCloudPartLightRef" class="cloud-son"></div>
        <div :ref="setCloudPartLightRef" class="cloud-son"></div>
        <div :ref="setCloudPartLightRef" class="cloud-son"></div>
        <div :ref="setCloudPartLightRef" class="cloud-son"></div>
        <div :ref="setCloudPartLightRef" class="cloud-son"></div>
      </div>
      <div class="stars">
        <div class="star big">
          <div class="star-son"></div>
          <div class="star-son"></div>
          <div class="star-son"></div>
          <div class="star-son"></div>
        </div>
        <div class="star big">
          <div class="star-son"></div>
          <div class="star-son"></div>
          <div class="star-son"></div>
          <div class="star-son"></div>
        </div>
        <div class="star medium">
          <div class="star-son"></div>
          <div class="star-son"></div>
          <div class="star-son"></div>
          <div class="star-son"></div>
        </div>
        <div class="star medium">
          <div class="star-son"></div>
          <div class="star-son"></div>
          <div class="star-son"></div>
          <div class="star-son"></div>
        </div>
        <div class="star small">
          <div class="star-son"></div>
          <div class="star-son"></div>
          <div class="star-son"></div>
          <div class="star-son"></div>
        </div>
        <div class="star small">
          <div class="star-son"></div>
          <div class="star-son"></div>
          <div class="star-son"></div>
          <div class="star-son"></div>
        </div>
      </div>
    </div>
  </div>
</template>

<script lang="ts" setup>
import {defineModel, nextTick, onBeforeUpdate, onMounted, onUnmounted, ref} from 'vue';

/**
 * 原始项目信息与许可证
 * This component is a Vue 3 translation of the original vanilla JS Web Component.
 *
 * Original Author: Xiumuzaidiao
 * Original Repository: https://github.com/Xiumuzaidiao/Day-night-toggle-button
 *
 * --- ISC License ---
 * Copyright(c)2024,Xiumuzaidiao
 *
 * Permission to use, copy, modify, and/or distribute this software for any
 * purpose with or without fee is hereby granted, provided that the above
 * copyright notice and this permission notice appear in all copies.
 *
 * THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
 * WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
 * MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
 * ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
 * WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
 * ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
 * OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
 */

defineProps({
  size: {
    type: Number,
    default: 3,
  },
  modelValue: {
    type: Boolean,
    default: false,
  },
});

// --- 云朵随机飘动效果的实现 ---
// 1. 创建普通的空数组来存储 DOM 元素引用
const cloudParts = ref<HTMLDivElement[]>([]);
const cloudPartsLight = ref<HTMLDivElement[]>([]);

// 2. 在每次组件更新前清空数组，防止重复添加
onBeforeUpdate(() => {
  cloudParts.value = [];
  cloudPartsLight.value = [];
});

// 3. 创建函数，Vue 会在渲染时调用这些函数并传入元素实例
const setCloudPartRef = (el: any) => {
  if (el) {
    cloudParts.value.push(el);
  }
};

const setCloudPartLightRef = (el: any) => {
  if (el) {
    cloudPartsLight.value.push(el);
  }
};

let cloudInterval: number | null = null;

onMounted(async () =>
{
  // 等待 DOM 渲染完成
  await nextTick();

  const allCloudParts = [...cloudParts.value, ...cloudPartsLight.value];

  const getRandomDirection = () =>
  {
    const directions = ["2em", "-2em"];
    return directions[Math.floor(Math.random() * directions.length)];
  };

  const moveElementRandomly = (element: HTMLElement) =>
  {
    const randomDirectionX = getRandomDirection();
    const randomDirectionY = getRandomDirection();
    // 使用 transform 来移动，性能更好
    element.style.transform = `translate(${randomDirectionX}, ${randomDirectionY})`;
  };

  // 启动定时器
  cloudInterval = window.setInterval(() =>
  {
    allCloudParts.forEach(moveElementRandomly);
  }, 1000);
});

// 组件卸载时清除定时器，防止内存泄漏
onUnmounted(() =>
{
  if (cloudInterval)
  {
    clearInterval(cloudInterval);
  }
});
</script>

<style scoped>
/* 基本样式和变量，直接从原始 CSS 翻译 */
* {
  margin: 0;
  padding: 0;
  transition: 0.7s;
}

.toggle-container {
  /* `em` 单位会根据这里的 font-size 进行缩放 */
  width: 180em;
  height: 70em;
  display: inline-block;
  vertical-align: middle; /* 方便在 n-space 中对齐 */
  transform: translate3d(0, 0, 0);
  cursor: pointer;
}

.components {
  position: relative; /* 改为 relative，因为 container 已经是 block */
  width: 100%;
  height: 100%;
  background-color: rgba(70, 133, 192, 1);
  border-radius: 100em;
  box-shadow: inset 0 0 5em 3em rgba(0, 0, 0, 0.5);
  overflow: hidden;
  transition: 0.7s cubic-bezier(0, 0.5, 1, 1);
}

.main-button {
  margin: 7.5em 0 0 7.5em;
  width: 55em;
  height: 55em;
  background-color: rgba(255, 195, 35, 1);
  border-radius: 50%;
  box-shadow: 3em 3em 5em rgba(0, 0, 0, 0.5),
  inset -3em -5em 3em -3em rgba(0, 0, 0, 0.5),
  inset 4em 5em 2em -2em rgba(255, 230, 80, 1);
  transition: 1s cubic-bezier(0.56, 1.35, 0.52, 1);
}

.moon {
  position: absolute;
  background-color: rgba(150, 160, 180, 1);
  box-shadow: inset 0em 0em 1em 1em rgba(0, 0, 0, 0.3);
  border-radius: 50%;
  transition: 0.5s;
  opacity: 0;
}

.moon:nth-child(1) {
  top: 7.5em;
  left: 25em;
  width: 12.5em;
  height: 12.5em;
}

.moon:nth-child(2) {
  top: 20em;
  left: 7.5em;
  width: 20em;
  height: 20em;
}

.moon:nth-child(3) {
  top: 32.5em;
  left: 32.5em;
  width: 12.5em;
  height: 12.5em;
}

.daytime-backgrond {
  position: absolute;
  border-radius: 50%;
  transition: 1s cubic-bezier(0.56, 1.35, 0.52, 1);
}

.daytime-backgrond:nth-of-type(1) {
  top: -20em;
  left: -20em;
  width: 110em;
  height: 110em;
  background-color: rgba(255, 255, 255, 0.2);
  z-index: 0;
}

.daytime-backgrond:nth-of-type(2) {
  top: -32.5em;
  left: -17.5em;
  width: 135em;
  height: 135em;
  background-color: rgba(255, 255, 255, 0.1);
  z-index: -1;
}

.daytime-backgrond:nth-of-type(3) {
  top: -45em;
  left: -15em;
  width: 160em;
  height: 160em;
  background-color: rgba(255, 255, 255, 0.05);
  z-index: -2;
}

.cloud, .cloud-light {
  transform: translateY(10em);
  transition: 1s cubic-bezier(0.56, 1.35, 0.52, 1);
}

.cloud-son {
  position: absolute;
  background-color: #fff;
  border-radius: 50%;
  z-index: 1; /* 比背景高一层 */
  transition: transform 6s, right 1s, bottom 1s; /* 保留原始动画 */
}

.cloud-son:nth-child(6n + 1) {
  right: -20em;
  bottom: 10em;
  width: 50em;
  height: 50em;
}

.cloud-son:nth-child(6n + 2) {
  right: -10em;
  bottom: -25em;
  width: 60em;
  height: 60em;
}

.cloud-son:nth-child(6n + 3) {
  right: 20em;
  bottom: -40em;
  width: 60em;
  height: 60em;
}

.cloud-son:nth-child(6n + 4) {
  right: 50em;
  bottom: -35em;
  width: 60em;
  height: 60em;
}

.cloud-son:nth-child(6n + 5) {
  right: 75em;
  bottom: -60em;
  width: 75em;
  height: 75em;
}

.cloud-son:nth-child(6n + 6) {
  right: 110em;
  bottom: -50em;
  width: 60em;
  height: 60em;
}

.cloud {
  z-index: 1;
}

.cloud-light {
  position: absolute;
  right: 0;
  bottom: 25em;
  opacity: 0.5;
  z-index: 0;
}

.stars {
  transform: translateY(-125em);
  z-index: 1;
  opacity: 0;
  transition: 1s cubic-bezier(0.56, 1.35, 0.52, 1);
}

.star {
  position: absolute;
  --size: 3em; /* default to small */
  width: calc(2 * var(--size));
  height: calc(2 * var(--size));
  transform: scale(1);
  transition: 1s cubic-bezier(0.56, 1.35, 0.52, 1);
  animation-iteration-count: infinite;
  animation-direction: alternate;
  animation-timing-function: linear;
}

.big {
  --size: 7.5em;
}

.medium {
  --size: 5em;
}

.small {
  --size: 3em;
}

.star:nth-child(1) {
  top: 11em;
  left: 39em;
  animation-name: star-twinkle;
  animation-duration: 3.5s;
}

.star:nth-child(2) {
  top: 39em;
  left: 91em;
  animation-name: star-twinkle;
  animation-duration: 4.1s;
}

.star:nth-child(3) {
  top: 26em;
  left: 19em;
  animation-name: star-twinkle;
  animation-duration: 4.9s;
}

.star:nth-child(4) {
  top: 37em;
  left: 66em;
  animation-name: star-twinkle;
  animation-duration: 5.3s;
}

.star:nth-child(5) {
  top: 21em;
  left: 75em;
  animation-name: star-twinkle;
  animation-duration: 3s;
}

.star:nth-child(6) {
  top: 51em;
  left: 38em;
  animation-name: star-twinkle;
  animation-duration: 2.2s;
}

@keyframes star-twinkle {
  0%, 20% {
    transform: scale(0);
  }
  20%, 100% {
    transform: scale(1);
  }
}

.star-son {
  float: left;
  width: var(--size);
  height: var(--size);
  background-image: radial-gradient(circle var(--size) at var(--pos), transparent var(--size), #fff);
}

.star-son:nth-child(1) {
  --pos: left 0;
}

.star-son:nth-child(2) {
  --pos: right 0;
}

.star-son:nth-child(3) {
  --pos: 0 bottom;
}

.star-son:nth-child(4) {
  --pos: right bottom;
}

/* --- 核心逻辑：当 .dark-mode-active 类存在时，改变样式 --- */
.dark-mode-active .components {
  background-color: rgba(25, 30, 50, 1);
}

.dark-mode-active .main-button {
  transform: translateX(110em);
  background-color: rgba(195, 200, 210, 1);
  box-shadow: 3em 3em 5em rgba(0, 0, 0, 0.5),
  inset -3em -5em 3em -3em rgba(0, 0, 0, 0.5),
  inset 4em 5em 2em -2em rgba(255, 255, 210, 1);
}

.dark-mode-active .daytime-backgrond:nth-of-type(1) {
  transform: translateX(110em);
}

.dark-mode-active .daytime-backgrond:nth-of-type(2) {
  transform: translateX(80em);
}

.dark-mode-active .daytime-backgrond:nth-of-type(3) {
  transform: translateX(50em);
}

.dark-mode-active .cloud,
.dark-mode-active .cloud-light {
  transform: translateY(80em);
}

.dark-mode-active .moon {
  opacity: 1;
}

.dark-mode-active .stars {
  transform: translateY(-62.5em);
  opacity: 1;
}

/* --- 悬停效果 (纯 CSS 实现) --- */
.toggle-container:hover .main-button {
  transform: translateX(10em);
}

.toggle-container:hover .daytime-backgrond:nth-of-type(1) {
  transform: translateX(10em);
}

.toggle-container:hover .daytime-backgrond:nth-of-type(2) {
  transform: translateX(7em);
}

.toggle-container:hover .daytime-backgrond:nth-of-type(3) {
  transform: translateX(4em);
}

/* Dark mode hover */
.toggle-container.dark-mode-active:hover .main-button {
  transform: translateX(100em);
}

.toggle-container.dark-mode-active:hover .daytime-backgrond:nth-of-type(1) {
  transform: translateX(100em);
}

.toggle-container.dark-mode-active:hover .daytime-backgrond:nth-of-type(2) {
  transform: translateX(73em);
}

.toggle-container.dark-mode-active:hover .daytime-backgrond:nth-of-type(3) {
  transform: translateX(46em);
}
</style>