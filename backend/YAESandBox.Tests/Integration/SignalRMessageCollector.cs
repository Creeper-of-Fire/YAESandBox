// --- START OF FILE SignalRMessageCollector.cs ---

using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit.Abstractions;
using YAESandBox.API.DTOs.WebSocket;

namespace YAESandBox.Tests.Integration;

/// <summary>
/// 辅助类，用于收集和等待来自 SignalR Hub 的特定消息。
/// </summary>
public class SignalRMessageCollector(HubConnection connection, ITestOutputHelper output)
{
    private readonly ConcurrentDictionary<Type, ConcurrentQueue<object>> _receivedMessages = new();
    private readonly ConcurrentDictionary<Type, SemaphoreSlim> _messageSemaphores = new();
    private readonly HubConnection _connection = connection;
    private readonly ITestOutputHelper _output = output; // 用于输出测试日志

    /// <summary>
    /// 注册一个处理器来监听特定类型的消息。
    /// </summary>
    /// <typeparam name="TMessage">要监听的消息类型。</typeparam>
    /// <param name="methodName">Hub 上用于发送此类型消息的方法名。</param>
    public void RegisterHandler<TMessage>(string methodName) where TMessage : class
    {
        var messageType = typeof(TMessage);
        this._receivedMessages.TryAdd(messageType, new ConcurrentQueue<object>());
        var semaphore = this._messageSemaphores.GetOrAdd(messageType, _ => new SemaphoreSlim(0)); // 初始计数为 0

        this._connection.On<TMessage>(methodName, (message) =>
        {
            if (message is DisplayUpdateDto dto)
                this._output.WriteLine(
                    $"[SignalRCollector Callback] Method '{methodName}' received ({messageType.Name}): {dto.Content}");

            // *** 日志结束 ***
            if (this._receivedMessages.TryGetValue(messageType, out var queue))
            {
                queue.Enqueue(message);
                try
                {
                    semaphore.Release(); // 收到消息，增加信号量计数
                }
                catch (ObjectDisposedException)
                {
                    /* 信号量可能已被 Dispose，忽略 */
                }

                Console.WriteLine($"[SignalRCollector] Received: {messageType.Name}"); // 添加日志
            }
        });
        Console.WriteLine($"[SignalRCollector] Registered handler for {messageType.Name} on method '{methodName}'");
    }

    /// <summary>
    /// 异步等待指定类型的下一条消息。
    /// </summary>
    /// <typeparam name="TMessage">期望的消息类型。</typeparam>
    /// <param name="timeout">等待超时时间。</param>
    /// <returns>接收到的消息实例。</returns>
    /// <exception cref="TimeoutException">如果在超时时间内未收到消息。</exception>
    /// <exception cref="InvalidOperationException">如果尚未为该消息类型注册处理器。</exception>
    public async Task<TMessage> WaitForMessageAsync<TMessage>(TimeSpan timeout) where TMessage : class
    {
        return await this.WaitForMessageAsync<TMessage>(_ => true, timeout); // 等待任何该类型的消息
    }

    /// <summary>
    /// 异步等待满足指定条件的下一条特定类型的消息。
    /// </summary>
    /// <typeparam name="TMessage">期望的消息类型。</typeparam>
    /// <param name="predicate">用于筛选消息的条件。</param>
    /// <param name="timeout">等待超时时间。</param>
    /// <returns>接收到的满足条件的消息实例。</returns>
    /// <exception cref="TimeoutException">如果在超时时间内未收到满足条件的消息。</exception>
    /// <exception cref="InvalidOperationException">如果尚未为该消息类型注册处理器。</exception>
    public async Task<TMessage> WaitForMessageAsync<TMessage>(Func<TMessage, bool> predicate, TimeSpan timeout)
        where TMessage : class
    {
        var messageType = typeof(TMessage);
        if (!this._messageSemaphores.TryGetValue(messageType, out var semaphore) ||
            !this._receivedMessages.TryGetValue(messageType, out var queue))
            throw new InvalidOperationException($"消息类型 '{messageType.Name}' 的处理器尚未注册。");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            // 尝试立即获取已排队且满足条件的消息
            if (queue.TryPeek(out object? messageObj) && messageObj is TMessage typedMessage && predicate(typedMessage))
                // 从队列中实际移除
                if (queue.TryDequeue(out _))
                {
                    // 需要消耗掉对应的信号量
                    if (await semaphore.WaitAsync(TimeSpan.Zero)) // 尝试非阻塞等待
                    {
                        Console.WriteLine($"[SignalRCollector] Consumed matching message: {messageType.Name}");
                        return typedMessage;
                    }

                    Console.WriteLine(
                        $"[SignalRCollector] Found matching message but semaphore wait failed (likely already consumed).");
                    // 如果信号量已被消耗（可能在并发检查中），继续循环等待新的信号
                }


            // 计算剩余等待时间
            var remainingTimeout = timeout - stopwatch.Elapsed;
            if (remainingTimeout <= TimeSpan.Zero) break; // 超时

            // 等待信号量（表示有新消息到达）
            Console.WriteLine($"[SignalRCollector] Waiting for semaphore ({messageType.Name})...");
            bool signalReceived = await semaphore.WaitAsync(remainingTimeout);
            Console.WriteLine($"[SignalRCollector] Semaphore wait returned: {signalReceived}");

            if (!signalReceived) break; // 等待超时或被取消

            // 信号量被释放，检查新到达的消息（或之前未匹配的消息）
            if (queue.TryPeek(out messageObj) && messageObj is TMessage newMessage && predicate(newMessage))
            {
                if (queue.TryDequeue(out _))
                {
                    // 这次不需要再等信号量，因为我们刚刚等到了
                    Console.WriteLine($"[SignalRCollector] Consumed matching message after wait: {messageType.Name}");
                    return newMessage;
                }
            }
            else
            {
                // 虽然收到了该类型的信号，但消息不满足条件，或者被其他线程快速取走
                // 释放我们刚刚获取的信号量，以便其他等待者可以检查
                semaphore.Release();
                Console.WriteLine(
                    $"[SignalRCollector] Received signal but message did not match predicate or was gone. Releasing semaphore.");
            }
        }

        stopwatch.Stop();
        throw new TimeoutException(
            $"在 {timeout.TotalSeconds} 秒内未收到满足条件的消息 '{messageType.Name}'。 Queue size: {queue?.Count ?? -1}, Semaphore count: {semaphore?.CurrentCount ?? -1}");
    }

    /// <summary>
    /// 检查在指定时间内是否 *没有* 收到满足条件的特定类型的消息。
    /// </summary>
    /// <typeparam name="TMessage">要检查的消息类型。</typeparam>
    /// <param name="predicate">用于筛选消息的条件。</param>
    /// <param name="timeout">检查的持续时间。</param>
    /// <returns>如果期间没有收到满足条件的消息，则为 true；否则为 false。</returns>
    public async Task<bool> DidNotReceiveMessageAsync<TMessage>(Func<TMessage, bool> predicate, TimeSpan timeout)
        where TMessage : class
    {
        try
        {
            await this.WaitForMessageAsync(predicate, timeout);
            return false; // 如果成功等到消息，则返回 false
        }
        catch (TimeoutException)
        {
            return true; // 如果超时，说明没有收到，返回 true
        }
        catch (InvalidOperationException)
        {
            // 如果处理器未注册，也视为没有收到
            return true;
        }
    }

    /// <summary>
    /// 清空特定类型消息的队列和信号量计数。
    /// </summary>
    /// <typeparam name="TMessage">要清除的消息类型。</typeparam>
    public void ClearMessages<TMessage>() where TMessage : class
    {
        var messageType = typeof(TMessage);
        if (this._receivedMessages.TryGetValue(messageType, out var queue)) queue.Clear();

        if (this._messageSemaphores.TryGetValue(messageType, out var semaphore))
            // 不断尝试获取信号量，直到为0，以清空计数
            while (semaphore.Wait(0))
            {
            }
    }

    /// <summary>
    /// 清空所有已注册类型的消息队列和信号量。
    /// </summary>
    public void ClearAllMessages()
    {
        foreach (var queue in this._receivedMessages.Values) queue.Clear();

        foreach (var semaphore in this._messageSemaphores.Values)
            while (semaphore.Wait(0))
            {
            }
    }
}
// --- END OF FILE SignalRMessageCollector.cs ---