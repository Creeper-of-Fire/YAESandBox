using System.Collections;
using System.Reflection;

namespace YAESandBox.Depend.AspNetCore.Secret;

/// <summary>
/// 处理数据的加密和解密
/// </summary>
public interface IDataProtectionService
{
    /// <summary>
    /// 深度加密对象中所有标记为 [Protected] 的属性。
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="dataObject">要加密的对象</param>
    /// <returns>返回同一个对象实例，但敏感字段已被加密。</returns>
    T ProtectObject<T>(T dataObject);

    /// <summary>
    /// 深度解密对象中所有标记为 [Protected] 的属性。
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="dataObject">要解密的对象</param>
    /// <returns>返回同一个对象实例，但敏感字段已被解密。</returns>
    T UnprotectObject<T>(T dataObject);
}

/// <inheritdoc />
public class DataProtectionService(ISecretProtector secretProtector) : IDataProtectionService
{
    private ISecretProtector SecretProtector { get; } = secretProtector;

    /// <inheritdoc />
    public T ProtectObject<T>(T dataObject)
    {
        ProcessObject(dataObject, (protector, value) => protector.Protect(value));
        return dataObject;
    }

    /// <inheritdoc />
    public T UnprotectObject<T>(T dataObject)
    {
        ProcessObject(dataObject, (protector, value) => protector.Unprotect(value));
        return dataObject;
    }

    /// <summary>
    /// 递归处理对象及其嵌套对象。
    /// </summary>
    private void ProcessObject(object? instance, Func<ISecretProtector, string, string> operation)
    {
        if (instance is null)
            return;


        // 如果对象是集合类型（如 List, Array），则递归处理其中每一个元素
        if (instance is IEnumerable enumerable and not string)
        {
            foreach (object? item in enumerable)
            {
                this.ProcessObject(item, operation);
            }
        }

        if (instance is not IProtectedData)
            return;

        var type = instance.GetType();
        // 4. 遍历当前对象的所有属性。
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(property => property is { CanRead: true, CanWrite: true }))
        {
            // A. 如果属性本身被标记为 [Protected]
            if (property.GetCustomAttribute<ProtectedAttribute>() != null)
            {
                if (property.GetValue(instance) is not string currentValue)
                    continue;
                string newValue = operation(this.SecretProtector, currentValue);
                property.SetValue(instance, newValue);
            }
            // B. 如果属性值是另一个需要保护的对象或集合 (递归的触发点)
            else if (property.GetValue(instance) is { } propertyValue)
            {
                this.ProcessObject(propertyValue, operation); // 对子对象/集合进行递归处理
            }
        }
    }
}