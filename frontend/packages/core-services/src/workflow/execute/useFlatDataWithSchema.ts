import { computed, ref, watch, type Ref } from 'vue';
import type { EntityFieldSchema } from './entitySchema.ts';
import { getKey } from './entitySchema.ts';

type FlatData = Record<string, string>;
type TypedData = Record<string, string | number | null>;
type Errors = Record<string, string | null>;

/**
 * 一个可复用的 Composable，用于将一个扁平的、纯字符串的 `flatData` 对象，
 * 根据提供的 schema，转换为一个类型化的、可验证的数据实体。
 *
 * @param flatDataRef - 对源 flatData 对象的响应式引用。
 * @param schema - 定义实体结构的 EntityFieldSchema 数组。
 */
export function useFlatDataWithSchema(
    flatDataRef: Ref<FlatData>,
    schema: EntityFieldSchema[]
) {
    // --- State ---
    const typedData = ref<TypedData>({});
    const errors = ref<Errors>({});
    const isValid = ref(true);

    // --- Core Logic ---

    /**
     * 根据 schema 和最新的 flatData 更新 typedData 和验证状态。
     */
    function processData() {
        const newTypedData: TypedData = {};
        const newErrors: Errors = {};
        let allValid = true;

        for (const field of schema) {
            const key = getKey(field);
            const stringValue = flatDataRef.value[key] ?? ''; // 从源数据获取字符串值

            let convertedValue: string | number | null = stringValue;

            // 1. 类型转换
            if (field.dataType === 'number') {
                // 直接在这里处理字符串，而不是调用 parseNumber
                if (stringValue === '') {
                    convertedValue = null;
                } else {
                    const parsed = parseInt(stringValue, 10);
                    // 如果解析失败 (e.g., stringValue was "abc")，结果为 null
                    convertedValue = isNaN(parsed) ? null : parsed;
                }
            }
            newTypedData[key] = convertedValue;

            // 2. 验证 (简易实现，可扩展)
            // TODO: 集成更复杂的 Naive UI 规则验证
            if (field.rules) {
                const rules = Array.isArray(field.rules) ? field.rules : [field.rules];
                for (const rule of rules) {
                    if (rule.required && (convertedValue === null || convertedValue === '')) {
                        newErrors[key] = rule.message?.toString() || `${field.label} is required.`;
                        allValid = false;
                        break; // 一个字段的第一个错误就足够了
                    }
                }
            }
        }

        typedData.value = newTypedData;
        errors.value = newErrors;
        isValid.value = allValid;
    }

    // --- Watchers ---

    // 当源 flatData 发生变化时（例如，AI流式输出更新），自动重新处理数据
    watch(flatDataRef, processData, { deep: true, immediate: true });

    return {
        /**
         * 响应式的、根据 schema 转换了类型的扁平数据对象。
         * Key 是 schema path 的组合 (e.g., 'content__description')。
         */
        typedData,
        /**
         * 响应式的、包含验证错误信息的对象。
         * Key 对应 typedData 的 key。
         */
        errors,
        /**
         * 一个计算属性，表示当前数据是否通过了所有验证规则。
         */
        isValid: computed(() => isValid.value),
    };
}