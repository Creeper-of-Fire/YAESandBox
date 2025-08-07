/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { VarSpecDef } from './VarSpecDef';
/**
 * 描述一个被消费的变量。
 */
export type ConsumedSpec = {
    /**
     * 被消费的变量名
     */
    name?: string | null;
    def?: VarSpecDef;
    /**
     * 此变量是否可选。默认为 false。
     */
    isOptional?: boolean;
};

