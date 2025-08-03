<template>
  <!-- 1. 如果当前 schema 节点有自定义渲染器，则渲染它 -->
  <component
      :is="schemaNode['ui:custom-renderer-property']"
      v-if="schemaNode?.['ui:custom-renderer-property']"
      v-model="model"
      :schema="schemaNode"
  />

  <!-- 2. 如果当前节点是对象，则递归遍历其属性 -->
  <template v-else-if="schemaNode?.type === 'object' && schemaNode.properties && model">
    <CustomFieldRenderer
        v-for="(propSchema, propName) in schemaNode.properties"
        :key="propName"
        v-model="model[propName]"
        :schema-node="propSchema"
    />
  </template>

  <!-- 3. 如果当前节点是数组，则递归遍历其元素 -->
  <!-- 注意：这里只处理了 item 是 object 的简单情况，可以根据需要扩展 -->
  <template v-else-if="schemaNode?.type === 'array' && schemaNode.items && Array.isArray(model)">
    <div v-for="(item, index) in model" :key="index">
      <CustomFieldRenderer
          v-model="model[index]"
          :schema-node="schemaNode.items"
      />
    </div>
  </template>
</template>

<script lang="ts">
// 必须使用 defineComponent 并命名，才能实现递归自调用
import {computed, defineComponent, type PropType} from 'vue';

export default defineComponent({
  name: 'CustomFieldRenderer',
  props: {
    schemaNode: {
      type: Object as PropType<Record<string, any>>,
      required: true,
    },
    modelValue: {
      type: [Object, Array, String, Number, Boolean, null] as PropType<any>,
      // 不再是 required，因为 undefined 是一个有效且常见的情况
      default: undefined,
    },
  },
  emits: ['update:modelValue'],
  setup(props, {emit})
  {
    // 使用可写的 computed 属性来代理 v-model
    const model = computed({
      get: () =>
      {
        // 【核心修改】
        // 如果 modelValue 非空，直接返回
        if (props.modelValue !== undefined && props.modelValue !== null)
        {
          return props.modelValue;
        }

        // 如果 modelValue 是 undefined 或 null，则根据 schema 类型提供一个安全的默认值
        // 这可以防止模板中出现 "cannot read properties of null" 的错误
        if (props.schemaNode?.type === 'object')
        {
          return {}; // 对象类型默认为空对象
        }
        if (props.schemaNode?.type === 'array')
        {
          return []; // 数组类型默认为空数组
        }

        // 其他原始类型，返回 null 是安全的
        return null;
      },
      set: (newValue) =>
      {
        emit('update:modelValue', newValue ?? null);
      },
    });

    return {
      model,
    };
  },
});
</script>