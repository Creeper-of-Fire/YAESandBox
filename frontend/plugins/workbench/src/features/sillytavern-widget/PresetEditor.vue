<!-- PresetEditor.vue -->
<template>
  <div class="preset-editor">
    <div class="toolbar">
      <n-select
          v-if="preset"
          v-model:value="selectedCharacterId"
          :options="characterIdOptions"
          placeholder="选择角色配置"
          style="width: 200px;"
      />
      <div class="toolbar-actions">
        <!-- 添加新提示词 -->
        <n-button @click="handleAddNewPrompt">
          <template #icon>
            <n-icon :component="AddIcon"/>
          </template>
          添加新提示词
        </n-button>
        <!-- 导入/导出按钮组 -->
        <n-button-group>
          <n-button @click="handleImportClick">
            <template #icon>
              <n-icon :component="UploadIcon"/>
            </template>
            导入
          </n-button>
          <n-button @click="handleExport">
            <template #icon>
              <n-icon :component="DownloadIcon"/>
            </template>
            导出
          </n-button>
        </n-button-group>
      </div>
      <!-- 隐藏的文件输入框 -->
      <input
          ref="fileInputRef"
          accept=".json"
          style="display: none"
          type="file"
          @change="handleFileSelected"
      />
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
            <n-empty description="请选择或创建一个角色配置">
              <template #extra>
                <n-button size="small" @click="handleAddNewCharacter">
                  创建新角色配置
                </n-button>
              </template>
            </n-empty>
          </div>
        </div>
      </div>
    </div>
    <div v-else class="loading-state">
      <n-empty description="未加载任何预设或文件损坏">
        <template #extra>
          <div class="empty-state-actions">
            <n-text depth="3">
              你可以从文件导入或创建一个新的预设。
            </n-text>
            <n-button type="primary" @click="handleInitializePreset">
              <template #icon>
                <n-icon :component="AddIcon"/>
              </template>
              创建新预设
            </n-button>
          </div>
        </template>
      </n-empty>
    </div>
  </div>
</template>

<script lang="ts" setup>
import {computed, ref, watch} from 'vue';
import {AddIcon as AddIcon, CloudDownloadIcon as DownloadIcon, CloudUploadIcon as UploadIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {useElementSize, useVModel} from '@vueuse/core';
import {NButton, NSelect, useDialog, useMessage, useThemeVars} from 'naive-ui';
import {createEmptyPreset, type PromptItem, type PromptOrderSetting, type SillyTavernPreset} from './sillyTavernPreset';
import AvailablePrompts from './AvailablePrompts.vue';
import ActivePromptOrder from './ActivePromptOrder.vue';
import {usePromptEditModal} from './usePromptEditModal';

const props = defineProps<{
  modelValue: string; // 接收 JSON 字符串
}>();

const emit = defineEmits(['update:modelValue']);

const message = useMessage();
const dialog = useDialog();

// 使用 useVModel 实现双向绑定
const jsonString = useVModel(props, 'modelValue', emit, {passive: true,});

// `rawPreset` 是我们的“单一事实来源”，它保留了所有原始字段
const rawPreset = ref<Record<string, any> | null>(null);
// `preset` 是一个类型安全的计算属性，作为对 `rawPreset` 的代理/视图
const preset = computed(() => rawPreset.value as SillyTavernPreset | null);


// 监听外部 JSON 字符串变化，更新我们的“单一事实来源”
watch(() => jsonString.value, (newJson) =>
{
  if (!newJson || newJson.trim() === '')
  {
    rawPreset.value = null;
    return;
  }
  try
  {
    const parsed = JSON.parse(newJson);
    if (parsed && typeof parsed === 'object')
    {
      rawPreset.value = parsed; // 直接赋值，保留所有字段
    }
  } catch (e)
  {
    console.error('Failed to parse preset JSON:', e);
    message.error('解析预设JSON失败，请检查格式。');
    rawPreset.value = null;
  }
}, {immediate: true});

// 深度监听内部 `rawPreset` 对象的变化，并更新外部 JSON 字符串
// 这样任何对 preset.value.prompts.push() 等操作都会被监听到
watch(rawPreset, (newPreset) =>
{
  if (newPreset)
  {
    // 序列化时，所有未被我们修改的未知字段都会被保留
    jsonString.value = JSON.stringify(newPreset, null, 2);
  }
}, {deep: true});

// --- 导入 / 导出逻辑 ---
const fileInputRef = ref<HTMLInputElement | null>(null);

const handleImportClick = () =>
{
  fileInputRef.value?.click();
};

const handleFileSelected = (event: Event) =>
{
  const target = event.target as HTMLInputElement;
  const file = target.files?.[0];
  if (!file) return;

  const reader = new FileReader();
  reader.onload = (e) =>
  {
    const content = e.target?.result as string;
    // 直接更新 v-model，触发上面的 watch 逻辑来解析和加载
    jsonString.value = content;
    message.success(`成功导入预设文件: ${file.name}`);
  };
  reader.onerror = () =>
  {
    message.error('读取文件失败');
  };
  reader.readAsText(file);

  // 清空 input 的 value，这样下次选择同一个文件还能触发 change 事件
  target.value = '';
};

const handleExport = () =>
{
  if (!jsonString.value)
  {
    message.warning('没有可导出的内容。');
    return;
  }
  const blob = new Blob([jsonString.value], {type: 'application/json;charset=utf-8'});
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `sillytavern_preset_${Date.now()}.json`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
  message.success('预设已导出。');
};


// --- 角色和提示词管理逻辑 ---
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

watch(characterIdOptions, (opts) =>
{
  // 仅在当前没有选中项，或者选中项不再有效时，才自动选择第一个
  const currentSelectionIsValid = opts.some(o => o.value === selectedCharacterId.value);
  if (opts.length > 0 && !currentSelectionIsValid)
  {
    selectedCharacterId.value = opts[0].value;
  }
  else if (opts.length === 0)
  {
    selectedCharacterId.value = null;
  }
}, {immediate: true, deep: true});


// --- 模态框逻辑 ---
const {open: openEditModal} = usePromptEditModal();

/**
 * 处理添加新提示词的请求
 */
const handleAddNewPrompt = async () =>
{
  if (!preset.value) return;

  // 打开模态框，初始数据为空对象，表示新建
  const newItem: PromptItem | undefined = await openEditModal({});

  if (newItem)
  {
    // 检查 identifier 是否已存在，虽然 UUID 碰撞概率极低，但这是好习惯
    if (preset.value.prompts.some(p => p.identifier === newItem.identifier))
    {
      // 可以用 useMessage 显示一个错误提示
      console.error('发现UUID碰撞！');
      return;
    }

    // 1. 将新定义添加到 prompts (定义池)
    preset.value.prompts.push(newItem);

    // 2. 询问用户是否要立即链接到当前激活顺序
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

  const updatedItemData = await openEditModal(itemToEdit);
  if (updatedItemData)
  {
    const index = preset.value.prompts.findIndex(p => p.identifier === identifier);
    if (index !== -1)
    {
      // 如果 identifier 被修改了(不常见但可能)，需要更新所有引用
      if (itemToEdit.identifier !== updatedItemData.identifier)
      {
        preset.value.prompt_order.forEach(po =>
        {
          po.order.forEach(orderItem =>
          {
            if (orderItem.identifier === itemToEdit.identifier)
            {
              orderItem.identifier = updatedItemData.identifier;
            }
          });
        });
      }
      Object.assign(itemToEdit, updatedItemData);
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

// 创建新角色配置的功能
const handleAddNewCharacter = () =>
{
  if (!rawPreset.value || !preset.value) return;

  // 简单的 ID 生成策略，实际中可能需要更复杂的逻辑
  const existingIds = preset.value.prompt_order.map(p => p.character_id);
  const newId = existingIds.length > 0 ? Math.max(...existingIds) + 1 : 100001;

  const newCharacterSetting: PromptOrderSetting = {
    character_id: newId,
    order: [],
  };

  preset.value.prompt_order.push(newCharacterSetting);
  // 创建后自动选中
  selectedCharacterId.value = newId;
  message.success(`已创建新的角色配置 (ID: ${newId})`);
};

const handleInitializePreset = () =>
{
  rawPreset.value = createEmptyPreset();
  message.success('已创建新的空白预设，现在可以开始添加内容了。');
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

.empty-state-actions {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
  margin-top: 12px;
}

.loading-state {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 400px;
}

.empty-state {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 100%;
  min-height: 400px;
}
</style>