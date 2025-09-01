using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using Microsoft.Extensions.Logging;
using YAESandBox.Depend;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow.API.Schema;

/// <summary>
/// 指示该属性在前端应使用 Monaco Editor 进行渲染，并提供语言服务的配置 URL。
/// </summary>
/// <remarks>
/// <para><b>插件内部资源路径解析:</b></para>
/// <para>
/// 对于需要引用插件包内文件的 `SimpleConfigUrl` 和 `LanguageServerWorkerUrl` 属性，
/// 请使用我们约定的 `plugin://` 协议。
/// </para>
/// <para>
/// 例如: `[RenderWithMonacoEditor("lua", SimpleConfigUrl = "plugin://monaco-lua-service.js")]`
/// </para>
/// <para>
/// 在运行时，系统会自动将 `plugin://` 前缀的路径解析为完整的、可公开访问的 URL。
/// 它会查找定义此特性的符文配置类所在的插件，并构建出类似 `/plugins/{PluginId}/monaco-lua-service.js` 的最终路径。
/// 这使得路径声明与插件的具体名称解耦，更加健壮和可移植。
/// </para>
/// <para>
/// 对于外部或固定的 URL，请直接提供完整的路径，不要使用 `plugin://` 协议。
/// </para>
/// </remarks>
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
    /// <para>支持使用 `plugin://` 协议来引用插件内部的资源。</para>
    /// <example>"plugin://my-config.js"</example>
    /// </summary>
    public string? SimpleConfigUrl { get; set; }

    /// <summary>
    /// (可选) 指向语言服务器的 Web Worker 脚本 URL。
    /// 如果提供了此项，将启用完整的 LSP 支持。
    /// <para>支持使用 `plugin://` 协议来引用插件内部的资源。</para>
    /// <example>"plugin://language-servers/lua-worker.js"</example>
    /// </summary>
    public string? LanguageServerWorkerUrl { get; set; }
}

/// <summary>
/// 处理 [RenderWithMonacoEditor] 特性，为 Schema 添加 'x-monaco-editor' 指令，
/// 并能动态解析插件内部的资源路径。
/// </summary>
internal class MonacoEditorRendererSchemaProcessor : YaePropertyAttributeProcessor<RenderWithMonacoEditorAttribute>
{
    private const string PluginScheme = "plugin://";
    private static ILogger Logger { get; } = AppLogging.CreateLogger<MonacoEditorRendererSchemaProcessor>();

    /// <summary>
    /// 解析包含 plugin:// 协议的 URL。
    /// </summary>
    private string? ResolvePluginUrl(string? templateUrl, Type ownerType)
    {
        if (string.IsNullOrEmpty(templateUrl))
            return null;

        // 如果 URL 不是以 plugin:// 开头，直接返回原样
        if (!templateUrl.StartsWith(PluginScheme, StringComparison.OrdinalIgnoreCase))
        {
            return templateUrl;
        }

        // 提取相对路径，例如 "monaco-lua-service.js"
        string relativePath = templateUrl[PluginScheme.Length..];

        // 1. 查找此类型是由哪个模块提供的
        var providerModule = RuneConfigTypeResolver.GetProviderModuleForType(ownerType);
        if (providerModule is not null)
            return $"{providerModule.ToRequestPath()}/{relativePath}";

        // 如果找不到提供者，则无法解析路径，记录警告并返回 null
        Logger.LogWarning("[SchemaProcessor] 警告: 无法解析路径 '{TemplateUrl}'，因为类型 '{OwnerTypeName}' 没有找到对应的插件提供者。", templateUrl, ownerType.Name);
        return null;
    }

    /// <inheritdoc />
    protected override void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema,
        RenderWithMonacoEditorAttribute attribute)
    {
        // 通常 context.ContextualType.Type 是属性对应的类型，并不能作为模块类型
        var ownerType = context.PropertyInfo?.DeclaringType ?? context.TypeInfo.Type;

        // 解析 URL
        string? resolvedSimpleConfigUrl = this.ResolvePluginUrl(attribute.SimpleConfigUrl, ownerType);
        string? resolvedWorkerUrl = this.ResolvePluginUrl(attribute.LanguageServerWorkerUrl, ownerType);

        schema["x-monaco-editor"] = new JsonObject
        {
            ["language"] = attribute.Language,
            ["simpleConfigUrl"] = resolvedSimpleConfigUrl,
            ["languageServerWorkerUrl"] = resolvedWorkerUrl,
        };
    }
}