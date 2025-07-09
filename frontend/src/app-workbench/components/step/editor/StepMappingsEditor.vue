<!-- src/app-workbench/components/editor/StepMappingsEditor.vue -->
<template>
  <div class="step-mappings-editor">
    <!-- 输入映射 -->
    <n-card :bordered="true" size="small" title="输入映射">
      <template #header-extra>
        <n-button dashed size="small" @click="addInputMappingRow">
          <template #icon>
            <n-icon :component="AddIcon"/>
          </template>
          手动添加映射
        </n-button>
      </template>

      <!-- 1. 缺少必要输入的警告 -->
      <n-alert v-if="missingInputs.length > 0" :show-icon="true" title="缺少必要的输入映射" type="error">
        <p style="margin-top: 4px;">此步骤中的模块需要以下输入，但尚未配置映射来源：</p>
        <n-tag v-for="input in missingInputs" :key="input" style="margin-right: 8px; margin-top: 4px;" type="error">
          {{ input }}
        </n-tag>
      </n-alert>

      <!-- 2. 输入映射表格 -->
      <n-data-table
          :bordered="false"
          :columns="inputColumns"
          :data="editableInputMappings"
          :pagination="false"
          :single-line="false"
          class="mappings-table"
          size="small"
      />
    </n-card>

    <!-- 输出映射 -->
    <n-card :bordered="true" size="small" style="margin-top: 16px;" title="输出映射">
      <template #header-extra>
        <n-button dashed size="small" @click="addOutputMappingRow">
          <template #icon>
            <n-icon :component="AddIcon"/>
          </template>
          添加输出映射
        </n-button>
      </template>

      <!-- 输出映射表格 -->
      <n-data-table
          :bordered="false"
          :columns="outputColumns"
          :data="editableOutputMappings"
          :pagination="false"
          :single-line="false"
          class="mappings-table"
          size="small"
      >
        <template #empty>
          <n-empty description="当前没有配置输出映射"/>
        </template>
      </n-data-table>
    </n-card>
  </div>
</template>

<script lang="ts" setup>
import {computed, h, onMounted, ref, watch} from 'vue';
import type {DataTableColumns} from 'naive-ui';
import {NAlert, NAutoComplete, NButton, NCard, NDataTable, NEmpty, NIcon, NInput, NPopconfirm, NTag, NTooltip} from 'naive-ui';
import {AddIcon, AlertCircleIcon, ArrowForwardIcon, DeleteIcon, HelpCircleIcon} from '@/utils/icons';
import {useDebounceFn} from '@vueuse/core';

// --- 类型定义 ---
/**
 * 用于在表格中编辑的映射行对象
 */
interface EditableMappingRow
{
  line_id: number; // 唯一键，用于 v-for
  globalVar: string; // 全局变量（来源）
  localVar: string; // 模块内部变量（目标）
  isOrphaned?: boolean; // 是否是孤立的（即 localVar 不在 requiredInputs 中）
}

// --- 组件 Props 和 Emits ---
const props = defineProps<{
  // 使用 v-model 双向绑定映射数据
  inputMappings: Record<string, string>;
  outputMappings: Record<string, string>;
  // 从父组件计算好的上下文信息
  requiredInputs: string[];      // 所有模块需要的输入变量名集合
  availableGlobalVars?: string[]; // 此步骤可用的全局变量名集合，用于自动完成提示
}>();

const emit = defineEmits(['update:inputMappings', 'update:outputMappings']);

// --- 内部状态 ---
// 用于表格展示和编辑的输入映射数组
const editableInputMappings = ref<EditableMappingRow[]>([]);
// 用于表格展示和编辑的输出映射数组
const editableOutputMappings = ref<EditableMappingRow[]>([]);
let nextId = 0; // 用于生成唯一的行 ID

// --- 计算属性 ---

// 格式化可用全局变量，用于 n-auto-complete 组件
const availableGlobalVarsOptions = computed(() =>
    props.availableGlobalVars?.map(v => ({label: v, value: v})) ?? []
);

// 计算哪些必需的输入还没有被映射
const missingInputs = computed(() =>
{
  const mappedLocals = new Set(Object.keys(props.inputMappings).filter(key => props.inputMappings[key]?.trim()));
  return props.requiredInputs.filter(req => !mappedLocals.has(req));
});


// --- 数据同步逻辑 ---

const initializeMappings = () =>
{
  const newEditableInputs: EditableMappingRow[] = [];
  const processedLocals = new Set<string>();

  // 1. 遍历现有的 inputMappings ({ [localVar]: globalVar })
  for (const [localVar, globalVar] of Object.entries(props.inputMappings))
  {
    newEditableInputs.push({
      line_id: nextId++,
      globalVar: globalVar,
      localVar: localVar,
      // 如果一个映射的 localVar 不再是必需的，则为孤立
      isOrphaned: !props.requiredInputs.includes(localVar),
    });
    processedLocals.add(localVar);
  }

  // 2. 遍历所有必需输入，为那些尚未处理的（即不在 inputMappings 中的）创建新行
  props.requiredInputs.forEach(req =>
  {
    if (!processedLocals.has(req))
    {
      newEditableInputs.push({
        line_id: nextId++,
        globalVar: '', // 等待用户填写
        localVar: req,
        isOrphaned: false
      });
    }
  });

  editableInputMappings.value = newEditableInputs;

  // 输出映射初始化 (结构和输入映射的新结构一致)
  editableOutputMappings.value = Object.entries(props.outputMappings).map(([globalVar, localVar]) => ({
    line_id: nextId++,
    globalVar: globalVar,
    localVar: localVar,
  }));
};
/**
 * 防抖函数：当内部表格数据变化时，将其转换回 Record<string, string> 并 emit 出去
 * 使用防抖可以避免在用户快速输入时频繁触发父组件更新
 */
const syncMappingsToParent = useDebounceFn(() =>
{
  // 1. 更新输入映射: { [localVar]: globalVar }
  const newInputMappings: Record<string, string> = {};
  editableInputMappings.value.forEach(row =>
  {
    const globalVar = row.globalVar.trim();
    const localVar = row.localVar.trim();
    // 只有当 localVar 和 globalVar 都有效时，才构成一个映射
    if (localVar && globalVar)
    {
      newInputMappings[localVar] = globalVar;
    }
  });
  emit('update:inputMappings', newInputMappings);

  // 2. 更新输出映射: { [globalVar]: localVar }
  const newOutputMappings: Record<string, string> = {};
  editableOutputMappings.value.forEach(row =>
  {
    const globalVar = row.globalVar.trim();
    const localVar = row.localVar.trim();
    if (globalVar && localVar)
    {
      // globalVar 作为键，也应该是唯一的
      newOutputMappings[globalVar] = localVar;
    }
  });
  emit('update:outputMappings', newOutputMappings);
}, 300);


// --- 生命周期和侦听器 ---

onMounted(() =>
{
  initializeMappings();
});


watch(() => props.requiredInputs, (newRequired) =>
{
  const currentLocals = new Set(editableInputMappings.value.map(m => m.localVar));

  // 1. 为新增的必需输入添加新行
  newRequired.forEach(req =>
  {
    if (!currentLocals.has(req))
    {
      editableInputMappings.value.push({
        line_id: nextId++, globalVar: '', localVar: req,
        isOrphaned: false
      });
    }
  });

  // 2. 重新评估每一行的状态
  editableInputMappings.value.forEach(row =>
  {
    const isNowRequired = newRequired.includes(row.localVar);
    row.isOrphaned = !isNowRequired;
  });
}, {deep: true});

watch(
    () => props.inputMappings,
    () => {
      editableInputMappings.value.forEach(row =>
      {
        const isNowRequired = props.requiredInputs.includes(row.localVar);
        row.isOrphaned = !isNowRequired;
      });
    },
    {deep: true}
);

watch(
    [editableInputMappings, editableOutputMappings],
    () =>
    {
      syncMappingsToParent();
    },
    {deep: true}
);


// --- 表格列定义和事件处理 ---


/**
 * 计算重复的输入映射本地变量 (用于UI警告)
 * @returns 一个包含所有重复键的 Set
 */
const duplicateInputLocals = computed(() =>
{
  const seen = new Set<string>();
  const duplicates = new Set<string>();
  editableInputMappings.value.forEach(row =>
  {
    const key = row.localVar.trim();
    if (!key) return; // 忽略空值
    if (seen.has(key))
    {
      duplicates.add(key);
    }
    else
    {
      seen.add(key);
    }
  });
  return duplicates;
});

/**
 * 计算重复的输出映射全局变量 (用于UI警告)
 * @returns 一个包含所有重复键的 Set
 */
const duplicateOutputGlobals = computed(() =>
{
  const seen = new Set<string>();
  const duplicates = new Set<string>();
  editableOutputMappings.value.forEach(row =>
  {
    const key = row.globalVar.trim();
    if (!key) return; // 忽略空值
    if (seen.has(key))
    {
      duplicates.add(key);
    }
    else
    {
      seen.add(key);
    }
  });
  return duplicates;
});

// --- 表格列定义 ---

/**
 * 辅助函数，用于创建带状态图标的单元格内容
 * @param inputNode - 已创建的输入框 h() 对象
 * @param isError - 是否是错误状态
 * @param errorMsg - 错误提示
 * @param isWarning - 是否是警告状态
 * @param warningMsg - 警告提示
 * @returns 最终的 h() 对象
 */
const renderCellWithStatus = (
    inputNode: ReturnType<typeof h>,
    isError: boolean,
    errorMsg: string,
    isWarning: boolean,
    warningMsg: string
) =>
{
  let statusIcon = null;

  if (isError)
  {
    statusIcon = h(NTooltip, null, {
      trigger: () => h(NIcon, {component: AlertCircleIcon, color: '#d03050', style: {marginLeft: '8px'}}),
      default: () => errorMsg
    });
  }
  else if (isWarning)
  {
    statusIcon = h(NTooltip, null, {
      trigger: () => h(NIcon, {component: HelpCircleIcon, color: '#f0a020', style: {marginLeft: '8px'}}),
      default: () => warningMsg
    });
  }

  if (statusIcon)
  {
    return h('div', {style: {display: 'flex', alignItems: 'center'}}, [inputNode, statusIcon]);
  }
  return inputNode;
};


// 创建输入映射表格的列

const inputColumns: DataTableColumns<EditableMappingRow> = [
  {
    title: '模块变量 (目标)',
    key: 'localVar',
    render(row, index)
    {
      const key = row.localVar.trim();
      const isDuplicate = key !== '' && duplicateInputLocals.value.has(key);
      const isOrphaned = !!row.isOrphaned; // 确保是 boolean

      const status = isDuplicate ? 'error' : (isOrphaned ? 'warning' : undefined);

      const inputNode = h(NInput, {
        value: row.localVar, placeholder: '输入模块内部变量名',
        readonly: false, status: status,
        onUpdateValue: (v) => editableInputMappings.value[index].localVar = v
      });

      return renderCellWithStatus(
          inputNode,
          isDuplicate, '模块变量名重复，这将导致映射被覆盖。',
          isOrphaned, '这个映射的目标变量已不是当前模块的必需输入。'
      );
    }
  },
  {
    title: '', key: 'arrow', width: 40,
    render: () => h(NIcon, {
      class: 'arrow-icon',
      style: {transform: 'rotate(180deg)'}
    }, {default: () => h(ArrowForwardIcon)})
  },
  {
    title: '全局变量 (来源)', key: 'globalVar',
    render(row, index)
    {
      return h(NAutoComplete, {
        value: row.globalVar, options: availableGlobalVarsOptions.value, placeholder: '输入或选择全局变量',
        onUpdateValue: (v) => editableInputMappings.value[index].globalVar = v,
      });
    }
  },
  {
    title: '操作', key: 'actions', width: 60, align: 'center',
    render: (_, index) => h(NPopconfirm, {onPositiveClick: () => editableInputMappings.value.splice(index, 1)}, {
      trigger: () => h(NButton, {circle: true, type: 'error', size: 'small', ghost: true}, {icon: () => h(NIcon, {component: DeleteIcon})}),
      default: () => '确定要删除这个映射吗？'
    })
  }
];

const outputColumns: DataTableColumns<EditableMappingRow> = [
  {
    title: '模块变量 (来源)', key: 'localVar',
    render(row, index)
    {
      return h(NInput, {
        value: row.localVar, placeholder: '模块内要输出的变量',
        onUpdateValue: (v) => editableOutputMappings.value[index].localVar = v,
      });
    },
  },
  {
    title: '', key: 'arrow', width: 40,
    render: () => h(NIcon, {class: 'arrow-icon'}, {default: () => h(ArrowForwardIcon)})
  },
  {
    title: '全局变量 (目标)',
    key: 'globalVar',
    render(row, index)
    {
      const key = row.globalVar.trim();
      const isDuplicate = key !== '' && duplicateOutputGlobals.value.has(key);

      const inputNode = h(NInput, {
        value: row.globalVar, placeholder: '映射到的全局变量名',
        status: isDuplicate ? 'error' : undefined,
        onUpdateValue: (v) => editableOutputMappings.value[index].globalVar = v,
      });

      return renderCellWithStatus(
          inputNode,
          isDuplicate, '全局变量名重复，这将导致映射被覆盖。',
          false, '' // 输出映射没有孤立状态
      );
    },
  },
  {
    title: '操作', key: 'actions', width: 60, align: 'center',
    render: (_, index) => h(NPopconfirm, {onPositiveClick: () => editableOutputMappings.value.splice(index, 1)}, {
      trigger: () => h(NButton, {circle: true, type: 'error', size: 'small', ghost: true}, {icon: () => h(NIcon, {component: DeleteIcon})}),
      default: () => '确定要删除这个映射吗？'
    })
  }
];


// --- 添加/删除行逻辑 ---
const addInputMappingRow = () =>
{
  editableInputMappings.value.push({
    line_id: nextId++,
    globalVar: '',
    localVar: '',
    isOrphaned: true
  });
};

const addOutputMappingRow = () =>
{
  editableOutputMappings.value.push({
    line_id: nextId++,
    globalVar: '',
    localVar: '',
  });
};


</script>

<style scoped>
.step-mappings-editor {
  margin-bottom: 12px;
  background-color: #fcfcfd;
  padding: 12px;
  border-radius: 4px;
  border: 1px solid #f0f0f0;
}

/* 调整表格内组件的样式，使其更紧凑 */
.mappings-table {
  margin-top: 12px;
}

/* 全局覆盖，让表格内的输入框没有边框，看起来更像是直接在单元格里编辑 */
:deep(.n-data-table-td .n-input),
:deep(.n-data-table-td .n-auto-complete) {
  background-color: transparent;
}

:deep(.n-data-table-td .n-input .n-input__border),
:deep(.n-data-table-td .n-input .n-input__state-border) {
  border: none !important;
  border-bottom: 1px solid #efefef;
  border-radius: 0;
}

:deep(.n-data-table-td .n-input:hover .n-input__state-border) {
  border-bottom: 1px solid #2080f0;
}

.arrow-icon {
  font-size: 16px;
  color: #aaa;
  display: block;
  margin: auto;
}

</style>