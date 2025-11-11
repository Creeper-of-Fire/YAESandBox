<!-- src/components/CharacterAvatar.vue -->
<template>
  <div :style="wrapperStyle" class="character-avatar-wrapper">
    <!-- 1. 渲染图片 -->
    <img v-if="isUrl" :src="character.avatar" alt="avatar" class="avatar-image"/>

    <!-- 2. 渲染单字或短文本 -->
    <span v-else-if="displayText" :style="textStyle" class="avatar-text">
      {{ displayText }}
    </span>

    <!-- 3. 渲染生成的SVG Identicon -->
    <div v-else v-html="identiconSvg" class="avatar-svg-wrapper"></div>
  </div>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {minidenticon} from 'minidenticons';
import {useThemeVars} from 'naive-ui';
import type {Character} from '#/types/models';

const props = defineProps<{
  character: Character;
  size: number;
}>();

const themeVars = useThemeVars();

// --- 逻辑判断计算属性 ---

const avatarContent = computed(() => props.character.avatar || '');

const isUrl = computed(() =>
    avatarContent.value.startsWith('http') || avatarContent.value.startsWith('/')
);

// 使用 [...string] 方式可以正确处理 Emoji 的长度
const graphemeLength = computed(() => [...avatarContent.value].length);

// 计算最终应该显示的文本（1-3个字符）
const displayText = computed(() =>
{
  if (isUrl.value) return null; // 如果是URL，不显示文本
  if (graphemeLength.value >= 1 && graphemeLength.value <= 3)
  {
    return avatarContent.value;
  }
  return null;
});

// 仅在需要时（即无URL且无显示文本时）生成 Identicon
const identiconSvg = computed(() =>
{
  if (!isUrl.value && !displayText.value)
  {
    // 使用角色的 name 作为种子，保证名字和头像的关联性
    return minidenticon(props.character.name);
  }
  return '';
});

// --- 样式计算属性 ---

const wrapperStyle = computed(() => ({
  width: `${props.size}px`,
  height: `${props.size}px`,
  borderRadius: '50%', // 总是圆形的
  backgroundColor: isUrl.value ? themeVars.value.avatarColor : (displayText.value ? themeVars.value.avatarColor : themeVars.value.cardColor),
  fontSize: `${props.size * 0.5}px`, // 基础字体大小
}));

const textStyle = computed(() => ({
  color: themeVars.value.textColor1,
  // 根据字符数量微调字体大小，以获得更好的视觉效果
  fontSize: `${props.size / (graphemeLength.value > 1 ? 1.8 : 1.5)}px`
}));

</script>

<style scoped>
.character-avatar-wrapper {
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  font-weight: bold;
  user-select: none;
  border: 1px solid v-bind('themeVars.borderColor');
  box-sizing: border-box;
}

.avatar-image {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.avatar-text {
  line-height: 1; /* 确保文字垂直居中 */
}

.avatar-svg-wrapper {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>