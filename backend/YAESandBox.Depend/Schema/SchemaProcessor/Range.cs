using System.ComponentModel.DataAnnotations;
using Namotion.Reflection;
using NJsonSchema;
using NJsonSchema.Generation;

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
internal class RangeProcessor : ISchemaProcessor
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
    public void Process(SchemaProcessorContext context)
    {
        var rangeAttribute = context.ContextualType.GetContextAttribute<RangeAttribute>(true);
        var customRangeAttribute = context.ContextualType.GetContextAttribute<CustomRangeAttribute>(true);
        context.Schema.ExtensionData ??= new Dictionary<string, object?>();

        if (rangeAttribute == null && customRangeAttribute == null) return;
        if (customRangeAttribute?.Step != null)
            context.Schema.MultipleOf = Convert.ToDecimal(customRangeAttribute.Step);

        decimal? maximum = null;
        decimal? minimum = null;
        if (rangeAttribute != null)
        {
            maximum = Convert.ToDecimal(rangeAttribute.Maximum);
            minimum = Convert.ToDecimal(rangeAttribute.Minimum);
        }

        if (customRangeAttribute?.Maximum != null)
        {
            maximum = Convert.ToDecimal(customRangeAttribute.Maximum);
            context.Schema.ExtensionData["maximum"] = maximum;
        }

        if (customRangeAttribute?.Minimum != null)
        {
            minimum = Convert.ToDecimal(customRangeAttribute.Minimum);
            context.Schema.ExtensionData["minimum"] = minimum;
        }

        if (context.Schema.Type != JsonObjectType.Number || maximum == null || minimum == null) return;
        decimal step = (maximum.Value - minimum.Value) / MinStepNumber;
        context.Schema.MultipleOf = decimal.Min(step, DefaultMultipleOf);
    }
}