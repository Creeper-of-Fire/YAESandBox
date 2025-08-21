// SliderWithInputWidget.vue
<template>
  <n-flex :wrap="false" align="center" style="width: 100%;">
    <!-- 滑块 -->
    <div style="flex-grow: 1; min-width: 100px; margin-right: 16px;"> <!-- 包裹滑块，控制其伸缩 -->
      <n-slider
          v-model:value="model"
          :disabled="props.disabled || props.readonly"
          :format-tooltip="(value: number) => value.toString()"
          :max="props.max ?? 100"
          :min="props.min ?? 0"
          :tuum="props.tuum ?? 1"
          style="width: 100%; margin-right: 16px;"
      />
    </div>

    <!-- 数字输入框 -->
    <n-input-number
        v-model:value="model"
        :disabled="props.disabled || props.readonly"
        :max="props.max"
        :min="props.min"
        :placeholder="props.placeholder ?? undefined"
        :show-button="disabled"
        :tuum="props.tuum ?? 1"
        style="width: 100px; flex-shrink: 0; margin-left: auto;"
    />
  </n-flex>
</template>

<script lang="ts" setup>
import {NInputNumber, NSlider} from 'naive-ui';
import {useVModel} from "@vueuse/core";

// 接收 vue-json-schema-viewer-form 传递的标准 props
const props = defineProps<{
  modelValue: number | null | undefined; // 可能是数字、null、undefined
  disabled?: boolean;
  readonly?: boolean;
  placeholder?: string;
  // 接收从 ui:options 传递的约束信息和 nullable 标记
  min?: number;
  max?: number;
  tuum?: number;
  default?: number;
}>();

const emit = defineEmits(['update:modelValue']);

const model = useVModel(props, 'modelValue', emit, {
  passive: true, // 仅在 modelValue 存在时才进行双向绑定
  // 如果父组件没有提供 modelValue，可以提供一个默认值，
  // 比如 props.default 或者 null
  defaultValue: props.default ?? null,
});

</script>