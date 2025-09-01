import type {AstNode, ErrorNode, TextNode} from '../types';
import type {ComponentContract} from './componentRegistry';

// --- 内部使用的、深度的AST类型 ---

interface DeepTagNode
{
    type: 'tag';
    tagName: string;
    attributes: Record<string, any>;
    children: DeepAstNode[]; // 子内容是节点数组
}

type DeepAstNode = TextNode | DeepTagNode | ErrorNode;

// --- 工具函数 ---

// 正则表达式用于解析属性字符串
// 支持：key="value" key='value' key=value key
const attributeRegex = /([\p{L}\p{N}_-]+)(?:=(?:"([^"]*)"|'([^']*)'|([^\s>]+)))?/gu;

function parseAttributes(attrString: string): Record<string, any>
{
    const attributes: Record<string, any> = {};
    for (const match of attrString.matchAll(attributeRegex))
    {
        const key = match[1];
        const value = match[2] ?? match[3] ?? match[4] ?? true;
        attributes[key] = value;
    }
    return attributes;
}

/**
 * 内部核心解析器。
 * 根据组件契约，严格地将字符串解析为深度AST。
 * 任何不符合规范的情况都会生成 ErrorNode。
 * @private
 */
function _strictParse(rawContent: string, contracts: Map<string, ComponentContract>): DeepAstNode[]
{
    // 使用 g 和 u 标志。u 支持 Unicode，g 使得 exec 可以连续查找
    const tagRegex = /<(\/)?([\p{L}\p{N}_-]+)([^<>]*)>/gu;

    const root: DeepTagNode = {type: 'tag', tagName: 'root', attributes: {}, children: []};
    const stack: DeepTagNode[] = [root];
    let lastIndex = 0;
    let match: RegExpExecArray | null;

    // 使用 while 循环和 regex.exec() 以便完全控制解析指针 (regex.lastIndex)
    while ((match = tagRegex.exec(rawContent)) !== null)
    {
        const [fullMatch, isClosing, tagName, attrString] = match;
        const currentIndex = match.index;
        const currentParent = stack[stack.length - 1];

        // 1. 处理上一个标签到当前标签之间的纯文本
        if (currentIndex > lastIndex)
        {
            currentParent.children.push({type: 'text', content: rawContent.substring(lastIndex, currentIndex)});
        }

        const tagNameLower = tagName.toLowerCase();
        const contract = contracts.get(tagNameLower);

        if (!contract)
        {
            // 2a. 未知标签：直接视为纯文本
            currentParent.children.push({type: 'text', content: fullMatch});
        }
        else
        {
            // 2b. 已知标签：根据契约严格处理
            if (isClosing)
            {
                if (stack.length > 1 && stack[stack.length - 1].tagName.toLowerCase() === tagNameLower)
                {
                    stack.pop(); // 匹配成功，出栈
                }
                else
                {
                    const expected = stack.length > 1 ? `</${stack[stack.length - 1].tagName}>` : 'no open tag';
                    const errorNode: ErrorNode = {
                        type: 'error',
                        message: `不匹配的闭合标签。期望 ${expected}，但是得到 ${fullMatch}。`,
                        rawContent: fullMatch
                    }
                    currentParent.children.push(errorNode);
                }
            }
            else
            { // 开放标签
                const newNode: DeepTagNode = {
                    type: 'tag',
                    tagName: tagName,
                    attributes: parseAttributes(attrString.trim()),
                    children: [],
                };
                currentParent.children.push(newNode);

                const isSelfClosing = attrString.trim().endsWith('/');

                if (contract.parseMode === 'strict' && !isSelfClosing)
                {
                    // 严格模式：入栈，等待子节点和闭合标签
                    stack.push(newNode);
                }
                else if (contract.parseMode === 'raw' && !isSelfClosing)
                {
                    // 原始模式：不入栈。立即寻找闭合标签并快进
                    const endTag = `</${tagName}>`;
                    // 从当前标签结束后开始搜索
                    const endTagIndex = rawContent.indexOf(endTag, tagRegex.lastIndex);

                    if (endTagIndex !== -1)
                    {
                        const innerContent = rawContent.substring(tagRegex.lastIndex, endTagIndex);
                        if (innerContent)
                        {
                            newNode.children.push({type: 'text', content: innerContent});
                        }
                        // 快进解析器的指针到闭合标签之后
                        tagRegex.lastIndex = endTagIndex + endTag.length;
                    }
                    else
                    {
                        // 未找到闭合标签。我们什么都不做，让最后的“未闭合标签检查”来捕获这个错误。
                        // 这统一了所有未闭合标签的错误处理逻辑。
                    }
                }
            }
        }
        lastIndex = tagRegex.lastIndex;
    }

    // 3. 检查栈中所有未闭合的已知标签
    if (stack.length > 1)
    {
        // 从最后一个未闭合的标签开始，将其错误信息附加到其父节点
        while (stack.length > 1)
        {
            const unclosedNode = stack.pop()!;
            const errorNode:ErrorNode = {
                type: 'error',
                message: `Unclosed tag: <${unclosedNode.tagName}> was not closed.`,
                // 原始文本是从标签开始到内容末尾
                rawContent: `<${unclosedNode.tagName}>`
            }
            stack[stack.length - 1].children.push(errorNode);
        }
    }

    // 4. 处理文档末尾的最后一个纯文本块
    if (lastIndex < rawContent.length)
    {
        root.children.push({type: 'text', content: rawContent.substring(lastIndex)});
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
        if (node.type === 'error')
        {
            // 通常不应序列化错误，但为了健壮性，返回其原始文本
            return node.rawContent;
        }

        // 序列化属性
        const attrs = Object.entries(node.attributes)
            .map(([key, value]) =>
            {
                if (value === true) return key;
                // 确保属性值中的双引号被正确转义
                return `${key}="${String(value).replace(/"/g, '&quot;')}"`;
            })
            .join(' ');

        const openingTag = `<${node.tagName}${attrs ? ' ' + attrs : ''}>`;

        if (!node.children || node.children.length === 0)
        {
            // 对于没有内容的标签，考虑是否自闭合，但为简单起见，统一使用开闭标签
            return `${openingTag}</${node.tagName}>`;
        }

        const inner = _serializeAstNodes(node.children);
        const closingTag = `</${node.tagName}>`;

        return `${openingTag}${inner}${closingTag}`;
    }).join('');
}


/**
 * 公开函数：将原始字符串解析为浅层AST。
 * 这是模块的最终解析入口。
 * @param rawContent 原始字符串
 * @param contracts 组件契约注册表
 * @returns 浅层AST节点数组
 */
export function parseContent(rawContent: string, contracts: Map<string, ComponentContract>): AstNode[]
{
    const deepNodes = _strictParse(rawContent, contracts);

    // 将深度AST转换为供渲染器使用的浅层AST
    return deepNodes.map((node): AstNode =>
    {
        if (node.type === 'text' || node.type === 'error')
        {
            // 直接传递文本和错误节点
            return node;
        }
        else
        {
            // 对于标签节点，将其子节点序列化回字符串
            return {
                type: 'tag',
                tagName: node.tagName,
                attributes: node.attributes,
                innerContent: _serializeAstNodes(node.children),
            };
        }
    });
}