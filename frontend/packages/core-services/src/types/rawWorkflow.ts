// 去掉冗余部分后的原始工作流相关配置

/**
 * 工作流的配置
 */
export interface RawWorkflowConfig {
    /**
     * 名字
     */
    name: string;
    /**
     * 声明此工作流启动时需要提供的入口参数列表。
     * 这些输入可以作为连接的源头。
     */
    workflowInputs: Array<string>;
    /**
     * 一个工作流含有的枢机（有序）
     */
    tuums: Array<RawTuumConfig>;
}

/**
 * 枢机的配置
 */
export interface RawTuumConfig  {
    /**
     * 名字
     */
    name: string;
    /**
     * 是否被启用，默认为True
     */
    enabled: boolean;
    /**
     * 唯一的 ID，在拷贝时也需要更新
     */
    configId: string;
    /**
     * 按顺序执行的符文列表。
     * TuumProcessor 在执行时会严格按照此列表的顺序执行符文。
     */
    runes: Array<RawAbstractRuneConfig>;
}

/**
 * 符文的配置
 */
export interface RawAbstractRuneConfig {
    /**
     * 名字
     */
    name: string;
    /**
     * 是否被启用，默认为True
     */
    enabled: boolean;
    /**
     * 唯一的 ID，在拷贝时也需要更新
     */
    configId: string;
    /**
     * 符文的类型
     */
    runeType: string;
}

