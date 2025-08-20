using System.Reflection;

namespace YAESandBox.Depend.Schema;

/// <summary>
/// 提供 ICustomAttributeProvider 的扩展方法，以简化 Attribute 的获取。
/// </summary>
public static class AttributeProviderExtensions
{
    /// <summary>
    /// 获取应用于提供者的第一个指定类型的自定义 Attribute，如果不存在则返回 null。
    /// 这是一个 <c>GetCustomAttribute&lt;T&gt;()</c> 的便捷实现。
    /// </summary>
    /// <typeparam name="T">要查找的 Attribute 类型。</typeparam>
    /// <param name="provider">在其上查找 Attribute 的 ICustomAttributeProvider。</param>
    /// <param name="inherit">是否查找继承链。</param>
    /// <returns>找到的第一个 Attribute 实例，或 null。</returns>
    public static T? GetCustomAttribute<T>(this ICustomAttributeProvider? provider, bool inherit = true) where T : Attribute
    {
        if (provider is null)
            return null;
        
        // 1. 先使用 .NET 内置的高效方法，它能处理绝大多数情况。
        //    我们传递 inherit: true。
        if (provider.GetCustomAttributes(typeof(T), true).FirstOrDefault() is T attribute)
        {
            return attribute;
        }

        // 2. 如果内置方法找不到（特别是在 record override 的场景下），
        //    并且提供者是一个 MemberInfo，我们启动手动追溯。
        if (provider is MemberInfo memberInfo)
        {
            return FindAttributeInHierarchyManually<T>(memberInfo);
        }

        return null;
    }
    
    /// <summary>
    /// 手动遍历类型的继承层次结构，以查找指定成员上的特定 Attribute。
    /// </summary>
    private static T? FindAttributeInHierarchyManually<T>(MemberInfo memberInfo) where T : Attribute
    {
        switch (memberInfo)
        {
            case Type currentType:
            {
                var baseType = currentType.BaseType;
                while (baseType != null && baseType != typeof(object))
                {
                    if (baseType.GetCustomAttributes(typeof(T), false).FirstOrDefault() is T attr) 
                        return attr;
                    baseType = baseType.BaseType;
                }

                break;
            }
            case PropertyInfo propertyInfo:
            {
                var currentType = propertyInfo.DeclaringType?.BaseType;
                while (currentType != null && currentType != typeof(object))
                {
                    var baseProperty = currentType.GetProperty(propertyInfo.Name,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                    if (baseProperty != null)
                    {
                        // 在基类属性上查找，inherit 必须为 false，因为我们只关心这一层
                        var attr = baseProperty.GetCustomAttributes(typeof(T), false).FirstOrDefault() as T;
                        if (attr != null) return attr;
                    }
                    currentType = currentType.BaseType;
                }

                break;
            }
        }

        return null;
    }
}
