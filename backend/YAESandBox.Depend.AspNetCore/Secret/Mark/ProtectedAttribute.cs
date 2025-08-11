namespace YAESandBox.Depend.AspNetCore.Secret.Mark;

/// <summary>
/// 将属性标记为受保护的数据。
/// 该属性的值在持久化时应被加密，在加载到内存时应被解密。
/// 目标属性的类型应为 string。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ProtectedAttribute : Attribute;