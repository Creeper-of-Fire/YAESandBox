using System.ComponentModel;
using Namotion.Reflection;
using NJsonSchema;
using NJsonSchema.Generation;

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
/// 处理带有 [StringOptionsAttribute] 的属性，
/// 为其生成枚举和/或指向自定义自动完成 Widget 的配置。
/// </summary>
internal class StringOptionsProcessor : ISchemaProcessor
{
    /// <inheritdoc/>
    public void Process(SchemaProcessorContext context)
    {
        // 获取属性上的 StringOptionsAttribute
        // context.ContextualAttributes 包含与当前类型/成员关联的所有特性
        var optionsAttribute = context.ContextualType.GetContextAttribute<StringOptionsAttribute>(true);

        if (optionsAttribute == null ||
            context.Schema.Type.HasFlag(JsonObjectType.Object) ||
            context.Schema.Type.HasFlag(JsonObjectType.Array))
        {
            return;
        }

        // 确认属性是字符串类型，如果不是，则此 Processor 不适用或发出警告
        // NJsonSchema 通常会根据 C# 属性类型正确设置 context.Schema.Type
        // 这里我们假设它已经是 string 或将要是 string
        if (!context.Schema.Type.HasFlag(JsonObjectType.String) && !context.Schema.Type.HasFlag(JsonObjectType.Null)) // 允许可空字符串
        {
            // 可以选择记录一个警告，或者如果类型绝对应该是 string，则强制设置
            // Console.WriteLine($"警告: StringOptionsAttribute 应用于非字符串类型属性 '{context.PropertyInfo?.Name}'。Schema 类型: {context.Schema.Type}");
            // 为了确保，我们强制它为 string，因为 StringOptionsAttribute 暗示了这一点。
            context.Schema.Type = JsonObjectType.String;
            if (context.ContextualType.IsNullableType) // 如果原始C#类型是可空的 string?
            {
                context.Schema.Type |= JsonObjectType.Null;
            }
        }

        context.Schema.ExtensionData ??= new Dictionary<string, object?>();

        // 1. 处理静态选项 (作为 Schema 的 enum/enumNames)
        // 这些选项将由自定义 Widget 读取作为初始/静态建议
        if (optionsAttribute.Options.Length > 0)
        {
            context.Schema.Enumeration.Clear(); // 清除可能由 NJsonSchema 默认生成的（例如，如果属性是C# enum）
            foreach (string val in optionsAttribute.Options.Select(o => o.Value))
                context.Schema.Enumeration.Add(val);

            // **** 关键修正：确保生成 "enumNames" ****
            context.Schema.EnumerationNames.Clear(); // 清理 NJsonSchema 可能已填充的 EnumerationNames

            // 直接将标签列表添加到 ExtensionData["enumNames"]，这是最可靠的方式
            // 以确保最终 JSON 中是 "enumNames" 而不是 "x-enumNames" 或其他。
            context.Schema.ExtensionData["enumNames"] = optionsAttribute.Options.Select(o => o.Label).ToList();

            var defaultAttribute = context.ContextualType.GetContextAttribute<DefaultValueAttribute>(true);
            if (defaultAttribute == null)
                context.Schema.ExtensionData["default"] = optionsAttribute.Options.First().Value;
        }

        // // 2. 指定使用自定义的自动完成 Widget
        // if (attribute.isEditableSelectOptions)
        //     context.Schema.ExtensionData["ui:widget"] = "MyCustomStringAutoComplete"; // 前端自定义组件的名称
        // else
        //     context.Schema.ExtensionData["ui:widget"] = "RadioWidget";

        var uiOptions = context.Schema.GetOrCreateUiOptions();

        if (optionsAttribute.IsEditableSelectOptions)
            uiOptions.IsEditableSelectOptions = true;

        // 3. 如果有 optionsProviderEndpoint，则将其传递给 Widget
        if (!string.IsNullOrWhiteSpace(optionsAttribute.OptionsProviderEndpoint))
            uiOptions.OptionsProviderEndpoint = optionsAttribute.OptionsProviderEndpoint;

        // 备注: isEditableSelectOptions 属性目前没有直接用于改变 Schema 生成逻辑，
        // 因为我们选择的 MyCustomStringAutoComplete Widget 本身就是可编辑的。
        // 如果需要，可以将 attribute.isEditableSelectOptions 的值也通过 uiOptions 传递给 Widget。
        // var uiOpt = SchemaProcessorHelper.GetOrCreateUiOptions(context.Schema);
        // uiOpt["isEditableFlagForWidget"] = attribute.isEditableSelectOptions;
    }
}