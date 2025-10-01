using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace YAESandBox.Depend.Storage;

/// <summary>
/// 一个通用包装器，用于在序列化时明确区分“值为null”和“没有值”（即数据损坏或丢失）两种状态。
/// <para>
/// 核心思想：
/// <list type="bullet">
///   <item><description><b>成功状态:</b> JSON 为 <c>{"value":...}</c>。即使内部值为 <c>null</c> (即 <c>{"value":null}</c>)，也表示一个有效的、已成功传输的 <c>null</c> 值。</description></item>
///   <item><description><b>失败状态:</b> JSON 为 <c>"null"</c> 或格式非法。这被视为数据传输/存储完整性问题，解析时会通过抛出异常或返回 <c>false</c> 来明确表示失败。</description></item>
/// </list>
/// </para>
/// </summary>
/// <typeparam name="TValue">被包装的值的类型。可以是引用类型、值类型，可空性任意。</typeparam>
/// <param name="Value">被包装的实际值。</param>
public partial record ValueWrapper<TValue>(TValue Value)
{
    /// <summary>
    /// 创建一个新的 <see cref="ValueWrapper{TValue}"/> 实例来包装一个值。
    /// </summary>
    /// <param name="value">要包装的值。</param>
    /// <returns>一个包含所提供值的新 <see cref="ValueWrapper{TValue}"/> 实例。</returns>
    public static ValueWrapper<TValue> Warp(TValue value) => new(value);
}

public partial record ValueWrapper<TValue>
{
    /// <summary>
    /// 解析一个被 <see cref="ValueWrapper{TValue}"/> 包装的 JSON 字符串，并返回内部的值。
    /// </summary>
    /// <param name="json">要解析的 JSON 字符串。</param>
    /// <param name="options">可选的序列化配置。如果为 null，则使用 <see cref="YaeSandBoxJsonHelper.JsonSerializerOptions"/>。</param>
    /// <returns>从包装器中提取出的原始值。如果原始 JSON 是 <c>{"value":null}</c>，则此返回值就是 <c>null</c>。</returns>
    /// <exception cref="JsonException">如果 JSON 字符串为 "null" 或无法被解析为 <see cref="ValueWrapper{TValue}"/> 对象，表明数据已损坏。</exception>
    public static TValue ParseValue([StringSyntax("Json")] string json, JsonSerializerOptions? options = null)
    {
        var resolvedOptions = options ?? YaeSandBoxJsonHelper.JsonSerializerOptions;
        var wrapper = JsonSerializer.Deserialize<ValueWrapper<TValue>>(json, resolvedOptions);

        // 如果包装器本身是 null，说明 JSON 字符串是 "null" 或格式严重错误，这是数据损坏的信号。
        if (wrapper is null)
        {
            throw new JsonException($"解析失败：期望得到 '{typeof(ValueWrapper<TValue>).FullName}' 的实例，但结果为 null。原始 JSON 可能为 'null' 或格式错误。");
        }

        return wrapper.Value;
    }

    /// <summary>
    /// 尝试解析一个被 <see cref="ValueWrapper{TValue}"/> 包装的 JSON 字符串，并获取内部的值。
    /// </summary>
    /// <param name="json">要解析的 JSON 字符串。</param>
    /// <param name="value">当方法返回 <c>true</c> 时，此参数包含从包装器中提取出的值，其可空性完全取决于<typeparamref name="TValue"/>本身是否可空。
    /// 当方法返回 <c>false</c> 时，此参数为 <c>default</c>。</param>
    /// <param name="options">可选的序列化配置。如果为 null，则使用 <see cref="YaeSandBoxJsonHelper.JsonSerializerOptions"/>。</param>
    /// <returns>如果成功解析出包装器（即使内部值为null），则返回 <c>true</c>；否则（如果JSON为"null"或格式错误），返回 <c>false</c>。</returns>
    /// <exception cref="JsonException">会抛出解析错误。</exception>
    public static bool TryParseValue([StringSyntax("Json")] string json, [MaybeNullWhen(false)] out TValue value,
        JsonSerializerOptions? options = null)
    {
        var resolvedOptions = options ?? YaeSandBoxJsonHelper.JsonSerializerOptions;
        var wrapper = JsonSerializer.Deserialize<ValueWrapper<TValue>>(json, resolvedOptions);

        if (wrapper is null)
        {
            value = default;
            return false;
        }

        value = wrapper.Value;
        return true;
    }
}

/// <summary>
/// 提供非泛型静态方法来创建、解析和序列化 <see cref="ValueWrapper{TValue}"/> 实例。
/// </summary>
public static class ValueWrapper
{
    /// <inheritdoc cref="ValueWrapper{TValue}.Warp"/>
    public static ValueWrapper<TValue> Warp<TValue>(TValue value) => new(value);

    /// <inheritdoc cref="ValueWrapper{TValue}.ParseValue"/>
    public static TValue ParseValue<TValue>(
        [StringSyntax("Json")] string json,
        JsonSerializerOptions? options = null
    ) => ValueWrapper<TValue>.ParseValue(json, options);
    
    /// <inheritdoc cref="ValueWrapper{TValue}.ParseValue"/>
    public static T Deserialize<T>(
        [StringSyntax("Json")] string json,
        JsonSerializerOptions? options = null
        ) => ValueWrapper<T>.ParseValue(json, options);

    /// <inheritdoc cref="ValueWrapper{TValue}.ParseValue"/>
    public static bool TryParseValue<TValue>(
        [StringSyntax("Json")] string json,
        [MaybeNullWhen(false)] out TValue value,
        JsonSerializerOptions? options = null
    ) => ValueWrapper<TValue>.TryParseValue(json, out value, options);

    /// <summary>
    /// 将一个值包装并序列化为 JSON 字符串。
    /// <para>这能确保 'null' 值被显式表示为 <c>{"value":null}</c>，而不是 "null"，从而保留其有效性。</para>
    /// </summary>
    /// <param name="value">要包装和序列化的值。</param>
    /// <param name="options">可选的序列化配置。如果为 null，则使用 <see cref="YaeSandBoxJsonHelper.JsonSerializerOptions"/>。</param>
    /// <returns>包装并序列化后的 JSON 字符串。</returns>
    public static string ToJsonString<TValue>(TValue value, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(Warp(value), options ?? YaeSandBoxJsonHelper.JsonSerializerOptions);
    }

    /// <summary>
    /// 将一个对象包装并序列化为 JSON 字符串。
    /// </summary>
    /// <remarks>这是 <see cref="ToJsonString{TValue}"/> 的一个别名，旨在提供更符合常规序列化方法的命名。</remarks>
    /// <typeparam name="T">要包装和序列化的对象的类型。</typeparam>
    /// <param name="tObject">要包装和序列化的对象。</param>
    /// <param name="options">可选的序列化配置。如果为 null，则使用 <see cref="YaeSandBoxJsonHelper.JsonSerializerOptions"/>。</param>
    /// <returns>包装并序列化后的 JSON 字符串。</returns>
    public static string SerializeAsWrapper<T>(T tObject, JsonSerializerOptions? options = null) => ToJsonString(tObject, options);

    /// <summary>
    /// 序列化一个 <see cref="ValueWrapper{TValue}"/> 实例。
    /// </summary>
    /// <param name="valueWrapper">要序列化的包装器实例。</param>
    /// <param name="options">可选的序列化配置。如果为 null，则使用 <see cref="YaeSandBoxJsonHelper.JsonSerializerOptions"/>。</param>
    /// <returns>包装器的 JSON 字符串表示。</returns>
    public static string Serialize<T>(this ValueWrapper<T> valueWrapper, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(valueWrapper, options ?? YaeSandBoxJsonHelper.JsonSerializerOptions);
    }

    /// <summary>
    /// 从 <see cref="ValueWrapper{TValue}"/> 实例中解包出内部的值。
    /// </summary>
    /// <typeparam name="T">内部值的类型。</typeparam>
    /// <param name="valueWrapper">要解包的包装器实例。</param>
    /// <returns>包装器内部的原始值。</returns>
    public static T UnWarp<T>(this ValueWrapper<T> valueWrapper)
    {
        return valueWrapper.Value;
    }

    /// <summary>
    /// 从 <see cref="ValueWrapper{TValue}"/> 实例中获取内部的值。
    /// </summary>
    /// <typeparam name="T">内部值的类型。</typeparam>
    /// <param name="valueWrapper">要解包的包装器实例。</param>
    /// <returns>包装器内部的原始值。</returns>
    public static T GetValue<T>(this ValueWrapper<T> valueWrapper)
    {
        return valueWrapper.Value;
    }

    /// <summary>
    /// 从 <see cref="ValueWrapper{TValue}"/> 实例中解包（反序列化）出内部的值。
    /// </summary>
    /// <remarks>
    /// 尽管方法名暗示了反序列化，但此操作只是简单的属性访问，不会进行任何复杂的转换，因此不会像真正的 JSON 反序列化那样抛出 <see cref="JsonException"/>。
    /// </remarks>
    /// <typeparam name="T">内部值的类型。</typeparam>
    /// <param name="valueWrapper">要解包的包装器实例。</param>
    /// <returns>包装器内部的原始值。</returns>
    public static T Deserialize<T>(this ValueWrapper<T> valueWrapper)
    {
        return valueWrapper.Value;
    }
}