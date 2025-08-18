/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 工作流 的 DTO。
 */
export type WorkflowDto = {
    /**
     * 存储触发block时所使用的工作流名称。
     */
    workflowName: string;
    /**
     * （仅父 Block 存储）触发此 Block 的某个子 Block 时所使用的参数。
     * 注意：当前设计只保存最后一次触发子节点时的参数。
     */
    triggeredChildParams: Record<string, string>;
    /**
     * 被触发时使用的参数。用于重新生成之类的。
     */
    triggeredParams: Record<string, string>;
};

