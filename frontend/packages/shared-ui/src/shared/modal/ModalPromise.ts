/**
 * 一个增强的 Promise-like 对象，为模态框操作提供了链式调用的 API。
 * 它允许以一种更具表现力的方式处理成功 (ok) 和取消 (cancel) 的情况。
 */
export class ModalPromise<T> implements PromiseLike<T | undefined>
{
    private promise: Promise<T | undefined>;

    constructor(executor: (resolve: (value: T | undefined) => void) => void)
    {
        this.promise = new Promise(executor);
    }

    /**
     * 实现 PromiseLike 接口，允许此对象被 `await`。
     */
    then<TResult1 = T | undefined, TResult2 = never>(
        onfulfilled?: ((value: T | undefined) => TResult1 | PromiseLike<TResult1>) | null,
        onrejected?: ((reason: any) => TResult2 | PromiseLike<TResult2>) | null
    ): PromiseLike<TResult1 | TResult2>
    {
        return this.promise.then(onfulfilled, onrejected);
    }

    /**
     * 注册一个当模态框成功返回数据时执行的回调。
     * @param callback 成功时执行的函数，接收返回的数据作为参数。
     * @returns 返回自身，以支持链式调用。
     */
    public onOk(callback: (data: T) => void): this
    {
        this.promise.then(result =>
        {
            if (result !== undefined)
            {
                callback(result);
            }
        });
        return this;
    }

    /**
     * 注册一个当模态框被取消时执行的回调。
     * @param callback 取消时执行的函数。
     * @returns 返回自身，以支持链式调用。
     */
    public onCancel(callback: () => void): this
    {
        this.promise.then(result =>
        {
            if (result === undefined)
            {
                callback();
            }
        });
        return this;
    }

    /**
     * 注册一个无论成功或取消都会执行的回调。
     * @param callback 完成时执行的函数。
     * @returns 返回自身，以支持链式调用。
     */
    public onFinally(callback: () => void): this
    {
        this.promise.finally(callback);
        return this;
    }
}