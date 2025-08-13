import type {AstNode, TextNode} from '../types';

// --- 内部使用的、深度的AST类型 ---

interface DeepTagNode
{
    type: 'tag';
    tagName: string;
    attributes: Record<string, any>;
    children: DeepAstNode[]; // 子内容是节点数组
}

type DeepAstNode = TextNode | DeepTagNode;

// 正则表达式用于解析属性字符串
// 支持：key="value" key='value' key=value key
const attributeRegex = /([\p{L}\p{N}_-]+)(?:=(?:"([^"]*)"|'([^']*)'|([^\s>]+)))?/gu;

function parseAttributes(attrString: string): Record<string, any>
{
    const attributes: Record<string, any> = {};
    for (const match of attrString.matchAll(attributeRegex))
    {
        const key = match[1];
        // 优先取双引号、单引号、无引号的值，如果都没有，则认为是布尔真值
        const value = match[2] ?? match[3] ?? match[4] ?? true;
        attributes[key] = value;
    }
    return attributes;
}

/**
 * 将包含自定义标签的字符串解析为抽象语法树 (AST)
 * @param rawContent 原始字符串
 * @returns AST节点数组
 * @private
 */
function _recursiveParse(rawContent: string): DeepAstNode[]
{
    // 正则表达式用于查找所有标签（开标签、闭标签）
    const tagRegex = /<(\/)?([\p{L}\p{N}_-]+)([^>]*)>/gu;

    const root: DeepTagNode = {type: 'tag', tagName: 'root', attributes: {}, children: []};
    const stack: DeepTagNode[] = [root];
    let lastIndex = 0;

    for (const match of rawContent.matchAll(tagRegex))
    {
        const [fullMatch, isClosing, tagName, attrString] = match;
        const currentIndex = match.index!;
        const currentParent = stack[stack.length - 1];

        // 1. 处理标签前的纯文本
        if (currentIndex > lastIndex)
        {
            const text = rawContent.substring(lastIndex, currentIndex);
            currentParent.children.push({type: 'text', content: text});
        }

        if (isClosing)
        {
            // 2. 处理闭合标签
            if (stack.length > 1 && stack[stack.length - 1].tagName.toLowerCase() === tagName.toLowerCase())
            {
                stack.pop();
            }
            else
            {
                // 标签不匹配，当作纯文本处理
                currentParent.children.push({type: 'text', content: fullMatch});
            }
        }
        else
        {
            // 3. 处理开放标签
            const newNode: DeepTagNode = {
                type: 'tag',
                tagName: tagName,
                attributes: parseAttributes(attrString.trim()),
                children: [],
            };
            currentParent.children.push(newNode);

            // 如果不是自闭合标签（不以'/'结尾），则推入栈中等待子节点
            if (!attrString.trim().endsWith('/'))
            {
                stack.push(newNode);
            }
        }

        lastIndex = currentIndex + fullMatch.length;
    }

    // 4. 处理最后一个标签后的纯文本
    if (lastIndex < rawContent.length)
    {
        const text = rawContent.substring(lastIndex);
        stack[stack.length - 1].children.push({type: 'text', content: text});
    }

    return root.children;
}

/**
 * 内部函数：将深度AST节点数组序列化回字符串。
 * @private
 */
function _serializeAstNodes(nodes: DeepAstNode[]): string
{
    return nodes.map(node =>
    {
        if (node.type === 'text')
        {
            return node.content;
        }

        // 序列化属性
        const attrs = Object.entries(node.attributes)
            .map(([key, value]) =>
            {
                if (value === true) return key;
                return `${key}="${String(value).replace(/"/g, '"')}"`;
            })
            .join(' ');

        const openingTag = `<${node.tagName}${attrs ? ' ' + attrs : ''}>`;
        const inner = _serializeAstNodes(node.children);
        const closingTag = `</${node.tagName}>`;

        return `${openingTag}${inner}${closingTag}`;
    }).join('');
}

/**
 * 公开函数：将原始字符串解析为浅层AST。
 * 这是模块的最终解析入口。
 */
export function parseContent(rawContent: string): AstNode[]
{
    // 1. 进行完整的深度解析
    const deepNodes = _recursiveParse(rawContent);

    // 2. 遍历顶层节点，并转换它们
    return deepNodes.map((node): AstNode =>
    {
        if (node.type === 'text')
        {
            // 文本节点保持不变
            return node;
        }
        else
        {
            // 标签节点：将其子节点序列化回字符串
            return {
                type: 'tag',
                tagName: node.tagName,
                attributes: node.attributes,
                innerContent: _serializeAstNodes(node.children),
            };
        }
    });
}