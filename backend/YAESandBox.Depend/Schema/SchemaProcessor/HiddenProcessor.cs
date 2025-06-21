using YAESandBox.Depend.Schema.Attributes;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 检查有无隐藏标签
/// </summary>
internal class HiddenProcessor() : NormalAttributeProcessor<HiddenInFormAttribute>("ui:hidden", attribute => attribute.IsHidden);

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