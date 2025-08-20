using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 自定义范围特性，用于指定数值属性的最小值、最大值和步长。
/// 这些值是可选的，并且以 object 类型存储，通过 OperandType 提供类型提示。
/// 主要用于辅助生成 JSON Schema。
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class CustomRangeAttribute : Attribute
{
    /// <summary>
    /// 获取或初始化允许的最小值。
    /// 如果为 null，则表示没有指定最小值。
    /// </summary>
    public object? Minimum { get; init; }

    /// <summary>
    /// 获取或初始化允许的最大值。
    /// 如果为 null，则表示没有指定最大值。
    /// </summary>
    public object? Maximum { get; init; }

    /// <summary>
    /// 获取或初始化值的步长或增量。
    /// 例如，对于数字，可以是 1, 5, 0.1。
    /// 如果为 null，则表示没有指定步长。
    /// </summary>
    public object? Step { get; init; }

    /// <summary>
    /// 获取或初始化操作数的预期类型（例如 typeof(int), typeof(double)）。
    /// 这有助于 JSON Schema 生成器理解 Minimum, Maximum, Tuum 的预期数据类型。
    /// 如果为 null，则类型未指定，可能需要从属性本身的类型推断。
    /// </summary>
    public Type OperandType { get; init; }

    /// <summary>
    /// 创建一个 CustomRangeAttribute 实例，用于指定数值属性的允许范围。
    /// </summary>
    /// <param name="minimum"></param>
    /// <param name="maximum"></param>
    public CustomRangeAttribute(int? minimum, int? maximum)
    {
        this.Minimum = minimum;
        this.Maximum = maximum;
        this.Step = 1;
        this.OperandType = typeof(int);
    }

    /// <summary>
    /// 创建一个 CustomRangeAttribute 实例，用于指定数值属性的允许范围。带有步长。
    /// </summary>
    /// <param name="minimum"></param>
    /// <param name="maximum"></param>
    /// <param name="step"></param>
    public CustomRangeAttribute(double? minimum, double? maximum, double? step = null)
    {
        this.Minimum = minimum;
        this.Maximum = maximum;
        this.Step = step;
        this.OperandType = typeof(double);
    }

    /// <summary>
    /// 创建一个 CustomRangeAttribute 实例，用于指定数值属性的允许范围。带有步长和字符串格式。
    /// </summary>
    /// <param name="operandType"></param>
    /// <param name="minimum"></param>
    /// <param name="maximum"></param>
    /// <param name="step"></param>
    public CustomRangeAttribute(Type operandType, string? minimum, string? maximum, string? step = null)
    {
        this.OperandType = operandType;
        this.Step = step;
        this.Minimum = minimum;
        this.Maximum = maximum;
    }
}

/// <summary>
/// 处理带有 [Range] 的属性，
/// 为其生成枚举和/或指向自定义自动完成 Widget 的配置。
/// </summary>
internal class RangeProcessor : IYaeSchemaProcessor
{
    /// <summary>
    /// 如果计算得到的 MultipleOf 大于它，则设置为它
    /// </summary>
    private const decimal DefaultMultipleOf = 0.01m;

    /// <summary>
    /// 至少 100 个步长
    /// </summary>
    private const decimal MinStepNumber = 100;

    /// <inheritdoc/>
    public void Process(JsonSchemaExporterContext context, JsonObject schema)
    {
        // 这是一个属性级别的处理器
        if (context.PropertyInfo is null)
            return;

        var rangeAttribute = context.PropertyInfo.AttributeProvider.GetCustomAttribute<RangeAttribute>(inherit: true);
        var customRangeAttribute = context.PropertyInfo.AttributeProvider.GetCustomAttribute<CustomRangeAttribute>(inherit: true);

        if (rangeAttribute == null && customRangeAttribute == null)
            return;

        // 优先使用 CustomRangeAttribute 的值，因为它更具体
        decimal? minimum = null;
        if (customRangeAttribute?.Minimum is not null)
            minimum = Convert.ToDecimal(customRangeAttribute.Minimum);
        else if (rangeAttribute?.Minimum is not null)
            minimum = Convert.ToDecimal(rangeAttribute.Minimum);

        decimal? maximum = null;
        if (customRangeAttribute?.Maximum is not null)
            maximum = Convert.ToDecimal(customRangeAttribute.Maximum);
        else if (rangeAttribute?.Maximum is not null)
            maximum = Convert.ToDecimal(rangeAttribute.Maximum);

        decimal? step = null;
        if (customRangeAttribute?.Step is not null)
            step = Convert.ToDecimal(customRangeAttribute.Step);

        // 将解析出的值写入 Schema
        if (minimum.HasValue)
            schema["minimum"] = minimum.Value;

        if (maximum.HasValue)
            schema["maximum"] = maximum.Value;

        // 如果明确指定了 step，则使用它作为 multipleOf
        if (step.HasValue)
            schema["multipleOf"] = step.Value;

        // 否则，如果提供了范围，则根据旧逻辑计算一个默认的步长，以支持UI滑块
        else if (minimum.HasValue && maximum.HasValue &&
                 (SchemaTypeContains(schema.TryGetPropertyValue("type", out var typeNode) ? typeNode : null, "number") ||
                  SchemaTypeContains(schema.TryGetPropertyValue("type", out typeNode) ? typeNode : null, "integer")))
        {
            decimal calculatedStep = (maximum.Value - minimum.Value) / MinStepNumber;
            // 确保计算出的步长不会无穷小
            if (calculatedStep > 0)
            {
                schema["multipleOf"] = Math.Min(calculatedStep, DefaultMultipleOf);
            }
        }
    }

    // 辅助函数，用于检查 Schema 类型是否包含指定的类型
    private static bool SchemaTypeContains(JsonNode? typeNode, string expectedType)
    {
        if (typeNode is null)
        {
            return false;
        }

        // 情况1：type 是一个单一的字符串值
        if (typeNode is JsonValue value && value.TryGetValue(out string? typeString))
        {
            return typeString == expectedType;
        }

        // 情况2：type 是一个类型数组
        if (typeNode is JsonArray array)
        {
            // 检查数组中是否包含我们期望的类型
            return array.Any(node => node is JsonValue v && v.TryGetValue(out string? t) && t == expectedType);
        }

        return false;
    }
}