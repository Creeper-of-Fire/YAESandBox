// utils/comparison.ts
import {cloneDeep, isEqualWith, isObject} from 'lodash-es';

type IndexableObject = Record<string, any>;

/**
* 递归地统一两个值的结构。
* - 如果是对象，则统一它们的键。缺失的键将被添加并赋值为 undefined。
* - 如果是数组，则统一它们的长度。
* - 如果一个值是对象/数组而另一个不是，则将非容器值转换为空的对应类型（{} 或 []）。
* 这确保了在后续比较中，两个值具有完全相同的“形状”。
* 返回一个包含两个新值的元组，不会修改原始对象。
*
* @param valueA 第一个值
* @param valueB 第二个值
* @returns {[any, any]} 包含两个结构统一后的新值的元组
*/
const unifyKeysDeep = (valueA: any, valueB: any): [any, any] =>
{
    // 内部递归函数，它总是返回一个元组 [unifiedA, unifiedB]
    const traverse = (a: any, b: any): [any, any] =>
    {
        // 使用 cloneDeep 来确保我们操作的是副本，避免深层嵌套的引用问题
        const unifiedA = cloneDeep(a);
        const unifiedB = cloneDeep(b);

        const isAObject = isObject(unifiedA) && !Array.isArray(unifiedA);
        const isBObject = isObject(unifiedB) && !Array.isArray(unifiedB);
        const isAArray = Array.isArray(unifiedA);
        const isBArray = Array.isArray(unifiedB);

        // Case 1: 两个都是对象
        if (isAObject && isBObject)
        {
            const objA = unifiedA as IndexableObject;
            const objB = unifiedB as IndexableObject;
            const allKeys = new Set([...Object.keys(objA), ...Object.keys(objB)]);
            allKeys.forEach(key =>
            {
                const [newValA, newValB] = traverse(objA[key], objB[key]);
                objA[key] = newValA;
                objB[key] = newValB;
            });
            return [objA, objB];
        }

        // Case 2: 两个都是数组
        if (isAArray && isBArray)
        {
            const maxLength = Math.max(unifiedA.length, unifiedB.length);
            for (let i = 0; i < maxLength; i++)
            {
                const [newItemA, newItemB] = traverse(unifiedA[i], unifiedB[i]);
                unifiedA[i] = newItemA;
                unifiedB[i] = newItemB;
            }
            return [unifiedA, unifiedB];
        }

        // Case 3: 类型不匹配（一个是对象/数组，另一个不是）
        // 我们将非容器类型提升为空容器，然后再次调用 traverse
        if (isAObject || isBObject)
        {
            return traverse(isAObject ? unifiedA : {}, isBObject ? unifiedB : {});
        }
        if (isAArray || isBArray)
        {
            return traverse(isAArray ? unifiedA : [], isBArray ? unifiedB : []);
        }

        // Case 4: 两个都不是对象或数组（原始类型，null等），直接返回
        return [unifiedA, unifiedB];
    };

    return traverse(valueA, valueB);
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