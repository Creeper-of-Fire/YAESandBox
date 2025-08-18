/**
 * 代表一条聊天消息的结构
 */
export type ChatMessage = {
    id: string; // 每条消息的唯一ID，用于v-for的key
    role: 'User' | 'Assistant'; // 消息的角色
    content: string; // 消息内容
};

/**
 * 发送给工作流的提示词格式
 */
export type Prompt = {
    role: 'User' | 'Assistant';
    content: string;
}