/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { StoredConfigMeta } from './StoredConfigMeta';
import type { StoredConfigRef } from './StoredConfigRef';
import type { TuumConfig } from './TuumConfig';
/**
 * 一个通用的、可持久化的配置包装器。
 */
export type TuumConfigStoredConfig = {
    storeRef?: StoredConfigRef;
    content: TuumConfig;
    /**
     * 指示此配置是否为只读。
     * 只读配置（如内置模板）不能通过API进行修改或删除。
     */
    isReadOnly: boolean;
    meta?: StoredConfigMeta;
};

