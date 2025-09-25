<template>
  <n-card :bordered="false" style="background-color: #fffbe6; border: 1px solid #fde089;">
    <n-spin :show="isFixing">
      <n-flex vertical>
        <!-- 错误提示 -->
        <n-alert title="符文解析失败" type="error">
          <p>系统无法识别或解析此符文的配置。请检查下面的错误信息和原始数据，尝试修复它。</p>
          <strong>错误详情:</strong> {{ props.modelValue.errorMessage }}
        </n-alert>

        <n-form label-placement="left" label-width="auto">
          <!-- 原始类型信息 -->
          <n-form-item label="原始类型">
            <n-input :value="props.modelValue.originalRuneType" placeholder="未指定原始类型" readonly/>
          </n-form-item>

          <!-- 原始JSON数据编辑器 -->
          <n-form-item label="原始JSON数据">
            <n-flex style="width: 100%" vertical>
              <n-input
                  v-model:value="jsonText"
                  :autosize="{ minRows: 6, maxRows: 20 }"
                  :status="jsonError ? 'error' : 'success'"
                  placeholder="在此编辑原始JSON数据..."
                  type="textarea"
              />
              <!-- JSON 格式校验提示 -->
              <n-text v-if="jsonError" style="font-size: 12px; margin-top: 4px;" type="error">
                JSON格式错误: {{ jsonError }}
              </n-text>
              <n-text v-else style="font-size: 12px; margin-top: 4px;" type="success">
                JSON格式正确
              </n-text>
            </n-flex>
          </n-form-item>
        </n-form>

        <n-flex justify="end">
          <n-button :loading="isFixing" type="primary" @click="handleAttemptFix">
            <template #icon>
              <n-icon :component="SparklesIcon"/>
            </template>
            尝试修复并转换类型
          </n-button>
        </n-flex>
        <div style="font-size: 12px; color: #888; margin-top: 8px;">
          <strong>提示:</strong> 修复 `RuneType` 和其他字段后，点击上方按钮。如果成功，此编辑器将自动变为对应类型的正确编辑器，无需刷新页面。
        </div>
      </n-flex>
      <template #description>
        正在从服务器获取新配置并转换...
      </template>
    </n-spin>
  </n-card>
</template>

<script lang="ts" setup>
import {ref, watch} from 'vue';
import {NAlert, NCard, NFlex, NForm, NFormItem, NInput, NText, useMessage} from 'naive-ui';
import {useVModel} from "@vueuse/core";
import {SparklesIcon} from "@yaesandbox-frontend/shared-ui/icons";
import {createBlankConfig} from "#/utils/createBlankConfig.ts";
import type {AbstractRuneConfig} from "#/types/generated/workflow-config-api-client";

// 定义组件的 props，modelValue 是从表单生成器接收的数据对象
const props = defineProps<{
  modelValue: {
    // 对应 UnknownRuneConfig 的结构
    originalRuneType: string;
    errorMessage: string;
    rawJsonData: Record<string, any>;
    // 其他可能从 AbstractRuneConfig 继承的属性
    [key: string]: any;
  };
}>();

// 定义组件的 emits，用于更新 modelValue
const emit = defineEmits(['update:modelValue']);

const formModel = useVModel(props, 'modelValue', emit, {
  passive: true,
  deep: true,
});
const jsonText = ref('');
const jsonError = ref<string | null>(null);


watch(
    () => formModel.value?.rawJsonData,
    (newRawJson) =>
    {
      // 检查 model 是否存在
      if (!newRawJson)
      {
        jsonText.value = '{}'; // 提供一个安全的默认值
        return;
      }
      const newJsonString = JSON.stringify(newRawJson, null, 2);
      // 关键：只有当外部数据真的和 textarea 内容不同时才更新，防止无限循环
      if (newJsonString !== jsonText.value)
      {
        jsonText.value = newJsonString;
        jsonError.value = null; // 外部同步时，假定数据是正确的，清除错误
      }
    },
    {immediate: true, deep: true} // immediate: 确保组件加载时立即执行一次
);


// 5. 同步逻辑：从本地 textarea 到父组件 (Internal -> External)
//    当用户在 textarea 中输入时，验证并更新 `formModel`
watch(jsonText, (newText) =>
{
  if (newText.trim() === '')
  {
    jsonError.value = 'JSON 内容不能为空。';
    return; // 不更新无效的空内容
  }

  try
  {
    const parsedJson = JSON.parse(newText);
    jsonError.value = null; // 格式正确，清除错误

    // 关键：直接修改 useVModel 返回的代理 ref。
    // useVModel 内部会处理 emit('update:modelValue') 的逻辑。
    if (formModel.value)
    {
      formModel.value.rawJsonData = parsedJson;
    }
  } catch (e: any)
  {
    // 如果解析失败，仅更新错误提示，不更新父组件的数据
    jsonError.value = e.message;
  }
});

const message = useMessage();
const isFixing = ref(false);

/**
 * 核心功能：尝试修复并“变形”当前对象
 */
async function handleAttemptFix()
{
  // 1. 验证 JSON 格式
  if (jsonError.value)
  {
    message.error(`修复失败：JSON 格式不正确。请先修正错误。`);
    return;
  }

  isFixing.value = true;
  try
  {
    const userJson = JSON.parse(jsonText.value);
    const newRuneType = userJson.runeType;

    // 2. 检查新的 RuneType 是否存在
    if (typeof newRuneType !== 'string' || !newRuneType)
    {
      throw new Error("修复失败：JSON数据中缺少有效的 'runeType' 字符串属性。");
    }

    // 3. 从后端获取对应类型的空白配置（“干净模板”）
    const blankConfig = await createBlankConfig('rune', userJson.name || props.modelValue.name, {runeType: newRuneType});

    // 4. 智能合并：以干净模板为基础，覆盖用户修改，并强制保留原始ID
    const finalConfig = {
      ...blankConfig,
      ...userJson,
      configId: props.modelValue.configId, // 关键：保持ID不变！
    } as AbstractRuneConfig;


    // 5. ✨ 偷梁换柱：就地改造 formModel 对象 ✨
    const target = formModel.value;
    if (!target) return;

    // a. 删除旧 UnknownRuneConfig 的特有属性
    const oldKeys = Object.keys(target);
    const newKeys = new Set(Object.keys(finalConfig));
    oldKeys.forEach(key =>
    {
      if (!newKeys.has(key))
      {
        delete target[key];
      }
    });

    // b. 复制新配置的所有属性到目标对象上
    Object.assign(target, finalConfig);

    // Vue 3 的 Proxy 会自动侦测到这些变化，并通知父组件重新渲染。
    // 父组件的渲染逻辑（可能是 :is="getComponentForRune(rune)"）会因为 rune.runeType 的变化
    // 而选择渲染新的、正确的编辑器组件。

    message.success(`符文类型已成功转换为 '${newRuneType}'！`);

  } catch (error: any)
  {
    console.error("修复符文失败:", error);
    message.error(`修复失败: ${error}`);
  } finally
  {
    isFixing.value = false;
  }
}

</script>