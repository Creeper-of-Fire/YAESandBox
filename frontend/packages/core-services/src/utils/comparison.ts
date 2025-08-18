// utils/comparison.ts
import {cloneDeep, isEqualWith, isObject} from 'lodash-es';

type IndexableObject = Record<string, any>;

/**
 * 递归地统一两个对象的键结构。
 * 如果一个键只存在于其中一个对象中，则在另一个对象中添加该键，并将其值设置为 undefined。
 * 这确保了在后续比较中，两个对象具有完全相同的键集合。
 * 返回一个包含两个新对象的元组，不会修改原始对象。
 *
 * @param valueA 第一个值
 * @param valueB 第二个值
 * @returns {[any, any]} 包含两个键结构统一后的新值的元组
 */
const unifyKeysDeep = (valueA: any, valueB: any): [any, any] =>
{
    // 创建深度克隆以避免修改原始输入
    const unifiedA = cloneDeep(valueA);
    const unifiedB = cloneDeep(valueB);

    const recurse = (objA: IndexableObject, objB: IndexableObject) =>
    {
        // 检查在调用处，这里假定传入的已经是对象
        const allKeys = new Set([...Object.keys(objA), ...Object.keys(objB)]);

        allKeys.forEach(key =>
        {
            const aHasKey = key in objA;
            const bHasKey = key in objB;

            // 确保双方都有这个键，没有的用 undefined 填充
            if (aHasKey && !bHasKey)
            {
                objB[key] = undefined;
            }
            else if (!aHasKey && bHasKey)
            {
                objA[key] = undefined;
            }

            const valA = objA[key];
            const valB = objB[key];

            const isValAObject = isObject(valA) && !Array.isArray(valA);
            const isValBObject = isObject(valB) && !Array.isArray(valB);

            if (isValAObject || isValBObject)
            {
                // 如果某一方不是对象，将其变为空对象，并进行类型断言
                if (!isValAObject)
                {
                    objA[key] = {} as IndexableObject;
                }
                if (!isValBObject)
                {
                    objB[key] = {} as IndexableObject;
                }
                recurse(objA[key], objB[key]);
            }
        });
    };

    // 顶层预处理：如果其中一个是对象而另一个不是，将非对象的转换为空对象
    const isTopAObject = isObject(unifiedA) && !Array.isArray(unifiedA);
    const isTopBObject = isObject(unifiedB) && !Array.isArray(unifiedB);

    if (isTopAObject && isTopBObject)
    {
        recurse(unifiedA, unifiedB);
    }
    else if (Array.isArray(unifiedA) && Array.isArray(unifiedB))
    {
        const maxLength = Math.max(unifiedA.length, unifiedB.length);
        for (let i = 0; i < maxLength; i++)
        {
            const itemA = unifiedA[i];
            const itemB = unifiedB[i];
            const isItemAObject = isObject(itemA) && !Array.isArray(itemA);
            const isItemBObject = isObject(itemB) && !Array.isArray(itemB);

            if (isItemAObject || isItemBObject)
            {
                if (!isItemAObject) unifiedA[i] = {} as IndexableObject;
                if (!isItemBObject) unifiedB[i] = {} as IndexableObject;
                recurse(unifiedA[i], unifiedB[i]);
            }
        }
    }

    return [unifiedA, unifiedB];
};


/**
 * 使用自定义业务逻辑深度比较两个值是否“等效”。
 *
 * 此比较包含两个关键步骤：
 * 1. **规范化**: 递归地移除两个值中所有 `undefined` 的属性。
 *    这使得 { a: 1, b: undefined } 和 { a: 1 } 被视为相等。
 * 2. **空值处理**: 将 `null`, `undefined`, 和 `''` (空字符串) 视作相等的值。
 *
 * @param valueA 第一个比较值
 * @param valueB 第二个比较值
 * @returns {boolean} 如果两个值根据上述规则被认为是等效的，则返回 true。
 */
export const isEquivalent = (valueA: any, valueB: any): boolean =>
{
    // 步骤 1: 统一键结构，用 undefined 填充缺失的键
    const [unifiedA, unifiedB] = unifyKeysDeep(valueA, valueB);

    // 步骤 2: 定义用于 isEqualWith 的自定义比较器
    const customizer = (objValue: any, othValue: any): boolean | undefined =>
    {
        // 如果两个值都是“空”的（我们定义为 null, undefined, 或 ''），
        // 那么就认为它们是相等的。
        const isObjValueEmpty = objValue === null || objValue === undefined || objValue === '';
        const isOthValueEmpty = othValue === null || othValue === undefined || othValue === '';

        if (isObjValueEmpty && isOthValueEmpty)
        {
            return true; // 强制认为它们相等
        }

        // 对于所有其他情况，返回 undefined，让 isEqualWith 使用其默认的比较逻辑。
        return undefined;
    };

    // 步骤 3: 使用规范化后的数据和自定义比较器进行深度比较
    const isEqual = isEqualWith(unifiedA, unifiedB, customizer);

    return isEqual;
};