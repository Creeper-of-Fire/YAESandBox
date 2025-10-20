/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { MockRunRequestDto } from '../models/MockRunRequestDto';
import type { MockRunResponseDto } from '../models/MockRunResponseDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class MockRuneService {
    /**
     * 执行一次即时测试。
     * @returns MockRunResponseDto OK
     * @throws ApiError
     */
    public static postApiV1WorkflowRuneMockRun({
        requestBody,
    }: {
        requestBody?: MockRunRequestDto,
    }): CancelablePromise<MockRunResponseDto> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/v1/workflow/rune/mock-run',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
