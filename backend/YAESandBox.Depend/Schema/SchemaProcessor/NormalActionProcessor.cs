using Namotion.Reflection;
using NJsonSchema;
using NJsonSchema.Generation;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 通用
/// </summary>
public class NormalActionProcessor(Action<SchemaProcessorContext> action) : ISchemaProcessor
{
    /// <inheritdoc/>
    public void Process(SchemaProcessorContext context) => action(context);
}

/// <summary>
/// 通用，检测Attribute并且在<see cref="SchemaProcessorContext.Schema"/> 中的 <see cref="JsonSchema.ExtensionData"/>中添加键
/// </summary>
/// <param name="action">执行的动作</param>
/// <remarks><see cref="AttributeExtensions.GetContextAttribute{T}"/>的inherit设置为了true</remarks>
/// <typeparam name="T">检测的Attribute的类型</typeparam>
public class NormalAttributeProcessor<T>(Action<SchemaProcessorContext, T> action) : NormalActionProcessor(context =>
{
    var attribute = context.ContextualType.GetContextAttribute<T>(true);
    if (attribute == null) return;
    action(context, attribute);
})
    where T : Attribute
{

    /// <inheritdoc cref="NormalAttributeProcessor{T}"/>
    /// <param name="extensionKey">新建的键名</param>
    /// <param name="setExtensionValue">通过Attribute的参数配置<see cref="JsonSchema.ExtensionData"/>中对应<paramref name="extensionKey"/>的值</param>
    internal NormalAttributeProcessor(string extensionKey, Func<T, object> setExtensionValue) : this((context, attribute) =>
    {
        context.Schema.ExtensionData ??= new Dictionary<string, object?>();
        context.Schema.ExtensionData[extensionKey] = setExtensionValue(attribute);
    }) { }

    /// <inheritdoc cref="NormalAttributeProcessor{T}"/>
    /// <param name="action">检测Attribute并且在<see cref="SchemaProcessorContext.Schema"/> 中的 <see cref="JsonSchema.ExtensionData"/>中添加键</param>
    internal NormalAttributeProcessor(Action<IDictionary<string, object?>, T> action) : this((context, attribute) =>
    {
        context.Schema.ExtensionData ??= new Dictionary<string, object?>();
        action(context.Schema.ExtensionData, attribute);
    }) { }
}