/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 代表工作流中一个可连接的端点。
 * 它由枢机的唯一ID和该枢机上的一个输入/输出变量名组成。
 */
export type TuumConnectionEndpoint = {
    /**
     * 端点所属枢机的ConfigId。
     */
    tuumId?: string | null;
    /**
     * 端点的名称，对应于TuumConfig中Input/Output Mappings的Value。
     */
    endpointName?: string | null;
};

