namespace YAESandBox.Workflow.Runtime.RuntimePersistence;

/// <summary>
/// 表示在持久化作用域 (PersistenceScope) 上执行了无效操作。
/// 例如，在缓存命中时尝试访问执行结果，或在需要执行时访问缓存结果。
/// </summary>
/// <param name="message">描述错误的详细信息。</param>
public class InvalidScopeOperationException(string message) : InvalidOperationException(message);