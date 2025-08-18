export type GlobalResourceItem<T> = {
    isSuccess: true;
    data: T;
} | {
    isSuccess: false;
    errorMessage: string;
    originJsonString: string | null; // 为失败情况添加原始 JSON 字符串
};