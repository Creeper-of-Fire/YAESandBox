// SliderWithInputWidget.vue
<template>
  <n-flex align="center" :wrap="false" style="width: 100%;">
    <!-- 滑块 -->
    <div style="flex-grow: 1; min-width: 100px; margin-right: 16px;"> <!-- 包裹滑块，控制其伸缩 -->
      <n-slider
          :value="internalValue ?? undefined"
          :min="props.min ?? 0"
          :max="props.max ?? 100"
          :step="props.step ?? 1"
          :disabled="props.disabled || props.readonly"
          @update:value="handleSliderChange"
          :format-tooltip="(value: number) => value.toString()"
          style="width: 100%; margin-right: 16px;"
      />
    </div>

    <!-- 数字输入框 -->
    <n-input-number
        :value="internalValue"
        :min="props.min"
        :max="props.max"
        :placeholder="props.placeholder ?? undefined"
        :step="props.step ?? 1"
        :disabled="props.disabled || props.readonly"
        @update:value="handleInputNumberChange"
        :show-button="disabled"
        style="width: 100px; flex-shrink: 0; margin-left: auto;"
    />
  </n-flex>
</template>

<script setup lang="ts">
import {ref, computed, watch, useAttrs} from 'vue';
import {NInputNumber, NSlider, NCheckbox, NSpace, NEmpty} from 'naive-ui';

// 接收 vue-json-schema-form 传递的标准 props
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
// const attrs = useAttrs(); // 如果 ui:options 是通过 attrs 传递，则需要它

const internalValue = ref<number | null | undefined>(props.modelValue);

// // 从 props.options 中提取约束信息
// const constraints = computed(() => ({
//   minimum: props.min,
//   maximum: props.max,
//   step: props.step,
//   default: props.default,
// }));

watch(() => props.modelValue, (newValue) => {
  // 如果外部值变化，更新内部值
  internalValue.value = newValue;
}, {deep: false}); // 数字类型不需要 deep watch


const handleInputNumberChange = (value: number | null) => {
  // NInputNumber 返回 null 如果清空输入框
  // 我们需要根据 schema 的 type 来决定是否允许 null
  let finalValue: number | null | undefined = value === null ? undefined : value; // NInputNumber 清空是 null

  // 如果 schema 允许 null，并且 NInputNumber 给了 null，则 emit null
  finalValue = value ?? null;

  internalValue.value = finalValue; // 更新内部状态
  emit('update:modelValue', finalValue); // 通知 vue-form 数据变化
};

const handleSliderChange = (value: number) => {
  internalValue.value = value; // 更新内部状态
  emit('update:modelValue', value); // 通知 vue-form 数据变化
};


// 如果初始 modelValue 是 undefined 且 schema 有 default 且不是 nullable，设为 default
// 考虑到 vue-form 应该会处理 default，这里可能不需要，但作为防御性可以加上
// onMounted(() => {
//   if (props.modelValue === undefined && props.schema.default !== undefined && !isNullable.value) {
//     handleInputNumberChange(props.schema.default);
//   }
// });

</script>