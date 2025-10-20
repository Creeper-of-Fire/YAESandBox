/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { VarSpecDef } from './VarSpecDef';
/**
 * 代表一个列表类型，包含一组相同类型的元素。
 */
export type ListVarSpecDef = {
    /**
     * 变量的类型基础名称/定义的别名
     */
    typeName: string;
    /**
     * 对该类型的全局描述
     */
    description?: string | null;
    elementDef: VarSpecDef;
};

