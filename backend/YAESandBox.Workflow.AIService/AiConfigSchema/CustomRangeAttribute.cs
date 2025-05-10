// CustomRangeAttribute.cs (在你的 AiConfigSchema 或共享的特性命名空间下)

using System.ComponentModel.DataAnnotations; // 为了能配合 ValidationAttribute 的逻辑 (如果需要)
using System.Globalization;

namespace YAESandBox.Workflow.AIService.AiConfigSchema;
// 或者你选择的命名空间

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class CustomRangeAttribute : RangeAttribute // 继承 ValidationAttribute 以便参与验证 (可选)
{
    public object? Step { get; set; } // 新增 Step 属性

    // 构造函数与 RangeAttribute 类似
    public CustomRangeAttribute(int minimum, int maximum) : base(minimum, maximum) { }

    public CustomRangeAttribute(double minimum, double maximum) : base(minimum, maximum) { }

    /// <summary>
    /// 构造函数，允许指定类型。主要用于当属性是可空类型时，
    /// 或者当你想在非数字类型上使用它（尽管这通常没有意义）。
    /// 对于我们的场景，主要是为了处理数字类型。
    /// </summary>
    /// <param name="type">期望的操作数类型（例如 typeof(int), typeof(double), typeof(decimal)）。</param>
    /// <param name="minimum">最小值的字符串表示。</param>
    /// <param name="maximum">最大值的字符串表示。</param>
    public CustomRangeAttribute(Type type, string minimum, string maximum) : base(type, minimum, maximum) { }
}