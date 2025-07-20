/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 工作流执行的最终结果。
 */
export type WorkflowExecutionResult = {
    /**
     * 指示工作流是否成功完成。
     */
    isSuccess?: boolean;
    /**
     * 如果失败，包含错误信息；成功则为 null。
     */
    errorMessage?: string | null;
    /**
     * （可选）更具体的错误代码或类型。
     */
    errorCode?: string | null;
};

