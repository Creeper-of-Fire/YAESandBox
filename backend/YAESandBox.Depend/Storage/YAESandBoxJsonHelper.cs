using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Results;

namespace YAESandBox.Depend.Storage;

/// <summary>
/// 项目通用的JsonHelper
/// </summary>
public static class YaeSandBoxJsonHelper
{
    /// <summary>
    /// 项目通用的JsonSerializerOptions
    /// </summary>
    public static JsonSerializerOptions JsonSerializerOptions { get; set; } = new()
    {
        WriteIndented = true, // 格式化输出
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // 忽略 null 值属性
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 允许不安全的字符编码 (如中文不转码)
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// 把 <paramref name="newOptions"/> 的参数完全赋值给 <paramref name="oldOptions"/>
    /// </summary>
    /// <param name="oldOptions"></param>
    /// <param name="newOptions"></param>
    public static void CopyFrom(JsonSerializerOptions oldOptions, JsonSerializerOptions newOptions)
    {
        ArgumentNullException.ThrowIfNull(oldOptions);
        ArgumentNullException.ThrowIfNull(newOptions);

        var properties = typeof(JsonSerializerOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            // 1. 跳过集合属性，它们将单独处理
            if (prop.Name == nameof(JsonSerializerOptions.Converters) ||
                prop.Name == nameof(JsonSerializerOptions.TypeInfoResolverChain))
            {
                continue;
            }

            // 2. 确保属性是可写的
            if (!prop.CanWrite)
            {
                continue;
            }

            object? oldValue = prop.GetValue(oldOptions);
            object? newValue = prop.GetValue(newOptions);
            var propertyType = prop.PropertyType;

            // 3. 检查 oldOptions 的属性值是否为其类型的默认值
            //    "如果有值了 (即不是默认值)，那就不复制"
            bool isOldValueDefault;
            if (propertyType.IsValueType)
            {
                // 对于值类型，与 Activator.CreateInstance(propertyType) 比较
                // 注意：对于枚举，默认值是其基础类型的0。
                // 对于结构体，是其所有字段的默认值。
                isOldValueDefault = Equals(oldValue, Activator.CreateInstance(propertyType));
            }
            else
            {
                // 对于引用类型，默认值是 null
                isOldValueDefault = oldValue == null;
            }

            // 4. 如果 oldOptions 的属性值是默认值，那么我们才从 newOptions 复制
            //    这意味着如果 oldOptions.Property 已经被设置为非默认值，它将被保留。
            if (isOldValueDefault)
            {
                try
                {
                    prop.SetValue(oldOptions, newValue);
                }
                catch (Exception ex)
                {
                    // 可以选择记录日志，某些属性即使 CanWrite 返回 true，也可能由于内部状态而不允许修改
                    // 例如，在 JsonSerializerOptions 被标记为只读后。
                    // 但在 MVC AddJsonOptions 配置期间，通常是可写的。
                    Console.WriteLine($"警告：复制属性 {prop.Name} 时出错: {ex.Message}");
                }
            }
        }

        // 5. 处理 Converters 集合
        foreach (var converter in newOptions.Converters)
        {
            // 可以考虑检查是否已存在，但 Clear 后就不需要了
            oldOptions.Converters.Add(converter);
        }

        // 6. 处理 TypeInfoResolverChain 集合
        foreach (var resolver in newOptions.TypeInfoResolverChain)
        {
            oldOptions.TypeInfoResolverChain.Add(resolver);
        }
    }

    /// <summary>
    /// 将对象实例通过反射转换为字典，优先使用 JsonPropertyNameAttribute 指定的名称。
    /// </summary>
    /// <param name="obj">要转换的对象实例。</param>
    /// <returns>一个包含属性名（或JsonPropertyName）和对应值的字典。如果输入对象为null，则返回一个空字典。</returns>
    public static Dictionary<string, object?> ToDictionaryWithJsonPropertyNames(object obj)
    {
        var dictionary = new Dictionary<string, object?>();

        var type = obj.GetType();
        // 获取所有公共实例属性
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties.Where(property => property.CanRead))
        {
            string key;
            // 尝试获取 JsonPropertyNameAttribute
            var jsonPropertyNameAttribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

            if (jsonPropertyNameAttribute != null && !string.IsNullOrEmpty(jsonPropertyNameAttribute.Name))
            {
                // 如果特性存在且其Name属性有值，则使用它作为键
                key = jsonPropertyNameAttribute.Name;
            }
            else
            {
                // 否则，回退到使用属性本身的名称
                // 根据你的描述，你已经将属性名小写了，所以这里直接用 property.Name 即可
                // 如果原始属性名是PascalCase，而你希望在没有特性时使用camelCase，可以进行转换：
                // key = char.ToLowerInvariant(property.Name[0]) + property.Name.Substring(1);
                // 但你的情况是属性名已经小写了。
                key = property.Name;
            }

            object? value = property.GetValue(obj);

            var jsonExtensionDataAttribute = property.GetCustomAttribute<JsonExtensionDataAttribute>();
            if (jsonExtensionDataAttribute != null)
            {
                switch (value)
                {
                    case IDictionary<string, object?> extensionData:
                    {
                        foreach (var kv in extensionData)
                            dictionary[kv.Key] = kv.Value;
                        continue;
                    }
                    case IDictionary<string, JsonNode> extensionData:
                    {
                        foreach (var kv in extensionData)
                            dictionary[kv.Key] = kv.Value;
                        continue;
                    }
                }
            }

            dictionary[key] = value; // 使用索引器赋值，如果key重复会覆盖（对于属性通常不会）
        }

        return dictionary;
    }

    /// <summary>
    /// 创建一个新的目标类型的实例，并仅从源对象复制带有 [RequiredAttribute] 标记的属性值。
    /// 此版本适用于源和目标类型在运行时确定。
    /// </summary>
    /// <param name="source">源对象。</param>
    /// <param name="targetType">要创建的目标对象的类型。</param>
    /// <returns>一个新的目标类型实例，只包含源对象中标记为 [Required] 的属性值。如果源对象为 null 或无法创建目标实例，则返回 null。</returns>
    public static object? CreateObjectWithRequiredPropertiesOnly(object? source, Type targetType)
    {
        if (source == null)
            return null;

        // 确保 targetType 可以被实例化 (例如，有无参数构造函数)
        object? target;
        try
        {
            target = Activator.CreateInstance(targetType);
            if (target == null)
                return null;
        }
        catch (Exception)
        {
            return null;
        }

        var sourceType = source.GetType(); // 获取源对象的实际运行时类型
        var sourceProperties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var sourceProperty in sourceProperties)
        {
            // 检查属性是否标记了 [RequiredAttribute]
            if (!sourceProperty.IsDefined(typeof(RequiredAttribute), inherit: true))
                continue; // inherit: true 表示也检查基类中的特性
            // 尝试在目标对象上找到同名且类型兼容的属性
            var targetProperty = targetType.GetProperty(
                sourceProperty.Name,
                BindingFlags.Public | BindingFlags.Instance,
                null, // binder
                sourceProperty.PropertyType, // 属性类型必须匹配
                Type.EmptyTypes, // 无索引参数
                null // modifiers
            );

            // 如果找到了对应的目标属性，并且它可以被写入
            if (targetProperty == null || !targetProperty.CanWrite) continue;
            try
            {
                object? value = sourceProperty.GetValue(source); // 从源对象获取值
                targetProperty.SetValue(target, value); // 设置到目标对象
            }
            catch (Exception)
            {
                // ReSharper disable once RedundantJumpStatement
                continue;
            }
        }

        return target;
    }

    /// <summary>
    /// 尝试克隆当前的 <see cref="JsonNode"/> 实例。
    /// </summary>
    /// <param name="document">要克隆的原始 JSON 文档。</param>
    /// <param name="clonedDocument">
    /// 当此方法返回时，如果克隆成功，则包含一个新的 <see cref="JsonNode"/> 实例；
    /// 否则为 <c>null</c>。
    /// </param>
    /// <returns>
    /// 如果克隆成功，则为 <c>true</c>；否则为 <c>false</c>。
    /// </returns>
    /// <remarks>
    /// 此方法通过解析原始文档的原始 JSON 文本创建一个新的 <see cref="JsonNode"/>，
    /// 因此适用于需要独立副本以避免修改原始内容的场景。
    /// 如果解析失败（如 JSON 格式错误），将捕获异常并返回 <c>false</c>。
    /// </remarks>
    public static bool CloneJsonNode(this JsonNode document, [NotNullWhen(true)] out JsonNode? clonedDocument)
    {
        try
        {
            clonedDocument = JsonNode.Parse(document.ToJsonString());
            if (clonedDocument != null)
                return true;
        }
        catch
        {
            clonedDocument = null;
        }

        return false;
    }
    
    /// <summary>
    /// 通过JSON序列化和反序列化创建一个对象的深拷贝。
    /// 这是一种简单而有效的创建对象独立副本的方法，适用于可序列化的POCO对象。
    /// </summary>
    /// <typeparam name="T">要克隆的对象的类型。</typeparam>
    /// <param name="source">源对象。</param>
    /// <returns>源对象的一个新的深拷贝实例，如果源为null则返回null。</returns>
    public static Result<T> DeepClonePoco<T>(T source)
    {
        try
        {
            // 1. 将源对象序列化为JSON字符串。
            string json = JsonSerializer.Serialize(source, JsonSerializerOptions);

            // 2. 将JSON字符串反序列化为一个新的对象。
            var clone = JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);

            // 如果克隆结果为null（例如，对象被序列化为"null"），这是一个意外的失败。
            if (clone is null)
            {
                return Result.Fail("深拷贝失败：反序列化后的结果为 null，这是一个非预期的状态。");
            }

            return Result.Ok<T>(clone);
        }
        catch (Exception ex) // 捕获所有可能的异常，如 JsonException, NotSupportedException 等
        {
            return Result.Fail($"深拷贝失败：在序列化或反序列化过程中发生错误。详情: {ex.Message}");
        }
    }
}