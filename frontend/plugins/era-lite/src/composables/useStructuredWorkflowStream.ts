import {computed} from 'vue';
import {useWorkflowStream} from '@yaesandbox-frontend/core-services/composables';
import {extractAllThoughts, getText, parseNumber} from '#/utils/workflowParser';
import type {ComplexPropertyValue} from '#/types/streaming';
import {type EntityFieldSchema, getKey} from "#/types/entitySchema.ts";

// --- 辅助函数 ---

/**
 * 根据路径从对象中安全地获取一个 ComplexPropertyValue。
 * @param obj - 源对象
 * @param path - 路径数组
 * @returns 找到的 ComplexPropertyValue，或 undefined
 */
function getValueByPath(obj: any, path: string[]): ComplexPropertyValue | undefined
{
    if (!obj) return undefined;
    if (path.length === 0) return obj; // 空路径意味着根对象
    return path.reduce((current, key) => (current && current[key] !== undefined) ? current[key] : undefined, obj) as ComplexPropertyValue | undefined;
}

/**
 * 递归地将 ComplexPropertyValue 转换为类 XML 格式的字符串，以提高可读性。
 * @param data - 要转换的 ComplexPropertyValue
 * @param indent - 当前的缩进级别
 * @returns 格式化后的类 XML 字符串
 */
function toXmlLikeString(data: ComplexPropertyValue | undefined, indent = 0): string
{
    // 使用更健壮的类型检查
    if (typeof data !== 'object' || data === null) return '';

    const anyData = data as any;
    const space = '  ';
    const indentStr = space.repeat(indent);
    const parts: string[] = []; // 使用一个数组按顺序收集所有渲染部分

    // 使用 Object.keys() 进行一次遍历，以保证属性的原始顺序
    for (const key of Object.keys(anyData))
    {
        const value = anyData[key];

        if (key === '_text') {
            // 在其原始位置处理文本节点
            if (typeof value === 'string' && value.trim()) {
                parts.push(`${indentStr}${space}${value.trim()}`);
            }
        } else if (value && typeof value === 'object') {
            // 处理元素节点
            const childContent = toXmlLikeString(value, indent + 1);

            // 仅当子节点有内容，或是显式的空对象时（例如 <empty/>），才渲染标签
            if (childContent || (Object.keys(value).length === 0)) {
                const formattedChild =
                    `${indentStr}${space}<${key}>\n` +
                    `${childContent}` +
                    // 确保子内容块后总有一个换行符，以保证格式一致
                    `${childContent.endsWith('\n') ? '' : '\n'}` +
                    `${indentStr}${space}</${key}>`;
                parts.push(formattedChild);
            }
        }
    }

    // 将所有按正确顺序收集的部分用换行符连接起来
    return parts.join('\n');
}

// --- Composable 核心 ---

/**
 * Composable 的配置选项。
 */
interface StructuredStreamOptions {
    /** 如果提供，将根据此 Schema 生成一个扁平化的数据对象 (`flatData`)。*/
    schema?: EntityFieldSchema[];
    /** 如果提供，将把此路径下的数据转换为一个类 XML 字符串 (`xmlLikeString`)。空数组[]表示根路径。*/
    xmlToStringPath?: string[];
}

/**
 * 一个灵活的、更高阶的 Composable，用于处理流式工作流，并按需将其转换为对前端友好的数据结构。
 *
 * @param options - 配置对象，用于指定需要哪些派生数据。
 * - `schema`: 用于生成 `flatData`。
 * - `xmlToStringPath`: 用于生成 `xmlLikeString`。
 * 如果不提供任何选项，它将主要作为 `useWorkflowStream` 的一个薄封装。
 */
export function useStructuredWorkflowStream(options: StructuredStreamOptions = {})
{
    const { schema, xmlToStringPath } = options;

    // 1. 核心：底层的流式数据源
    const {
        data: rawStreamData,
        isLoading,
        error,
        isFinished,
        execute,
    } = useWorkflowStream<ComplexPropertyValue>();

    // 2. 按需计算：扁平化的数据模型 (仅在提供了 schema 时)
    const flatData = schema
        ? computed<Record<string, string | number | null>>(() => {
            if (!rawStreamData.value) return {};
            return schema.reduce((acc, field) => {
                const key = getKey(field);
                const complexValue = getValueByPath(rawStreamData.value, field.path);
                acc[key] = field.dataType === 'number' ? parseNumber(complexValue) : getText(complexValue);
                return acc;
            }, {} as Record<string, string | number | null>);
        })
        : undefined;

    // 3. 按需计算：类 XML 字符串 (仅在提供了 xmlToStringPath 时)
    const xmlLikeString = xmlToStringPath
        ? computed<string>(() => {
            if (!rawStreamData.value) return '';
            const targetNode = getValueByPath(rawStreamData.value, xmlToStringPath);
            return toXmlLikeString(targetNode);
        })
        : undefined;


    // 4. 附加的辅助计算属性 (总是提供，因为它开销很小且通用)
    const thinkingProcess = computed<string>(() => {
        if (!rawStreamData.value) return '';
        return extractAllThoughts(rawStreamData.value);
    });

    // 5. 控制函数
    function clear() {
        rawStreamData.value = null;
    }

    // 6. 返回一个动态构建的对象
    // 使用这种方式，TypeScript 可以推断出可选的返回值
    return {
        // --- 基础状态和控制 ---
        isLoading,
        error,
        isFinished,
        execute,
        clear,
        rawStreamData,
        thinkingProcess,

        // --- 条件返回的派生数据 ---
        ...(flatData && { flatData }),
        ...(xmlLikeString && { xmlLikeString }),
    };
}