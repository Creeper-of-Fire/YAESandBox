/**
 * 为 ModalResult 的 match 方法定义的回调函数集合。
 * 所有回调都是可选的，允许调用者只处理他们关心的结果。
 */
export type ResultMatcher<T, R> = {
    /**
     * 当结果状态为 'ok' 时调用。
     * @param data 成功时附带的数据。
     * @returns 返回一个 R 类型的值。
     */
    ok?: (data: T) => R;
    /**
     * 当结果状态为 'cancel' 时调用。
     * @returns 返回一个 R 类型的值。
     */
    cancel?: () => R;
};

/**
 * 一个健壮的、类型安全的结果类，用于封装可能成功 (ok) 或被取消 (cancel) 的操作结果。
 * 它清晰、灵活，并鼓励良好的编程实践。
 */
export class ModalResult<T>
{
    public readonly status: 'ok' | 'cancel';
    public readonly data: T | undefined;

    private constructor(status: 'ok' | 'cancel', data?: T)
    {
        this.status = status;
        this.data = data;
    }

    public static ok<T>(data: T): ModalResult<T>
    {
        return new ModalResult('ok' as const, data);
    }

    public static cancel<T>(): ModalResult<T>
    {
        return new ModalResult<T>('cancel' as const, undefined);
    }

    public isOk(): this is this & { status: 'ok'; data: T }
    {
        return this.status === 'ok';
    }

    public isCancel(): this is this & { status: 'cancel'; data: undefined }
    {
        return this.status === 'cancel';
    }

    /**
     * 对结果进行模式匹配，并根据当前状态执行相应的回调。
     * @param matcher 包含 `ok` 和 `cancel` 回调的对象。
     * @returns 如果有匹配的回调被执行，则返回其返回值；否则返回 undefined。
     */
    public match<R>(matcher: ResultMatcher<T, R>): R | undefined
    {
        if (this.isOk() && matcher.ok)
        {
            return matcher.ok(this.data);
        }
        if (this.isCancel() && matcher.cancel)
        {
            return matcher.cancel();
        }
        // 如果没有提供匹配的回调，则显式返回 undefined
        return undefined;
    }
}