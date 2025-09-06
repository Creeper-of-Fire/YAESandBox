<template>
  <div id="simulation-container" ref="containerRef" style="width: 800px; height: 600px; border: 1px solid #ccc;">
    <v-stage v-if="containerSize.width > 0" :config="stageConfig">
      <FieldVisualizer v-if="showField" :grid="potentialGrid"/>

      <v-layer ref="layerRef">
        <v-group
            v-for="renderable in renderables"
            :key="renderable.id"
        >
          <component
              :is="renderable.shape.component"
              :config="renderable.shape.config"
          />
          <!-- 如果存在，渲染该物体所受的力矢量箭头 -->
          <v-arrow
              v-if="renderable.force"
              :config="renderable.force"
          />
        </v-group>
      </v-layer>
    </v-stage>
  </div>
</template>

<script lang="ts" setup>
import {computed, onMounted, onUnmounted, ref, shallowRef} from 'vue';
import {Simulation} from './Simulation.ts';
import type {SimulationConfigDTO} from './SimulationConfig.ts';
import tableBlueprint from './blueprints/table.json';
import wallBlueprint from './blueprints/wall.json';
import FieldVisualizer from "#/simulation/FieldVisualizer.vue";


const simulationConfig: SimulationConfigDTO = {
  blueprints: [tableBlueprint, wallBlueprint],
  spawnRequests: [
    {blueprintType: 'TABLE', id: 'table1', name: 'Big Table', initialPosition: {x: 350, y: 300}},
    {blueprintType: 'TABLE', id: 'table2', name: 'Side Table', initialPosition: {x: 450, y: 300}},
    {blueprintType: 'WALL', id: 'wall_bottom', name: 'Bottom Wall', initialPosition: {x: 400, y: 590}},
  ]
};

const containerRef = ref<HTMLDivElement | null>(null);
const containerSize = ref({width: 0, height: 0});
const showForces = ref(true);
// 使用 shallowRef，因为我们不需要 Vue 深度代理整个 Simulation 实例
const simulation = shallowRef<Simulation | null>(null);

const renderables = shallowRef<any[]>([]);

// renderables 是一个计算属性，它将物理世界的数据转换为渲染层所需的数据结构
/**
 * 一个独立的、纯粹的函数，用于将物理世界状态转换为渲染数据。
 * 这使得我们的 gameLoop 更清晰。
 */
function updateRenderables()
{
  if (!simulation.value)
  {
    renderables.value = [];
    return;
  }

  const FORCE_SCALE_FACTOR = 5000;
  const bodies = simulation.value.getAllBodies();
  const forces = simulation.value.forceCache;

  // 直接构建一个新的数组
  const newRenderables = bodies.map(body =>
  {
    const gameObject = simulation.value?.getGameObjectByBodyId(body.id);
    const shapeRenderable = gameObject
        ? gameObject.shape.getRenderConfig(body)
        : {component: 'v-text', config: {text: 'ERROR', x: body.position.x, y: body.position.y}};

    let forceRenderable = null;

    // --- 从缓存中读取力 ---
    const lastForce = forces.get(body.id);

    if (showForces.value && lastForce)
    {
      const lastForceX = lastForce.x * FORCE_SCALE_FACTOR;
      const lastForceY = lastForce.y * FORCE_SCALE_FACTOR;
      if (lastForceX || lastForceY){
        forceRenderable = {
          points: [body.position.x, body.position.y, body.position.x + lastForceX, body.position.y + lastForceY],
          pointerLength: 10, pointerWidth: 10, fill: 'red', stroke: 'red', strokeWidth: 2,
        };
      }

    }

    return {
      id: body.id,
      shape: shapeRenderable,
      force: forceRenderable,
    };
  });

  // --- 手动为 ref 赋一个新的数组值 ---
  // 这会可靠地触发 Vue 的更新。
  renderables.value = newRenderables;
  // ---------------------------------------------
}

let animationFrameId: number;

const stageConfig = computed(() => ({
  width: containerSize.value.width,
  height: containerSize.value.height,
}));

const showField = ref(true);
const potentialGrid = ref<number[][]>([]);
const gridCellSize = 20; // 渲染层的配置
const gridWidth = computed(() => Math.ceil(stageConfig.value.width / gridCellSize));
const gridHeight = computed(() => Math.ceil(stageConfig.value.height / gridCellSize));


const layerRef = ref(null);

let frameCount = 0;

const gameLoop = () =>
{
  if (simulation.value)
  {
    simulation.value.update(1000 / 60);

    updateRenderables();

    if (showField.value && frameCount % 10 === 0)
    {
      // 在需要时调用，并传入渲染层管理的参数
      potentialGrid.value = simulation.value.calculatePotentialGrid(
          gridWidth.value,
          gridHeight.value,
          gridCellSize
      );
    }

    frameCount++;
  }

  animationFrameId = requestAnimationFrame(gameLoop);
};

onMounted(() =>
{
  if (containerRef.value)
  {
    containerSize.value = {
      width: containerRef.value.offsetWidth,
      height: containerRef.value.offsetHeight,
    };
  }

  // 模拟器现在在 onMounted 中使用配置进行实例化
  simulation.value = new Simulation(simulationConfig);
  gameLoop();
});

onUnmounted(() =>
{
  cancelAnimationFrame(animationFrameId);
});

</script>