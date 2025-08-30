<!-- PresetEditor.vue -->
<template>
  <div class="preset-editor">
    <div class="toolbar">
      <n-select
          v-model:value="selectedCharacterId"
          :options="characterIdOptions"
          placeholder="选择角色配置"
          style="width: 200px;"
      />
      <n-button @click="handleAddNewPrompt">
        添加新提示词
      </n-button>
    </div>

    <div v-if="preset" class="editor-layout">
      <!-- 可用提示词 (定义池) -->
      <div class="layout-column column-pool">
        <h3 class="column-title">可用提示词 (定义池)</h3>
        <AvailablePrompts
            v-model:prompts="preset.prompts"
            :style="{ height: `${availablePromptsColumnContentHeight}px` }"
            @delete="handleDeletePrompt"
            @edit="handleEditPrompt"
        />
      </div>

      <!-- 激活顺序 -->
      <div class="layout-column column-order">
        <h3 class="column-title">激活顺序</h3>
        <div ref="activePromptOrderColumnContentRef" class="column-content">
          <ActivePromptOrder
              v-if="currentOrderSetting"
              v-model:order="currentOrderSetting.order"
              :prompts="preset.prompts"
              @edit="handleEditPrompt"
          />
          <div v-else class="empty-state">
            请选择或创建一个角色配置
          </div>
        </div>
      </div>
    </div>
    <div v-else class="loading-state">
      无效的JSON或正在加载...
    </div>
  </div>
</template>

<script lang="ts" setup>
import {computed, ref, watch} from 'vue';
import {useElementSize, useVModel} from '@vueuse/core';
import {NButton, NSelect, useDialog, useThemeVars} from 'naive-ui';
import type {SillyTavernPreset} from './sillyTavernPreset';
import AvailablePrompts from './AvailablePrompts.vue';
import ActivePromptOrder from './ActivePromptOrder.vue';
import {usePromptEditModal} from './usePromptEditModal';

const props = defineProps<{
  modelValue: string; // 接收 JSON 字符串
}>();

const emit = defineEmits(['update:modelValue']);

// 使用 useVModel 实现双向绑定
const jsonString = useVModel(props, 'modelValue', emit, {
  passive: true,
});

// 内部状态：解析后的 preset 对象
const preset = ref<SillyTavernPreset | null>(null);

// 监听外部 JSON 字符串变化，并安全地解析
watch(() => jsonString.value, (newJson) =>
{
  try
  {
    const parsed = JSON.parse(newJson);
    // TODO: 这里可以加上更严格的 Zod 或 Yup 验证
    if (parsed && typeof parsed === 'object')
    {
      preset.value = parsed;
    }
  } catch (e)
  {
    console.error('Failed to parse preset JSON:', e);
    preset.value = null;
  }
}, {immediate: true});

// 监听内部 preset 对象的变化，并更新外部 JSON 字符串
watch(preset, (newPreset) =>
{
  if (newPreset)
  {
    jsonString.value = JSON.stringify(newPreset, null, 2);
  }
}, {deep: true});

// 当前选中的 character_id
const selectedCharacterId = ref<number | null>(100001);

// 角色ID选项
const characterIdOptions = computed(() =>
    preset.value?.prompt_order.map(po => ({
      label: `角色 ${po.character_id}`,
      value: po.character_id,
    })) ?? []
);

// 当前正在编辑的 PromptOrderSetting
const currentOrderSetting = computed(() =>
    preset.value?.prompt_order.find(po => po.character_id === selectedCharacterId.value)
);

// 首次加载时，默认选中第一个
watch(characterIdOptions, (opts) =>
{
  if (opts.length > 0 && !selectedCharacterId.value)
  {
    selectedCharacterId.value = opts[0].value;
  }
}, {immediate: true});

const dialog = useDialog();
// --- 模态框逻辑 ---
const {open: openEditModal} = usePromptEditModal();

/**
 * 处理添加新提示词的请求
 */
const handleAddNewPrompt = async () =>
{
  if (!preset.value) return;

  // 打开模态框，初始数据为空对象，表示新建
  const newItem = await openEditModal({});

  if (newItem)
  {
    // 检查 identifier 是否已存在，虽然 UUID 碰撞概率极低，但这是好习惯
    if (preset.value.prompts.some(p => p.identifier === newItem.identifier))
    {
      // 可以用 useMessage 显示一个错误提示
      console.error('Identifier collision detected!');
      return;
    }

    // 1. 将新定义添加到 prompts (定义池)
    preset.value.prompts.push(newItem);

    // 2. (可选) 询问用户是否要立即链接到当前激活顺序
    if (currentOrderSetting.value)
    {
      dialog.info({
        title: '链接提示词',
        content: `要将新的提示词 "${newItem.name}" 添加到当前激活顺序的末尾吗？`,
        positiveText: '是的，添加',
        negativeText: '不了，谢谢',
        onPositiveClick: () =>
        {
          currentOrderSetting.value?.order.push({
            identifier: newItem.identifier,
            enabled: true,
          });
        },
      });
    }
  }
};

const handleEditPrompt = async (identifier: string) =>
{
  const itemToEdit = preset.value?.prompts.find(p => p.identifier === identifier);
  if (!itemToEdit || !preset.value) return;

  const updatedItem = await openEditModal(itemToEdit);
  if (updatedItem)
  {
    const index = preset.value.prompts.findIndex(p => p.identifier === identifier);
    if (index !== -1)
    {
      // 如果 identifier 被修改了(不常见但可能)，需要更新所有引用
      if (itemToEdit.identifier !== updatedItem.identifier)
      {
        preset.value.prompt_order.forEach(po =>
        {
          po.order.forEach(orderItem =>
          {
            if (orderItem.identifier === itemToEdit.identifier)
            {
              orderItem.identifier = updatedItem.identifier;
            }
          });
        });
      }
      preset.value.prompts[index] = updatedItem;
    }
  }
};

/**
 * 处理彻底删除提示词的请求，带有确认对话框
 */
const handleDeletePrompt = (identifier: string) =>
{
  if (!preset.value) return;
  const itemToDelete = preset.value.prompts.find(p => p.identifier === identifier);
  if (!itemToDelete) return;

  dialog.warning({
    title: '确认删除',
    content: `你确定要彻底删除提示词 "${itemToDelete.name}" 吗？此操作不可逆，并且会从所有的角色激活顺序中移除它。`,
    positiveText: '确定删除',
    negativeText: '取消',
    onPositiveClick: () =>
    {
      if (!preset.value) return; // TS 类型守卫

      // 1. 从 prompts (定义池) 中删除
      preset.value.prompts = preset.value.prompts.filter(p => p.identifier !== identifier);

      // 2. 从所有 prompt_order 的 order 列表中删除
      preset.value.prompt_order.forEach(promptOrder =>
      {
        promptOrder.order = promptOrder.order.filter(orderItem => orderItem.identifier !== identifier);
      });

      // (可选) 可以用 useMessage 显示一个成功提示
      // message.success(`提示词 "${itemToDelete.name}" 已被删除`);
    },
  });
};

const activePromptOrderColumnContentRef = ref<HTMLElement | null>(null);
const {height: availablePromptsColumnContentHeight} = useElementSize(activePromptOrderColumnContentRef);

const themeVars = useThemeVars()
</script>

<style scoped>
.editor-layout {
  display: flex;
  align-items: flex-start;
  gap: 16px;
}

.layout-column {
  flex-basis: 50%;
  min-width: 0;
}
</style>