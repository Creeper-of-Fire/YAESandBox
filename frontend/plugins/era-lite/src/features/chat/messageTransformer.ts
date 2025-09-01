/**
 * 默认的消息内容转换器。
 * 将特定的消息内容（合并流式后的）标签转换为可渲染的 <collapse> 组件标签。
 * @param content 原始消息字符串
 * @returns 转换后的字符串
 */
export function defaultTransformMessageContent(content: string): string
{
    let transformedContent = content;
    const targetComponentTag = 'collapse';

    // --- 步骤1: 处理那些需要“提升子标签为name”的父标签 ---

    // 定义需要此行为的父标签列表
    const parentTagsToTransform = ['detail', 'collapse'];

    // 根据列表动态创建一个正则表达式，例如: /<(detail|collapse)>(.*?)<\/\1>/gs
    const parentTagRegex = new RegExp(`<(${parentTagsToTransform.join('|')})>(.*?)<\\/\\1>`, 'gs');

    transformedContent = transformedContent.replace(parentTagRegex, (match, parentTagName, innerContent) =>
    {
        // 这里的 parentTagName 会是 'detail' 或 'collapse'

        const parser = new DOMParser();
        // 使用 text/html 解析器更宽容，能处理非严格的XML
        const doc = parser.parseFromString(`<div>${innerContent}</div>`, 'text/html');
        const wrapper = doc.body.firstChild as Element;

        if (!wrapper) return ''; // 如果内部为空，则返回空字符串

        // 遍历所有直接子元素，将它们转换为目标组件 <collapse>
        return Array.from(wrapper.children)
            .map(element =>
            {
                const childTagName = element.tagName.toLowerCase(); // 子标签名作为 name
                const innerHTML = element.innerHTML; // 保留子标签内的所有内容
                // 使用模板字符串和变量来构建，而不是硬编码
                return `<${targetComponentTag} name="${childTagName}">${innerHTML}</${targetComponentTag}>`;
            })
            .join(''); // 将所有转换后的 <collapse> 拼接起来
    });

    // 规则 2: 处理独立的 <think> 和 <summary> 标签
    // 这个替换也会处理那些不在 <detail> 标签内的 <think> 和 <summary>
    transformedContent = transformedContent.replace(/<think>(.*?)<\/think>/gs, `<${targetComponentTag} name="思维链">$1</${targetComponentTag}>`);
    transformedContent = transformedContent.replace(/<summary>(.*?)<\/summary>/gs, `<${targetComponentTag} name="总结">$1</${targetComponentTag}>`);

    return transformedContent;
}