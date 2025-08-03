using System.Reflection;
using Namotion.Reflection;
using NJsonSchema.Generation;
using YAESandBox.Depend.Schema.SchemaProcessor;

namespace YAESandBox.Workflow.API.Schema;

/// <summary>
/// 指示该属性/类型在前端应使用插件提供的、预编译的 Vue 组件进行渲染。
/// </summary>
/// <param name="componentName">
/// Vue 组件在插件包中导出的名称。例如，如果插件包导出 { LuaEditor: ..., MarkdownEditor: ... }，
/// 那么这里就应该传入 "LuaEditor"。
/// </param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public class RenderWithVueComponentAttribute(string componentName) : Attribute
{
    /// <summary>
    /// 组件在插件包中导出的名称。
    /// </summary>
    public string ComponentName { get; } = componentName;
}

/// <summary>
/// 指示该属性/类型在前端应使用插件提供的 Web Component 进行渲染。
/// </summary>
/// <param name="componentTagName">
/// 要渲染的 Web Component 的 HTML 标签名。例如 "lua-editor-component"。
/// 脚本在加载后应该通过 customElements.define() 注册这个标签。
/// </param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public class RenderWithWebComponentAttribute(string componentTagName) : Attribute
{
    /// <summary>
    /// Web Component 的 HTML 标签名。
    /// </summary>
    public string ComponentTagName { get; } = componentTagName;
}

/// <summary>
/// 处理 [RenderWithWebComponent] 特性。
/// 根据特性是附着在类还是属性上，为 Schema 添加 'x-web-component-class' 或 'x-web-component-property' 指令。
/// </summary>
internal class WebComponentRendererSchemaProcessor : ISchemaProcessor
{
    /// <inheritdoc />
    public void Process(SchemaProcessorContext context)
    {
        var attribute = context.ContextualType.GetContextAttribute<RenderWithWebComponentAttribute>(true);
        if (attribute is null)
            return;

        string extensionKey = context.ContextualType.Context switch
        {
            // 判断特性附着在类上还是属性上
            PropertyInfo => "x-web-component-property",
            TypeInfo => "x-web-component-class",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(extensionKey))
            return;

        context.Schema.ExtensionData ??= new Dictionary<string, object?>();
        context.Schema.ExtensionData[extensionKey] = attribute.ComponentTagName;
    }
}

/// <summary>
/// 处理 [RenderWithVueComponent] 特性。
/// 根据特性是附着在类还是属性上，为 Schema 添加 'x-vue-component-class' 或 'x-vue-component-property' 指令。
/// </summary>
internal class VueComponentRendererSchemaProcessor : ISchemaProcessor
{
    /// <inheritdoc />
    public void Process(SchemaProcessorContext context)
    {
        var attribute = context.ContextualType.GetContextAttribute<RenderWithVueComponentAttribute>(true);
        if (attribute is null)
            return;

        string extensionKey = context.ContextualType.Context switch
        {
            // 判断特性附着在类上还是属性上
            PropertyInfo => "x-vue-component-property",
            TypeInfo => "x-vue-component-class",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(extensionKey))
            return;

        context.Schema.ExtensionData ??= new Dictionary<string, object?>();
        context.Schema.ExtensionData[extensionKey] = attribute.ComponentName;
    }
}