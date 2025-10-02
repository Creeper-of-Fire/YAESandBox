/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AbstractRuneConfig } from './AbstractRuneConfig';
import type { StoredConfigMeta } from './StoredConfigMeta';
import type { StoredConfigRef } from './StoredConfigRef';
/**
 * 一个通用的、可持久化的配置包装器。
 */
export type AbstractRuneConfigStoredConfig = {
    storeRef?: StoredConfigRef;
    content: AbstractRuneConfig;
    /**
     * 指示此配置是否为只读。
     * 只读配置（如内置模板）不能通过API进行修改或删除。
     */
    isReadOnly: boolean;
    meta?: StoredConfigMeta;
};

