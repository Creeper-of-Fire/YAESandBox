import TOML from 'https://esm.sh/@ltd/j-toml@1.38.0';

/**
 * 深度合并两个或多个对象。
 * 对于数组，它会连接它们并去重。对于对象，它会递归地合并它们的属性。
 * @param {...Object} objects - 要合并的对象。
 * @returns {Object} - 合并后的新对象。
 */
export function deepMerge(...objects)
{
    const isObject = obj => obj && typeof obj === 'object';

    return objects.reduce((prev, obj) =>
    {
        Object.keys(obj).forEach(key =>
        {
            const pVal = prev[key];
            const oVal = obj[key];

            if (Array.isArray(pVal) && Array.isArray(oVal))
            {
                // 合并数组并基于 'name' 属性去重
                const combined = [...pVal, ...oVal];
                const unique = Array.from(new Map(combined.map(item => [item.name, item])).values());
                prev[key] = unique;
            } else if (isObject(pVal) && isObject(oVal))
            {
                // 递归合并对象
                prev[key] = deepMerge(pVal, oVal);
            } else
            {
                // 否则直接赋值
                prev[key] = oVal;
            }
        });

        return prev;
    }, {});
}

/**
 * 核心功能：加载并合并所有 API 定义。
 * 它首先加载清单文件，然后根据清单并行加载所有独立的 API JSON 文件，
 * 最后将它们【深度合并】成一个单一的 API 数据对象。
 * @param {string} manifestUrl - 指向 api-manifest.json 的 URL。
 * @returns {Promise<Object>} - 一个解析为合并后 API 数据对象的 Promise。
 */
export async function loadAndMergeApis(manifestUrl)
{
    console.log('[Lua Service] 开始加载 API 清单...');
    try
    {
        const manifestResponse = await fetch(manifestUrl);
        if (!manifestResponse.ok)
        {
            throw new Error(`无法加载清单文件: ${manifestResponse.statusText}`);
        }
        const manifest = await manifestResponse.json();

        // 创建一个包含所有 API 文件 fetch 操作的 Promise 数组
        const fetchPromises = manifest.apiFiles.map(async filePath =>
        {
            // 使用 `new URL()` 来正确解析相对路径
            const apiUrl = new URL(filePath, manifestUrl).href;
            console.log(`[Lua Service] 发现 API 定义: ${apiUrl}`);
            const response = await fetch(apiUrl);
            if (!response.ok)
            {
                throw new Error(`无法加载 API 文件: ${response.statusText}`);
            }
            const tomlText = await response.text();

            try
            {
                // 2. 使用 j-toml 解析 TOML 文本
                // j-toml 的 parse 方法返回一个 Map，我们用 parseAsObject 直接得到 JS 对象
                return TOML.parse(tomlText, {joiner: '\n', bigInt: false});
            } catch (e)
            {
                console.error(`解析 TOML 文件 ${apiUrl} 失败:`, e);
                throw new Error(`解析 TOML 文件 ${apiUrl} 失败: ${e.message}`);
            }
        });

        // 并行等待所有 API 文件加载完成
        const apis = await Promise.all(fetchPromises);

        const mergedApiData = deepMerge({}, ...apis);

        console.log('[Lua Service] 所有 API 定义已成功加载并合并!');
        return mergedApiData;

    } catch (error)
    {
        console.error('[Lua Service] 加载 API 数据时发生严重错误:', error);
        // 在出错时返回一个空对象，让编辑器仍然可以工作，只是没有智能提示。
        return {};
    }
}