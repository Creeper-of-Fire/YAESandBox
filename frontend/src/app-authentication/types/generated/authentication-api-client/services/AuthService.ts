/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AuthResponse } from '../models/AuthResponse';
import type { LoginRequest } from '../models/LoginRequest';
import type { RegisterRequest } from '../models/RegisterRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class AuthService {
    /**
     * @returns string OK
     * @throws ApiError
     */
    public static postApiV1AuthRegister({
        requestBody,
    }: {
        requestBody?: RegisterRequest,
    }): CancelablePromise<string> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/v1/auth/register',
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `Bad Request`,
            },
        });
    }
    /**
     * @returns AuthResponse OK
     * @throws ApiError
     */
    public static postApiV1AuthLogin({
        requestBody,
    }: {
        requestBody?: LoginRequest,
    }): CancelablePromise<AuthResponse> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/v1/auth/login',
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                401: `Unauthorized`,
            },
        });
    }
}
