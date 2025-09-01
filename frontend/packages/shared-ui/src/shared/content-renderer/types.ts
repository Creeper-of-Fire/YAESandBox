// src/shared/content-renderer/types.ts

/**
 * 文本节点，代表纯字符串内容。
 */
export interface TextNode {
    type: 'text';
    content: string;
}

/**
 * 标签节点（浅层），代表一个被成功解析的自定义标签。
 * 其内部内容已经被序列化回字符串，供组件消费。
 */
export interface TagNode {
    type: 'tag';
    tagName: string;
    attributes: Record<string, any>;
    /**
     * 标签内部的原始内容，已经被序列化为字符串。
     * 组件可以通过 prop (如 'rawContent') 或默认 slot 接收此内容。
     * 或者其他处理方式
     */
    innerContent: string;
}

/**
 * 错误节点，代表一段无法被正确解析的内容。
 * 渲染器应将此节点渲染为一个清晰的错误提示。
 */
export interface ErrorNode {
    type: 'error';
    /**
     * 向用户展示的、人类可读的错误信息。
     * 例如："Mismatched closing tag. Expected </collapse>, but got </error>."
     */
    message: string;
    /**
     * 导致解析错误的原始文本块，用于在错误提示中展示上下文。
     */
    rawContent: string;
}

/**
 * 抽象语法树（AST）节点。
 * 这是解析器 `parseContent` 函数最终输出的、供渲染器 `ContentRenderer` 消费的节点类型联合。
 */
export type AstNode = TextNode | TagNode | ErrorNode;