export type GlobalResourceItemSuccess<T> = {
    isSuccess: true;
    data: T;
};

export type GlobalResourceItemFailure = {
    isSuccess: false;
    errorMessage: string;
    originJsonString: string | null; // 为失败情况添加原始 JSON 字符串
};


export type GlobalResourceItem<T> = GlobalResourceItemSuccess<T> | GlobalResourceItemFailure;