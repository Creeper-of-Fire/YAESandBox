<template>
  <!-- 1. 如果当前 schema 节点有自定义渲染器，则渲染它 -->
  <component
      v-if="schemaNode?.['ui:custom-renderer']"
      :is="schemaNode['ui:custom-renderer']"
      v-model="model"
      :schema="schemaNode"
  />

  <!-- 2. 如果当前节点是对象，则递归遍历其属性 -->
  <template v-else-if="schemaNode?.type === 'object' && schemaNode.properties && model">
    <CustomFieldRenderer
        v-for="(propSchema, propName) in schemaNode.properties"
        :key="propName"
        :schema-node="propSchema"
        v-model="model[propName]"
    />
  </template>

  <!-- 3. 如果当前节点是数组，则递归遍历其元素 -->
  <!-- 注意：这里只处理了 item 是 object 的简单情况，可以根据需要扩展 -->
  <template v-else-if="schemaNode?.type === 'array' && schemaNode.items && Array.isArray(model)">
    <div v-for="(item, index) in model" :key="index">
      <CustomFieldRenderer
          :schema-node="schemaNode.items"
          v-model="model[index]"
      />
    </div>
  </template>
</template>

<script lang="ts">
// 必须使用 defineComponent 并命名，才能实现递归自调用
import { defineComponent, computed, type PropType } from 'vue';

export default defineComponent({
  name: 'CustomFieldRenderer',
  props: {
    schemaNode: {
      type: Object as PropType<Record<string, any>>,
      required: true,
    },
    modelValue: {
      type: [Object, Array, String, Number, Boolean, null] as PropType<any>,
      required: true,
    },
  },
  emits: ['update:modelValue'],
  setup(props, { emit }) {
    // 使用可写的 computed 属性来代理 v-model
    const model = computed({
      get: () => props.modelValue,
      set: (newValue) => {
        emit('update:modelValue', newValue);
      },
    });

    return {
      model,
    };
  },
});
</script>