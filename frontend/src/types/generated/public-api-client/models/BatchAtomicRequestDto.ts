/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AtomicOperationRequestDto } from './AtomicOperationRequestDto';
/**
 * 包含用于批量执行的原子操作请求列表。
 */
export type BatchAtomicRequestDto = {
    /**
     * 要执行的原子操作请求列表。该列表不能为空。
     */
    operations: Array<AtomicOperationRequestDto>;
};

