namespace YAESandBox.Core.State;

/// <summary>
/// 存储与特定 Block 相关的游戏状态设置。
/// 目前是一个简单的键值字典，支持克隆。
/// </summary>
public class GameState
{
    // 使用 ConcurrentDictionary 可以在需要时支持多线程访问，虽然目前 Block 操作是单线程的
    // 但如果未来 GameState 可能被其他并行进程读取，它是更安全的选择。
    // 如果确定是单线程访问，普通的 Dictionary<string, object?> 也可以。
    private readonly Dictionary<string, object?> _settings = new(StringComparer.OrdinalIgnoreCase); // 忽略键的大小写

    /// <summary>
    /// 获取或设置游戏状态值。
    /// </summary>
    /// <param name="key">设置的键。</param>
    /// <returns>设置的值，如果键不存在则返回 null。</returns>
    public object? this[string key]
    {
        get => this._settings.GetValueOrDefault(key);
        set => this._settings[key] = value;
    }

    /// <summary>
    /// 尝试获取指定类型的游戏状态值。
    /// </summary>
    /// <typeparam name="T">期望的值类型。</typeparam>
    /// <param name="key">设置的键。</param>
    /// <param name="value">如果成功获取并转换，则输出值。</param>
    /// <returns>如果键存在且类型匹配，则为 true；否则为 false。</returns>
    public bool TryGetValue<T>(string key, out T? value)
    {
        if (this._settings.TryGetValue(key, out object? rawValue) && rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// 移除一个设置项。
    /// </summary>
    /// <param name="key">要移除的键。</param>
    /// <returns>如果成功移除则为 true，否则为 false。</returns>
    public bool Remove(string key)
    {
        return this._settings.Remove(key);
    }

    /// <summary>
    /// 获取所有设置项的只读字典副本。
    /// </summary>
    public IReadOnlyDictionary<string, object?> GetAllSettings()
    {
        // 返回一个副本以防止外部修改内部字典
        return new Dictionary<string, object?>(this._settings, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 创建 GameState 的浅拷贝副本。
    /// 注意：如果设置值是引用类型，副本将共享引用。
    /// 如果需要深拷贝，需要实现更复杂的逻辑。
    /// </summary>
    /// <returns>一个新的 GameState 实例，包含当前设置的副本。</returns>
    public GameState Clone()
    {
        var clone = new GameState();
        // 简单地拷贝字典内容（值是浅拷贝）
        foreach (var kvp in this._settings)
        {
            clone._settings[kvp.Key] = kvp.Value; // 浅拷贝
        }
        return clone;
    }
}