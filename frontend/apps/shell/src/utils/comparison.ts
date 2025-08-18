// utils/comparison.ts
import { cloneDeep, isEqualWith, isObject } from 'lodash-es';

/**
 * 递归地移除对象中所有值为 undefined 的键。
 * 返回一个新对象，不会修改原始对象。
 * @param obj 要处理的对象
 * @returns 一个没有 undefined 键的新对象
 */
export const omitUndefinedDeep = (obj: any): any => {
    // 你的 omitUndefinedDeep 函数代码保持不变
    if (!isObject(obj)) {
        return obj;
    }
    type IndexableObject = Record<string, any>;
    const newObj = cloneDeep(obj); // 避免修改原始对象

    const recurse = (current: any) => {
        if (!isObject(current)) return;

        if (Array.isArray(current)) {
            current.forEach(item => recurse(item));
            return;
        }

        const currentObject = current as IndexableObject;

        for (const key in currentObject) {
            if (Object.prototype.hasOwnProperty.call(currentObject, key)) {
                if (currentObject[key] === undefined) {
                    delete currentObject[key];
                } else if (isObject(currentObject[key])) {
                    recurse(currentObject[key]);
                }
            }
        }
    };

    recurse(newObj);
    return newObj;
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
export const isEquivalent = (valueA: any, valueB: any): boolean => {
    // 步骤 1: 规范化数据，移除所有 undefined 的键
    const normalizedA = omitUndefinedDeep(valueA);
    const normalizedB = omitUndefinedDeep(valueB);

    // 步骤 2: 定义用于 isEqualWith 的自定义比较器
    const customizer = (objValue: any, othValue: any): boolean | undefined => {
        // 如果两个值都是“空”的（我们定义为 null, undefined, 或 ''），
        // 那么就认为它们是相等的。
        const isObjValueEmpty = objValue === null || objValue === undefined || objValue === '';
        const isOthValueEmpty = othValue === null || othValue === undefined || othValue === '';

        if (isObjValueEmpty && isOthValueEmpty) {
            return true; // 强制认为它们相等
        }

        // 对于所有其他情况，返回 undefined，让 isEqualWith 使用其默认的比较逻辑。
        return undefined;
    };

    // 步骤 3: 使用规范化后的数据和自定义比较器进行深度比较
    return isEqualWith(normalizedA, normalizedB, customizer);
};