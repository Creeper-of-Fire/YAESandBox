using Namotion.Reflection;
using NJsonSchema;
using NJsonSchema.Generation;
using YAESandBox.Depend.Schema.SchemaProcessor;

namespace YAESandBox.Workflow.API.Schema;

/// <summary>
/// 指示该属性在前端应使用插件提供的、预编译的 Vue 组件进行渲染。
/// </summary>
/// <param name="componentName">
/// Vue 组件在插件包中导出的名称。例如，如果插件包导出 { LuaEditor: ..., MarkdownEditor: ... }，
/// 那么这里就应该传入 "LuaEditor"。
/// </param>
[AttributeUsage(AttributeTargets.Property)]
internal class RenderWithVueComponentAttribute(string componentName) : Attribute
{
    /// <summary>
    /// 组件在插件包中导出的名称。
    /// </summary>
    public string ComponentName { get; } = componentName;
}

/// <summary>
/// 指示该属性在前端应使用插件提供的 Web Component 进行渲染。
/// </summary>
/// <param name="componentTagName">
/// 要渲染的 Web Component 的 HTML 标签名。例如 "lua-editor-component"。
/// 脚本在加载后应该通过 customElements.define() 注册这个标签。
/// </param>
[AttributeUsage(AttributeTargets.Property)]
internal class RenderWithWebComponentAttribute(string componentTagName) : Attribute
{
    /// <summary>
    /// Web Component 的 HTML 标签名。
    /// </summary>
    public string ComponentTagName { get; } = componentTagName;
}

/// <summary>
/// 指示该属性在前端应使用 Monaco Editor 进行渲染，并提供语言服务的配置 URL。
/// </summary>
/// <param name="language">要配置的语言 ID，例如 "lua"、"javascript"。</param>
[AttributeUsage(AttributeTargets.Property)]
public class RenderWithMonacoEditorAttribute(string language) : Attribute
{
    /// <summary>
    /// Monaco Editor 中定义的语言 ID。
    /// </summary>
    public string Language { get; } = language;

    /// <summary>
    /// (可选) 指向一个 JS 文件，该文件导出一个 `configure` 函数，用于简单的语言配置。
    /// </summary>
    public string? SimpleConfigUrl { get; set; }
    
    /// <summary>
    /// (可选) 指向语言服务器的 Web Worker 脚本 URL。
    /// 如果提供了此项，将启用完整的 LSP 支持。
    /// </summary>
    public string? LanguageServerWorkerUrl { get; set; }
}

/// <summary>
/// 处理 [RenderWithWebComponent] 特性，为 Schema 添加 'x-web-component' 指令。
/// </summary>
internal class WebComponentRendererSchemaProcessor() : NormalAttributeProcessor<RenderWithWebComponentAttribute>((extension, attribute) =>
    extension["x-web-component"] = attribute.ComponentTagName);

/// <summary>
/// 处理 [RenderWithVueComponent] 特性，为 Schema 添加 'x-vue-component' 指令。
/// </summary>
internal class VueComponentRendererSchemaProcessor() : NormalAttributeProcessor<RenderWithVueComponentAttribute>((extension, attribute) =>
    extension["x-vue-component"] = attribute.ComponentName);

/// <summary>
/// 处理 [RenderWithMonacoEditor] 特性，为 Schema 添加 'x-monaco-editor' 指令。
/// </summary>
internal class MonacoEditorRendererSchemaProcessor() : NormalAttributeProcessor<RenderWithMonacoEditorAttribute>((extension, attribute) =>
{
    // 将配置作为一个对象添加到 schema 中
    extension["x-monaco-editor"] = new
    {
        language = attribute.Language,
        simpleConfigUrl = attribute.SimpleConfigUrl,
        languageServerWorkerUrl = attribute.LanguageServerWorkerUrl
    };
});