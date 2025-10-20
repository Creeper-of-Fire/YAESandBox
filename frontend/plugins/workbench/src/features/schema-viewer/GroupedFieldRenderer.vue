<template>
  <div class="grouped-field-renderer">
    <template v-for="(group, groupIndex) in groupedFields" :key="groupIndex">
      <!-- 1. 渲染分组标题 -->
      <n-divider
          v-if="group.groupName && showGroupTitles"
          :style="{ marginTop: groupIndex === 0 ? '0' : '1rem', marginBottom: '1rem' }"
          title-placement="left"
      >
        {{ group.groupName }}
      </n-divider>

      <!-- 2. 渲染字段行 -->
      <div class="form-row">
        <template v-for="field in group.fields" :key="field.name">
          <div :class="{ 'full-width-if-single': group.fields.length === 1 }" class="form-field-container">
            <!-- 3. 使用作用域插槽将渲染控制权交还给父组件 -->
            <slot :field="field" :group="group"></slot>
          </div>
        </template>
      </div>
    </template>
  </div>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {NDivider} from 'naive-ui';
import type {FormFieldViewModel} from '#/features/schema-viewer/preprocessSchema';

const props = withDefaults(defineProps<{
  fields: FormFieldViewModel[];
  showGroupTitles?: boolean;
}>(), {
  showGroupTitles: true,
});

const groupedFields = computed(() =>
{
  if (!props.fields || props.fields.length === 0)
  {
    return [];
  }

  const result: Array<{ groupName: string | null; fields: FormFieldViewModel[] }> = [];
  const processedGroups = new Set<string>();

  props.fields.forEach(field =>
  {
    if (field.inlineGroup)
    {
      if (!processedGroups.has(field.inlineGroup))
      {
        const fieldsInGroup = props.fields.filter(f => f.inlineGroup === field.inlineGroup);
        result.push({
          groupName: field.inlineGroup,
          fields: fieldsInGroup
        });
        processedGroups.add(field.inlineGroup);
      }
    }
    else
    {
      result.push({
        groupName: null,
        fields: [field]
      });
    }
  });

  return result;
});
</script>

<style scoped>
.form-row {
  display: flex;
  gap: 1rem;
  margin-bottom: 1rem;
}

.form-row:last-child {
  margin-bottom: 0;
}

.form-field-container {
  min-width: 0;
  flex: 1; /* 默认让组内元素平分宽度 */
}

.form-field-container.full-width-if-single {
  /* 当组内只有一个元素时，让它填满剩余空间 */
  flex: 1;
}
</style>