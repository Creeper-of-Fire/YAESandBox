/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { VarSpecDef } from './VarSpecDef';
/**
 * 代表一个结构化的记录/对象类型，包含一组带类型的属性。
 */
export type RecordVarSpecDef = {
    /**
     * 变量的类型基础名称/定义的别名
     */
    typeName: string;
    /**
     * 对该类型的全局描述
     */
    description?: string | null;
    /**
     * 定义了此记录类型的所有属性。
     * Key: 属性名 (e.g., "age")
     * Value: 该属性的类型定义 (VarSpecDef)，允许嵌套。
     */
    properties: Record<string, VarSpecDef>;
};

