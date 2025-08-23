// SliderWithInputWidget.vue
<template>
  <n-flex :wrap="false" align="center" style="width: 100%;">
    <!-- 滑块 -->
    <div style="flex-grow: 1; min-width: 100px; margin-right: 16px;"> <!-- 包裹滑块，控制其伸缩 -->
      <n-slider
          v-model:value="sliderModel"
          :disabled="props.disabled || props.readonly"
          :format-tooltip="(value: number) => value.toString()"
          :max="props.max ?? 100"
          :min="props.min ?? 0"
          :step="props.step ?? 1"
          style="width: 100%; margin-right: 16px;"
      />
    </div>

    <!-- 数字输入框 -->
    <n-input-number
        v-model:value="directModel"
        :disabled="props.disabled || props.readonly"
        :max="props.max"
        :min="props.min"
        :placeholder="props.placeholder ?? undefined"
        :show-button="disabled"
        :step="props.step ?? 1"
        style="width: 100px; flex-shrink: 0; margin-left: auto;"
    />
  </n-flex>
</template>

<script lang="ts" setup>
import {NInputNumber, NSlider} from 'naive-ui';
import {useVModel} from "@vueuse/core";
import {computed} from "vue";

// 接收 vue-json-schema-viewer-form 传递的标准 props
const props = defineProps<{
  modelValue: number | null | undefined; // 可能是数字、null、undefined
  disabled?: boolean;
  readonly?: boolean;
  placeholder?: string;
  // 接收从 ui:options 传递的约束信息和 nullable 标记
  min?: number;
  max?: number;
  step?: number;
  default?: number;
}>();

const emit = defineEmits(['update:modelValue']);

const directModel = useVModel(props, 'modelValue', emit, {
  passive: true, // 使用watch模式
  // 如果父组件没有提供 modelValue，可以提供一个默认值，
  // 比如 props.default 或者 null
  defaultValue: props.default ?? null,
});

const sliderModel = computed({
  // Getter：当父组件的值是 null 时，返回 undefined 给 slider
  get() {
    // 如果 directModel 的值是 null，就返回 undefined，否则返回原值
    return directModel.value === null ? undefined : directModel.value;
  },
  // Setter：当 slider 改变值时，直接 emit 这个新值
  set(newValue: number | undefined) {
    // slider 拖动时 newValue 会是 number，
    // 我们直接更新父组件的状态。
    // 如果 newValue 变为 undefined（虽然在 slider 中不常见），我们也更新为 null 以保持一致性
    emit('update:modelValue', newValue ?? null);
  }
});
</script>