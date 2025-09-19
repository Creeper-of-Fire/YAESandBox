// 从 GameObject 中提取的、用于在UI中展示的最小信息集
export interface SelectedObjectInfo {
    id: string;
    type: string;
    // 未来可以添加更多描述性信息，如名称、状态等
}

// 用于展示的场信息
export interface SelectedFieldInfo {
    name: string; // e.g., 'light_level'
    value: number;
}

// 用于展示的粒子信息
export interface SelectedParticleInfo {
    type: string;
    count: number;
}

// Pinia store中存储的完整选择信息
export interface SelectionDetails {
    objects: SelectedObjectInfo[];
    fields: SelectedFieldInfo[];
    particles: SelectedParticleInfo[];
}

/**
 * 定义了不同类型的用户意图。
 * 这将驱动UI显示何种 "意图组件"，以及背后调用哪个工作流。
 */
export enum InstructionType {
    ENRICH_OBJECT = 'ENRICH_OBJECT',
    // 未来可以扩展
    // CREATE_CHARACTER = 'CREATE_CHARACTER',
    // GENERATE_LORE = 'GENERATE_LORE',
}

/**
 * 指令在其生命周期中所处的状态。
 */
export enum InstructionStatus {
    PENDING_USER_INPUT = 'PENDING_USER_INPUT', // 等待用户输入（例如，填写prompt）
    GENERATING = 'GENERATING',                 // 正在调用AI工作流
    PROPOSED = 'PROPOSED',                     // AI已返回提案，等待用户决策
    APPLYING = 'APPLYING',                     // 用户已点击“应用”，正在修改WorldState
    APPLIED = 'APPLIED',                       // 提案已成功应用
    DISCARDED = 'DISCARDED',                   // 用户已丢弃此指令
    ERROR = 'ERROR',                           // 生成或应用过程中发生错误
}

/**
 * '意图组件' 的核心数据结构。
 * 它封装了一次完整的、原子化的 "用户意图 -> AI执行 -> 用户决策" 循环。
 */
export interface Instruction {
    id: string;
    type: InstructionType;
    status: InstructionStatus;

    // 执行意图所需的上下文
    context: {
        targetObjectId?: string; // e.g., for ENRICH_OBJECT
        // targetGridPos?: {x: number, y: number}; // e.g., for CREATE_CHARACTER
    };

    // 用户输入的附加指令
    userInput: {
        prompt: string;
    };

    // AI返回的提案，其结构由意图类型决定
    aiProposal: Record<string, any> | null;

    // 存储工作流执行期间的错误信息
    error: string | null;

    // 时间戳等元数据
    readonly createdAt: number;
}