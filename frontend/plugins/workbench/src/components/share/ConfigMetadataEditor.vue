<template>
  <div v-if="draft" class="metadata-editor">
    <n-collapse v-model:expanded-names="expandedNames">
      <n-collapse-item name="meta" title="元数据与引用">
        <n-form label-placement="top">
          <!-- 描述 -->
          <n-form-item label="描述">
            <n-input
                v-model:value="description"
                :disabled="isReadOnly"
                placeholder="为这个配置添加一些描述信息"
                type="textarea"
            />
          </n-form-item>

          <!-- 标签 -->
          <n-form-item label="标签">
            <n-dynamic-tags v-model:value="tags" :disabled="isReadOnly"/>
          </n-form-item>

          <!-- StoreRef -->
          <n-card :bordered="true" size="small" title="版本引用 (可选)">
            <n-form-item label="引用ID (RefId)">
              <n-input
                  v-model:value="refId"
                  :disabled="isReadOnly"
                  placeholder="例如：yae.templates.standard-rag"
              />
            </n-form-item>
            <n-form-item label="版本">
              <n-input
                  v-model:value="refVersion"
                  :disabled="isReadOnly"
                  placeholder="例如：1.0.0"
              />
            </n-form-item>
          </n-card>
        </n-form>
      </n-collapse-item>
    </n-collapse>
  </div>
</template>

<script lang="ts" setup>
import {computed, type PropType} from 'vue';
import {NCard, NCollapse, NCollapseItem, NDynamicTags, NForm, NFormItem, NInput} from 'naive-ui';
import type {AnyResourceItemSuccess} from '#/services/GlobalEditSession.ts';
import {useSelectedConfig} from "#/services/editor-context/useSelectedConfig.ts";
import {useScopedStorage} from "@yaesandbox-frontend/core-services/composables";

const props = defineProps({
  draft: {
    type: Object as PropType<AnyResourceItemSuccess>,
    required: true,
  },
});

const { isReadOnly } = useSelectedConfig();
const expandedNames = useScopedStorage<string[]>('workbench-metadata-editor-expanded', ['meta']);

// --- 使用 Computed Properties + Get/Set 实现双向绑定 ---

const description = computed({
  get: () => props.draft.meta?.description || '',
  set: (value) => {
    if (!props.draft.meta) {
      props.draft.meta = {};
    }
    props.draft.meta.description = value;
  },
});

const tags = computed({
  get: () => props.draft.meta?.tags || [],
  set: (value) => {
    if (!props.draft.meta) {
      props.draft.meta = {};
    }
    props.draft.meta.tags = value;
  },
});

const refId = computed({
  get: () => props.draft.storeRef?.refId || '',
  set: (value) => {
    if (!props.draft.storeRef) {
      // 如果 refId 和 version 都是空的，则将 storeRef 设为 undefined
      if (!value && !refVersion.value) {
        props.draft.storeRef = undefined;
        return;
      }
      props.draft.storeRef = { refId: '', version: '' };
    }
    props.draft.storeRef.refId = value;
  },
});

const refVersion = computed({
  get: () => props.draft.storeRef?.version || '',
  set: (value) => {
    if (!props.draft.storeRef) {
      if (!value && !refId.value) {
        props.draft.storeRef = undefined;
        return;
      }
      props.draft.storeRef = { refId: '', version: '' };
    }
    props.draft.storeRef.version = value;
  },
});
</script>

<style scoped>
.metadata-editor {
  margin-top: 16px;
}
</style>