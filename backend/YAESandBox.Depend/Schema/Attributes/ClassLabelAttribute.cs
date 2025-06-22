namespace YAESandBox.Depend.Schema.Attributes;

/// <summary>
/// 给Class一个别名
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false)]
public class ClassLabelAttribute(string label) : Attribute
{
    /// <summary>
    /// Class的标签
    /// </summary>
    public string Label { get; } = label;
}