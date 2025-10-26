/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { EmittedContentSpec } from './EmittedContentSpec';
import type { UpdateMode } from './UpdateMode';
/**
 * 描述一个由工作流向外部发射的事件的静态契约。
 * 这是工作流“API文档”的一部分，用于描述其副作用。
 */
export type EmittedEventSpec = {
    /**
     * 事件发射到的逻辑地址（Path）。
     */
    address: string;
    mode: UpdateMode;
    /**
     * 对该事件用途的人类可读描述。
     */
    description: string;
    /**
     * 声明此事件的源符文的ConfigId。
     */
    sourceRuneConfigId: string;
    contentSpec?: EmittedContentSpec;
};

