<template>
  <n-layout style="height: 100%; display: flex; flex-direction: column;">
    <!-- Tabs 作为控制器 -->
    <n-tabs
        v-model:value="activeTab"
        animated
        style="padding: 12px; flex-shrink: 0;"
        type="line"
    >
      <n-tab name="creator">
        创造者模式
      </n-tab>
      <n-tab name="player">
        玩家模式
      </n-tab>
    </n-tabs>

    <!--
      内容区域。关键在于这里：
      我们使用一个 flex-grow 的容器来包裹两个视图，
      并用 v-show 来控制它们的可见性。
      这样可以保证两个组件实例都不会被销毁。
    -->
    <div style="flex-grow: 1; ">
      <!-- 使用 position: absolute 可以让两个组件在DOM流中重叠，
           v-show 会完美地处理显示哪一个。
           这可以避免 display: none 带来的微小布局抖动。 -->
      <CreatorView v-show="activeTab === 'creator'" class="view-pane"/>
      <EraMapView v-show="activeTab === 'player'" class="view-pane"/>
    </div>
  </n-layout>
</template>

<script lang="ts" setup>
import {onMounted} from 'vue';
import {NLayout, NTab, NTabs} from 'naive-ui';
import {useWorldInitializer} from '#/composables/useWorldInitializer';

// 导入两个主视图
import CreatorView from './CreatorView.vue';
import EraMapView from './EraMapView.vue';
import {useScopedStorage} from "@yaesandbox-frontend/core-services/composables";
// 当前激活的标签页
const activeTab = useScopedStorage('activeGameSessionView', 'creator');
// 使用初始化器
const {initialize} = useWorldInitializer();

// 在这个“会话”组件挂载时，执行一次且仅一次初始化
onMounted(async () =>
{
  await initialize();
});
</script>

<style scoped>


</style>