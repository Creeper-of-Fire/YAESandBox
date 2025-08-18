// src/shared/content-renderer/types.ts

/**
 * 文本节点，代表纯字符串内容
 */
export interface TextNode {
    type: 'text';
    content: string;
}

/**
 * 标签节点（浅层），代表一个自定义标签及其未经解析的内部内容
 */
export interface TagNode {
    type: 'tag';
    tagName: string;
    attributes: Record<string, any>;
    /**
     * 标签内部的原始字符串内容。
     * 子组件可以自己决定如何处理这段内容。
     */
    innerContent: string;
}

/**
 * 抽象语法树（AST）节点（浅层）
 */
export type AstNode = TextNode | TagNode;