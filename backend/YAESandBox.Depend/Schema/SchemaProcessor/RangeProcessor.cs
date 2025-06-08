using System.ComponentModel.DataAnnotations;
using Namotion.Reflection;
using NJsonSchema;
using NJsonSchema.Generation;
using YAESandBox.Depend.Schema.Attributes;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

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