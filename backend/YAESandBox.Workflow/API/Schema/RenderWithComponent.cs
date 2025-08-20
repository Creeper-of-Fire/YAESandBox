using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using YAESandBox.Depend.Schema;

namespace YAESandBox.Workflow.API.Schema;

/// <summary>
/// 一个通用的、可重用的处理器，用于处理那些可以同时应用于类型和属性、
/// 并根据应用位置需要不同 Schema 键的 Attribute。
/// </summary>
/// <param name="typeKey">当 Attribute 应用于类型时，要写入 Schema 的键。</param>
/// <param name="propertyKey">当 Attribute 应用于属性时，要写入 Schema 的键。</param>
/// <param name="valueSelector">一个委托，用于从 Attribute 实例中提取要写入的值。</param>
/// <typeparam name="TAttribute">要处理的 Attribute 类型。</typeparam>
internal class ComponentRendererProcessor<TAttribute>(string typeKey, string propertyKey, Func<TAttribute, string> valueSelector)
    : IYaeSchemaProcessor where TAttribute : Attribute
{
    private readonly string TypeKey = typeKey;
    private readonly string PropertyKey = propertyKey;
    private readonly Func<TAttribute, string> ValueSelector = valueSelector;

    public void Process(JsonSchemaExporterContext context, JsonObject schema)
    {
        // 核心逻辑：属性上的特性优先级高于类型上的特性。

        // 1. 检查属性级别
        var attribute = context.PropertyInfo?.AttributeProvider.GetCustomAttribute<TAttribute>();
        if (attribute != null)
        {
            schema[this.PropertyKey] = this.ValueSelector(attribute);
            // 找到并处理后，直接返回，不再处理类型级别的特性
            return;
        }

        // 2. 如果属性上没有，再检查类型级别
        // 这会处理纯类型上下文，以及属性的类型上带有特性的情况
        var typeAttribute = context.TypeInfo.Type.GetCustomAttribute<TAttribute>();
        if (typeAttribute is not null)
        {
            schema[this.TypeKey] = this.ValueSelector(typeAttribute);
        }
    }
}

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
internal class WebComponentRendererSchemaProcessor() : ComponentRendererProcessor<RenderWithWebComponentAttribute>(
    typeKey: "x-web-component-class",
    propertyKey: "x-web-component-property",
    valueSelector: attr => attr.ComponentTagName
);

/// <summary>
/// 处理 [RenderWithVueComponent] 特性。
/// 根据特性是附着在类还是属性上，为 Schema 添加 'x-vue-component-class' 或 'x-vue-component-property' 指令。
/// </summary>
internal class VueComponentRendererSchemaProcessor() : ComponentRendererProcessor<RenderWithVueComponentAttribute>(
    typeKey: "x-vue-component-class",
    propertyKey: "x-vue-component-property",
    valueSelector: attr => attr.ComponentName
);