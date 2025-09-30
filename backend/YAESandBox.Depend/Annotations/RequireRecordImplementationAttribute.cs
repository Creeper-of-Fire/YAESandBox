namespace YAESandBox.Depend.Annotations;

/// <summary>
/// 使用了这个属性，则表示该接口必须实现为 Record
/// </summary>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class RequireRecordImplementationAttribute : Attribute;