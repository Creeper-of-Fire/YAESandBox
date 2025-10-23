<!-- PromptEditPanel.vue -->
<template>
  <!--
    由于我们是通过 useModal.create() 来动态创建这个组件，
    所以它不需要自己的 NModal 包装器。
    我们直接从内容卡片开始。
  -->
  <n-card :bordered="false" aria-modal="true" class="prompt-edit-modal" role="dialog" size="huge">
    <n-form ref="formRef" :model="formData" :rules="formRules">
      <n-grid :cols="6" :x-gap="24">
        <!-- 名称 -->
        <n-form-item-gi :span="3" label="名称" path="name">
          <n-input
              v-model:value="formData.name"
              :disabled="isMarker"
              placeholder="此提示词的名称"
          />
        </n-form-item-gi>

        <!-- 角色 -->
        <n-form-item-gi :span="3" label="角色" path="role">
          <n-select
              v-model:value="formData.role"
              :disabled="isMarker"
              :options="roleOptions"
              placeholder="此消息应归于谁"
          />
        </n-form-item-gi>

        <!-- 位置 -->
        <n-form-item-gi :span="isHistoryRelative ? 2 : 3" label="位置" path="injection_position">
          <n-select
              v-model:value="formData.injection_position"
              :disabled="isMarker"
              :options="positionOptions"
              placeholder="相对(相对于提示管理器中的其他提示) 或在聊天中深度。"
          />
        </n-form-item-gi>


        <!-- 只有在 "聊天中" 位置时才显示深度和顺序 -->

        <!-- 深度 -->
        <n-form-item-gi v-if="isHistoryRelative" :span="2" label="深度" path="injection_depth">
          <n-input-number
              v-model:value="formData.injection_depth"
              :disabled="isMarker || !isHistoryRelative"
              class="w-full"
              placeholder="0=最后一条消息之后, 1=之前, etc."
          />
        </n-form-item-gi>
        <!-- 顺序 -->
        <n-form-item-gi v-if="isHistoryRelative" :span="2" label="Order" path="injection_order">
          <template #label>
            Order
            <n-popover trigger="hover">
              <template #trigger>
                <n-icon class="ml-1 cursor-help">
                  <HelpCircleIcon/>
                </n-icon>
              </template>
              当多个提示注入到相同的深度时，此值用于排序。值从小到大排列。
            </n-popover>
          </template>
          <n-input-number
              v-model:value="formData.injection_order"
              :disabled="isMarker || !isHistoryRelative"
              class="w-full"
              placeholder="100"
          />
        </n-form-item-gi>
      </n-grid>

      <n-divider/>

      <!-- 提示词 -->
      <n-form-item label="提示词" path="content">
        <div v-if="isMarker" class="marker-content-placeholder">
          <n-text depth="3">
            此提示词的内容是从其他地方提取的，无法在此处进行编辑。
            <br>
            <n-text strong>
              Source: {{ formData.name }}
            </n-text>
          </n-text>
        </div>
        <n-input
            v-else
            v-model:value="formData.content"
            :autosize="{ minRows: 8, maxRows: 20 }"
            placeholder="要发送的提示词。支持 {{variable}} 语法。"
            type="textarea"
        />
      </n-form-item>
    </n-form>

    <template #footer>
      <div class="modal-footer">
        <n-button @click="handleCancel">
          <template #icon>
            <n-icon :component="CloseIcon"/>
          </template>
          取消
        </n-button>
        <n-button type="primary" @click="handleSave">
          <template #icon>
            <n-icon :component="SaveIcon"/>
          </template>
          保存
        </n-button>
      </div>
    </template>
  </n-card>
</template>

<script lang="ts" setup>
import {computed, reactive, ref} from 'vue';
import {
  type FormInst,
  type FormRules,
  NButton,
  NCard,
  NDivider,
  NForm,
  NFormItem,
  NFormItemGi,
  NGrid,
  NIcon,
  NInput,
  NInputNumber,
  NSelect,
  NText,
  NPopover,
  useThemeVars,
} from 'naive-ui';
import {CloseIcon as CloseIcon, HelpCircleIcon, SaveIcon as SaveIcon} from '@yaesandbox-frontend/shared-ui/icons';
import type {ContentPromptItem, MarkerPromptItem, PromptItem} from './sillyTavernPreset';

const props = defineProps<{
  initialValue: Partial<PromptItem>;
}>();

const emit = defineEmits<{
  (e: 'save', item: PromptItem): void;
  (e: 'cancel'): void;
}>();


// 2. 响应式表单状态
// 使用 reactive 创建一个本地副本，避免直接修改 props
// 并为新建条目提供健全的默认值
const formData = reactive({
  identifier: props.initialValue.identifier ?? crypto.randomUUID(),
  name: props.initialValue.name ?? '新提示词',
  system_prompt: props.initialValue.system_prompt ?? false,
  marker: props.initialValue.marker ?? false,
  role: props.initialValue.role ?? 'system',
  content: props.initialValue.content ?? '',
  injection_position: props.initialValue.injection_position ?? 0,
  injection_depth: props.initialValue.injection_depth ?? 0,
  injection_order: props.initialValue.injection_order ?? 100,
});


// 3. 计算属性，用于控制 UI 逻辑
const isMarker = computed(() => formData.marker);
const isHistoryRelative = computed(() => formData.injection_position === 1);

// 4. 表单验证
const formRef = ref<FormInst | null>(null);
const formRules: FormRules = {
  name: {required: true, message: '名称不能为空', trigger: ['input', 'blur']},
  content: {
    // 只有在不是标记时，内容才是必须的
    validator: (_, value) =>
    {
      if (!isMarker.value && !value)
      {
        return new Error('提示词内容不能为空');
      }
      return true;
    },
    trigger: ['input', 'blur'],
  },
};


// 5. Select 选项
const roleOptions = [
  {label: '系统 (System)', value: 'system'},
  {label: '用户 (User)', value: 'user'},
  {label: '助手 (Assistant)', value: 'assistant'},
];

const positionOptions = [
  {label: '相对', value: 0},
  {label: '聊天中', value: 1},
];


// 6. 事件处理
const handleSave = () =>
{
  formRef.value?.validate(errors =>
  {
    if (!errors)
    {
      // 验证通过，构建最终的 PromptItem 对象
      let finalItem: PromptItem;

      if (formData.marker)
      {
        finalItem = {
          identifier: formData.identifier,
          name: formData.name,
          marker: true,
          system_prompt: false, // Marker item 通常没有这些属性
        } as MarkerPromptItem;
      }
      else
      {
        finalItem = {
          identifier: formData.identifier,
          name: formData.name,
          marker: false,
          system_prompt: formData.role === 'system', // system_prompt 通常与 role 联动
          role: formData.role,
          content: formData.content,
          injection_position: formData.injection_position,
          // 只有在深度注入模式下才保留这些值，保持 JSON 清洁
          injection_depth: isHistoryRelative.value ? formData.injection_depth : undefined,
          injection_order: isHistoryRelative.value ? formData.injection_order : undefined,
        } as ContentPromptItem;
      }

      emit('save', finalItem);
    }
  });
};

const handleCancel = () =>
{
  emit('cancel');
};

const themeVars = useThemeVars();
</script>

<style scoped>
.prompt-edit-modal {
  /* 可以在这里添加特定的样式 */
}

.marker-content-placeholder {
  width: 100%;
  min-height: 180px; /* 与 textarea 的 min-height 保持一致 */
  padding: 10px;
  background-color: v-bind('themeVars.inputColorDisabled');
  border-radius: 4px;
  border: 1px solid v-bind('themeVars.borderColor');
  display: flex;
  align-items: center;
  justify-content: center;
  text-align: center;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.w-full {
  width: 100%;
}

.ml-1 {
  margin-left: 4px;
}

.cursor-help {
  cursor: help;
}
</style>