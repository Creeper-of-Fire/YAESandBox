using System.ComponentModel;
using System.Resources;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using YAESandBox.Depend.Schema.SchemaProcessor.Abstract;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 用于为属性提供字符串选项列表的特性，支持静态定义、动态获取以及可编辑下拉框行为。
/// 可用于前端生成下拉选择框、可编辑组合框等 UI 元素。
/// </summary>
/// <remarks>
/// 支持以下功能：
/// - 静态选项定义（Value/Label 对）
/// - 动态选项加载（通过 API 端点）
/// - 是否允许用户输入自定义值（combobox 行为）
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public class StringOptionsAttribute() : Attribute
{
    /// <summary>
    /// 静态定义的选项列表。
    /// </summary>
    public (string Value, string Label)[] Options { get; } = [];

    /// <summary>
    /// 如果提供，表示该字段的选项可以从这个API端点动态获取。
    /// 前端可以调用此端点来刷新或获取选项列表。
    /// 动态获取的选项通常会与静态定义的 Options 合并或替换（行为由前端或帮助类决定）。
    /// </summary>
    public string? OptionsProviderEndpoint { get; set; }

    /// <summary>
    /// 是否允许用户输入不在建议选项列表中的自定义值 (可编辑下拉框)。
    /// 默认为 false，表示标准的固定选项下拉框。
    /// 当与 optionsProviderEndpoint 一起使用时，或即使 Options 为空但此值为 true，
    /// 通常暗示前端应渲染一个 combobox。
    /// </summary>
    public bool IsEditableSelectOptions { get; set; } = false;
    
    /// <summary>
    /// 指定用于本地化选项标签的资源类型。
    /// 如果设置了此属性，<see cref="Options"/> 中每个元组的 <c>Label</c> 将被用作键，
    /// 从指定的资源文件中查找本地化的字符串。
    /// </summary>
    /// <example>
    /// ResourceType = typeof(MyProject.Resources.MyStrings)
    /// </example>
    public Type? ResourceType { get; set; }

    /// <summary>
    /// Value=Label时的构造函数
    /// </summary>
    /// <param name="options">字符串列表</param>
    public StringOptionsAttribute(params string[] options) : this(options.ToList().ConvertAll(option => (option, option)).ToArray()) { }

    /// <param name="options">选项列表</param>
    public StringOptionsAttribute(params (string Value, string Label)[] options) : this()
    {
        this.Options = options;
    }

    /// <inheritdoc />
    public StringOptionsAttribute(string[] values, string[] labels) : this()
    {
        this.Options = values.Zip(labels).ToArray();
    }
}

/// <summary>
/// 处理带有 [StringOptions] 的属性，为其生成 enum, enumNames, default, 
/// 以及指向自定义自动完成 Widget 的 ui:options 配置。
/// </summary>
internal class StringOptionsProcessor : YaePropertyAttributeProcessor<StringOptionsAttribute>
{
    protected override void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema, StringOptionsAttribute attribute)
    {
        // 1. 处理静态选项
        if (attribute.Options.Any())
        {
            // 使用 JsonArray 来创建 enum 和 enumNames
            var enumValues = attribute.Options.Select(o =>o.Value).ToJsonArray();
            schema["enum"] = enumValues;
            
            JsonArray enumLabels;

            // 检查是否需要本地化
            if (attribute.ResourceType is not null)
            {
                var resourceManager = new ResourceManager(attribute.ResourceType);
                enumLabels = attribute.Options
                    .Select(o => resourceManager.GetString(o.Label) ?? o.Label) // 查找资源，如果找不到则回退到键名
                    .ToJsonArray();
            }
            else
            {
                // 保持原始行为
                enumLabels = attribute.Options.Select(o => o.Label).ToJsonArray();
            }
            
            // JSON Schema 标准中没有 "enumNames"，这是一个 UI 库的扩展。
            // vue-json-schema-form 使用它。我们直接写入。
            schema["enumNames"] = enumLabels;


            // 2. 处理默认值
            // 检查属性上是否已经有 DefaultValueAttribute
            var defaultAttribute = context.PropertyInfo?.AttributeProvider.GetCustomAttribute<DefaultValueAttribute>();
            if (defaultAttribute is null)
            {
                // 如果没有显式的默认值，就使用选项中的第一个作为默认值
                schema["default"] = attribute.Options.First().Value;
            }
        }

        // 3. 处理 ui:options
        // 只有当需要传递动态加载端点或可编辑标志时，才创建 ui:options
        if (attribute.IsEditableSelectOptions || !string.IsNullOrWhiteSpace(attribute.OptionsProviderEndpoint))
        {
            var uiOptions = schema["ui:options"];
            if (uiOptions is null)
            {
                uiOptions = new JsonObject();
                schema["ui:options"] = uiOptions;
            }

            if (attribute.IsEditableSelectOptions)
            {
                // 注意：这里的键名需要与你前端的 SchemaUiSetting 记录类中的 JsonPropertyName 匹配
                uiOptions["isEditableSelectOptions"] = true;
            }

            if (!string.IsNullOrWhiteSpace(attribute.OptionsProviderEndpoint))
            {
                uiOptions["optionsProviderEndpoint"] = attribute.OptionsProviderEndpoint;
            }
        }
    }
}