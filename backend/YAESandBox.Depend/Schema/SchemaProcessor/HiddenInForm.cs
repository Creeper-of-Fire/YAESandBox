 namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 在Schema中隐藏
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class HiddenInFormAttribute(bool isHidden) : Attribute
{
    /// <summary>
    /// 是否隐藏
    /// </summary>
    public bool IsHidden { get; } = isHidden;
}

/// <summary>
/// 检查有无隐藏标签
/// </summary>
internal class HiddenInFormProcessor() : NormalAttributeProcessor<HiddenInFormAttribute>("ui:hidden", attribute => attribute.IsHidden);

// /// <summary>
// /// 隐藏标题和描述
// /// </summary>
// public class HiddenDisplayProcessor() : NormalAttributeProcessor<HiddenDisplayAttribute>((extentData, attribute) =>
// {
//     if (attribute.IsHiddenTitle)
//         extentData["ui:title"] = "";
//     if (attribute.IsHiddenDescription)
//         extentData["ui:description"] = "";
// });



// /// <summary>
// /// 隐藏标题和描述
// /// </summary>
// [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Class, AllowMultiple = false)]
// public class HiddenDisplayAttribute(bool isHiddenTitle,  bool isHiddenDescription) : Attribute
// {
//     /// <summary>
//     /// 是否隐藏标题
//     /// </summary>
//     public bool IsHiddenTitle { get; } = isHiddenTitle;
//
//     /// <summary>
//     /// 是否隐藏描述
//     /// </summary>
//     public bool IsHiddenDescription { get; } = isHiddenDescription;
// }