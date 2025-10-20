<template>
  <div class="field-array-widget">
    <div v-for="(item, index) in internalValue" :key="index" class="array-item">
      <n-card :title="`${arrayTitle || ''} #${index + 1}`" size="small">
        <GroupedFieldRenderer v-slot="{ field }" :fields="itemFieldsTemplate" :show-group-titles=false>
          <FormFieldWrapper
              :description="field.description"
              :isSingleInGroup="false"
              :label="field.label"
              :name="`${name}[${index}].${field.name}`"
          >
            <component
                :is="componentMap[field.component]"
                v-model:value="internalValue[index][field.name]"
                v-bind="field.props"
                @update:value="onInternalChange"
            />
          </FormFieldWrapper>
        </GroupedFieldRenderer>

        <template #header-extra>
          <n-button circle size="tiny" type="error" @click="removeItem(index)">
            <template #icon>
              <n-icon :component="DeleteIcon"/>
            </template>
          </n-button>
        </template>
      </n-card>
    </div>

    <n-button block dashed style="margin-top: 1rem;" type="primary" @click="addItem">
      <template #icon>
        <n-icon :component="AddIcon"/>
      </template>
      添加新项目
    </n-button>
  </div>
</template>

<script lang="ts" setup>
import {type Component, type PropType, ref, watch} from 'vue';
import {NButton, NCard, NIcon} from 'naive-ui';
import {AddIcon, DeleteIcon} from '@yaesandbox-frontend/shared-ui/icons';
import FormFieldWrapper from '../FormFieldWrapper.vue';
import type {FormFieldViewModel} from '../preprocessSchema';
import {cloneDeep} from 'lodash-es';
import GroupedFieldRenderer from "#/features/schema-viewer/GroupedFieldRenderer.vue";

const props = defineProps({
  name: {type: String, required: true},
  modelValue: {type: Array as PropType<any[]>, default: () => []},
  itemFieldsTemplate: {type: Array as PropType<FormFieldViewModel[]>, required: true},
  itemDefaults: {type: Object as PropType<Record<string, any>>, required: true},
  componentMap: {type: Object as PropType<Record<string, Component>>, required: true},
  arrayTitle: { type: String, default: '项目' }
});

const emit = defineEmits(['update:modelValue']);

// 内部状态，与 modelValue 同步
const internalValue = ref<any[]>(cloneDeep(props.modelValue) ?? []);

watch(() => props.modelValue, (newValue) =>
{
  // 外部变化时同步内部状态，用 JSON 字符串比较避免无限循环
  if (JSON.stringify(newValue) !== JSON.stringify(internalValue.value))
  {
    internalValue.value = cloneDeep(newValue) ?? [];
  }
}, {deep: true});

// 当内部任何数据变化时调用此函数
const onInternalChange = () =>
{
  emit('update:modelValue', cloneDeep(internalValue.value));
};

const addItem = () =>
{
  internalValue.value.push(cloneDeep(props.itemDefaults));
  onInternalChange();
};

const removeItem = (index: number) =>
{
  internalValue.value.splice(index, 1);
  onInternalChange();
};
</script>

<style scoped>
.field-array-widget {
  width: 100%;
}

.array-item {
  margin-bottom: 1rem;
}
</style>