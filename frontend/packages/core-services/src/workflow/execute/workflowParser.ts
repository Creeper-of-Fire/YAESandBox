import type {ComplexPropertyValue} from './streaming.ts';


/**
 * 从 ComplexPropertyValue 中安全地提取【用于最终显示】的文本。
 * 这个函数是一个视图辅助工具，它会丢失所有嵌套结构。
 * @param value 要解析的值
 * @returns 提取出的顶层字符串，如果没有则返回空字符串
 */
export function getText(value: ComplexPropertyValue | undefined): string
{
    return value?._text || '';
}

/**
 * 递归地遍历一个对象，提取所有嵌套的 <think> 标签的内容，并将它们连接成一个字符串。
 * 这对于在调试视图中显示完整的思考链非常有用。
 * @param data ComplexPropertyValue
 * @returns 拼接好的所有思考过程字符串
 */
export function extractAllThoughts(data: ComplexPropertyValue): string
{
    const thoughts: string[] = [];

    function traverse(obj: any): void
    {
        if (!obj || typeof obj !== 'object')
        {
            return;
        }

        // 检查当前对象是否是一个包含 <think> 标签的 ComplexPropertyValue
        if (obj.think && typeof obj.think._text === 'string' && obj.think._text.trim())
        {
            thoughts.push(obj.think._text.trim());
        }

        // 递归遍历所有属性值
        for (const key in obj)
        {
            // 避免无限循环和重复处理 think 标签本身
            if (key !== 'think')
            {
                traverse(obj[key]);
            }
        }
    }

    traverse(data);
    return thoughts.join('\n\n---\n\n');
}