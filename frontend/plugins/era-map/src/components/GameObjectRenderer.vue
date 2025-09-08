<template>
  <!-- Konva组件需要一个共同的根，v-group是理想选择 -->
  <v-group :config="groupConfig">
    <!-- 渲染器 1: 形状 -->
    <template v-if="renderConfig.renderType === EnumRenderType.SHAPE">
      <v-rect v-if="renderConfig.shape === 'rect'" :config="shapeConfig"/>
      <v-circle v-if="renderConfig.shape === 'circle'" :config="shapeConfig"/>
    </template>

    <!-- 渲染器 2: 图片 -->
    <template v-if="renderConfig.renderType === RenderType.SPRITE">
      <v-image
          v-for="(component, index) in renderConfig.components"
          :key="index"
          :config="getSpriteComponentConfig(component)"
      />
    </template>
  </v-group>
</template>

<script lang="ts" setup>
import {computed, toRefs} from 'vue';
import {GameObject} from '#/game/GameObject';
import {RenderType, type ShapeRenderConfig, type SpriteComponent} from '#/game/types';
import {TILE_SIZE} from "#/constant.ts";
import type {Tileset} from "#/game/Tileset.ts"; // 注意：这是图集瓦片大小

// --- Props ---
const props = defineProps<{
  gameObject: GameObject;
  tileset: Tileset | null;
}>();

const {gameObject, tileset} = toRefs(props);
const renderConfig = computed(() => gameObject.value.config.renderConfig);

// --- Konva 配置计算 ---

// v-group 用于处理定位和旋转，使其内部的形状/图片无需关心这些
const groupConfig = computed(() => ({
  x: gameObject.value.position.x,
  y: gameObject.value.position.y,
  rotation: gameObject.value.rotation,
  // 设置偏移量，使旋转和定位的中心点在物体中心
  offsetX: gameObject.value.size.width / 2,
  offsetY: gameObject.value.size.height / 2,
}));

// 形状渲染的配置
const shapeConfig = computed(() =>
{
  const config = gameObject.value.config.renderConfig as ShapeRenderConfig;
  const common = {
    fill: config.fill,
    stroke: config.stroke || 'transparent',
    strokeWidth: 2,
  };
  if (config.shape === 'rect')
  {
    return {
      ...common,
      width: gameObject.value.size.width,
      height: gameObject.value.size.height,
    };
  }
  if (config.shape === 'circle')
  {
    return {
      ...common,
      radius: config.radius || gameObject.value.size.width / 2,
    };
  }
  return {};
});

function getSpriteComponentConfig(component: SpriteComponent) {
  const { tileId, offset } = component;

  // 如果没有提供图集，则不渲染
  if (!props.tileset) {
    return {};
  }

  return {
    x: offset.x * TILE_SIZE,
    y: offset.y * TILE_SIZE,
    image: props.tileset.image,
    // 目标渲染尺寸始终是TILE_SIZE
    width: TILE_SIZE,
    height: TILE_SIZE,
    // 使用tileset实例的方法来获取裁剪信息，不再硬编码
    crop: props.tileset.getTileCrop(tileId),
  };
}

// 导出RenderType，以便在模板中使用
const EnumRenderType = RenderType;
</script>