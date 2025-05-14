using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace YAESandBox.Depend.Storage;

/// <summary>
/// 项目通用的JsonHelper
/// </summary>
public static class YAESandBoxJsonHelper
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
    /// 把 <param name="newOptions"/> 的参数完全赋值给 <param name="oldOptions"/>
    /// </summary>
    /// <param name="oldOptions"></param>
    /// <param name="newOptions"></param>
    public static void CopyFrom(JsonSerializerOptions oldOptions, JsonSerializerOptions newOptions)
    {
        // oldOptions.PropertyNamingPolicy = newOptions.PropertyNamingPolicy;
        // oldOptions.AllowOutOfOrderMetadataProperties = newOptions.AllowOutOfOrderMetadataProperties;
        // oldOptions.AllowTrailingCommas = newOptions.AllowTrailingCommas;
        // oldOptions.DefaultBufferSize = newOptions.DefaultBufferSize;
        // oldOptions.DefaultIgnoreCondition = newOptions.DefaultIgnoreCondition;
        // oldOptions.DictionaryKeyPolicy = newOptions.DictionaryKeyPolicy;
        // oldOptions.Encoder = newOptions.Encoder;
        // oldOptions.IgnoreReadOnlyFields = newOptions.IgnoreReadOnlyFields;
        // oldOptions.IgnoreReadOnlyProperties = newOptions.IgnoreReadOnlyProperties;
        // oldOptions.IncludeFields = newOptions.IncludeFields;
        // oldOptions.IndentCharacter = newOptions.IndentCharacter;
        // oldOptions.IndentSize = newOptions.IndentSize;
        // oldOptions.MaxDepth = newOptions.MaxDepth;
        // oldOptions.NewLine = newOptions.NewLine;
        // oldOptions.NumberHandling = newOptions.NumberHandling;
        // oldOptions.PreferredObjectCreationHandling = newOptions.PreferredObjectCreationHandling;
        // oldOptions.PropertyNameCaseInsensitive = newOptions.PropertyNameCaseInsensitive;
        // oldOptions.ReadCommentHandling = newOptions.ReadCommentHandling;
        // oldOptions.ReferenceHandler = newOptions.ReferenceHandler;
        // oldOptions.RespectNullableAnnotations = newOptions.RespectNullableAnnotations;
        // oldOptions.RespectRequiredConstructorParameters = newOptions.RespectRequiredConstructorParameters;
        // oldOptions.TypeInfoResolver = newOptions.TypeInfoResolver;
        // newOptions.TypeInfoResolverChain.ToList().ForEach(item =>
        // {
        //     if (!oldOptions.TypeInfoResolverChain.Contains(item)) // 避免重复添加
        //         oldOptions.TypeInfoResolverChain.Add(item);
        // });
        // newOptions.Converters.ToList().ForEach(item =>
        // {
        //     if (!oldOptions.Converters.Contains(item)) // 避免重复添加
        //         oldOptions.Converters.Add(item);
        // });
        // oldOptions.UnknownTypeHandling = newOptions.UnknownTypeHandling;
        // oldOptions.UnmappedMemberHandling = newOptions.UnmappedMemberHandling;
        // oldOptions.WriteIndented = newOptions.WriteIndented;

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
            Type propertyType = prop.PropertyType;

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
                isOldValueDefault = (oldValue == null);
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
}