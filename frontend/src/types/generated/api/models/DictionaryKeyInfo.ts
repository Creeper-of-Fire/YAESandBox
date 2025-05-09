/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { SchemaDataType } from './SchemaDataType';
import type { SelectOption } from './SelectOption';
/**
 * 描述字典类型字段中“键”的相关信息。
 */
export type DictionaryKeyInfo = {
    keyType: SchemaDataType;
    /**
     * 如果 KeyType 是 Enum，这里提供枚举的选项列表。
     */
    enumOptions?: Array<SelectOption> | null;
    /**
     * 原始C#键类型名称，用于调试或特定场景。
     */
    rawKeyTypeName?: string | null;
};

