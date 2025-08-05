import {cloneDeep} from "lodash-es";
/**
 * 辅助函数：根据 JSON Schema 创建一个“宽容”版的object。
 * 它会填充缺失的必填字段。
 * @param config - 原始的、可能不完整的配置。
 * @param schema - JSON Schema 映射。
 * @returns 一个经过填充的配置，如果无法处理则返回 null。
 */
export function synchronizeModelWithSchema(
    config: any | null,
    schema: any): any | null
{
    if (!config)
    {
        return null;
    }

    // 如果没有对应的 schema，我们无能为力，直接返回原始对象，让它按原样尝试
    if (!schema || !schema.properties)
    {
        console.warn(`未找到Schema，将使用原始数据。`);
        return config;
    }

    // 关键步骤：深度克隆，绝不修改原始的 ref 对象
    const tolerantConfig = cloneDeep(config);

    // 递归函数，用于填充默认值
    function applyDefaults(targetObject: any, schemaObject: any)
    {
        if (!targetObject || typeof targetObject !== 'object' || !schemaObject.properties)
        {
            return;
        }

        for (const key in schemaObject.properties)
        {
            const propSchema = schemaObject.properties[key];
            const isMissing = targetObject[key] === undefined || targetObject[key] === null;

            if (isMissing)
            {
                // 优先使用 schema 中定义的 default 值
                if (propSchema.default !== undefined)
                {
                    targetObject[key] = cloneDeep(propSchema.default);
                    continue;
                }


                // --- 处理 type 是数组的情况 ---
                let primaryType = propSchema.type;
                if (Array.isArray(primaryType)) {
                    // 从类型数组中选择第一个非 "null" 的类型作为主类型
                    primaryType = primaryType.find(t => t !== 'null') || 'null';
                }

                // 根据确定的主类型提供一个安全的空值
                switch (primaryType) {
                    case 'string':
                        targetObject[key] = '';
                        break;
                    case 'number':
                    case 'integer':
                        targetObject[key] = 0;
                        break;
                    case 'boolean':
                        targetObject[key] = false;
                        break;
                    case 'array':
                        targetObject[key] = [];
                        break;
                    case 'object':
                        targetObject[key] = {};
                        if (propSchema.properties) {
                            applyDefaults(targetObject[key], propSchema);
                        }
                        break;
                    case 'null':
                        targetObject[key] = null;
                        break;
                    default:
                        // 对于未知的 primaryType，设置为 null 是最安全的选择
                        console.warn(`[synchronizeModelWithSchema] 在处理属性 "${key}" 时遇到未知类型 "${primaryType}"，默认设置为 null。`);
                        targetObject[key] = null;
                        break;
                }
            }
            else if (propSchema.type === 'object' && targetObject[key])
            {
                // 如果属性已存在且是对象，则递归检查其内部
                applyDefaults(targetObject[key], propSchema);
            }
        }
    }

    applyDefaults(tolerantConfig, schema);
    return tolerantConfig;
}
