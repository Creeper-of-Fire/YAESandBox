using System.Text.Json.Nodes;

namespace YAESandBox.Depend.Schema;

/// <summary>
/// 提供将 IEnumerable&lt;T&gt; 转换为 JsonArray 的扩展方法。
/// </summary>
public static class JsonArrayExtensions
{
    /// <summary>
    /// 从一个对象集合创建一个 JsonArray，通过一个选择器函数从每个对象中提取值。
    /// </summary>
    /// <typeparam name="TSource">源集合中的对象类型。</typeparam>
    /// <param name="source">要转换的源集合。</param>
    /// <param name="valueSelector">一个函数，用于从每个源对象中提取要转换为 JsonValue 的值。</param>
    /// <returns>一个新的 JsonArray，包含从源集合中提取并转换的值。</returns>
    /// <example>
    ///   var types = new Type[] { typeof(string), typeof(int) };
    ///   var jsonArray = types.ToJsonArray(t => t.Name); // -> ["String", "Int32"]
    /// </example>
    public static JsonArray ToJsonArray<TSource>(this IEnumerable<TSource>? source, Func<TSource, JsonNode?> valueSelector)
    {
        if (source is null)
            return [];

        return new JsonArray(source.Select(valueSelector).ToArray());
    }

    /// <summary>
    /// 从一个值集合（如 string, int, bool 等）创建一个 JsonArray。
    /// </summary>
    /// <typeparam name="TValue">源集合中的值的类型。该类型必须能被 JsonValue.Create 支持。</typeparam>
    /// <param name="source">要转换的源值集合。</param>
    /// <returns>一个新的 JsonArray，包含源集合中的值。</returns>
    /// <example>
    ///   var names = new string[] { "Alice", "Bob" };
    ///   var jsonArray = names.ToJsonArray(); // -> ["Alice", "Bob"]
    /// </example>
    public static JsonArray ToJsonArray<TValue>(this IEnumerable<TValue>? source)
    {
        if (source is null)
            return [];

        // JsonValue.Create 有泛型重载，可以直接处理
        return new JsonArray(source.Select(it => JsonValue.Create(it)).ToArray<JsonNode?>());
    }
}