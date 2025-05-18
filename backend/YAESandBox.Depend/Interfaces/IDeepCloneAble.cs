namespace YAESandBox.Depend.Interfaces;

/// <summary>
/// 定义一个可深度克隆对象的泛型接口。
/// 实现此接口的类型必须提供一个返回新副本的 <see cref="DeepClone"/> 方法。
/// </summary>
/// <typeparam name="T">该类型自身或其派生类型的实例，表示克隆后的返回值类型。</typeparam>
public interface IDeepCloneAble<out T>
{
    /// <summary>
    /// 执行当前对象的深度克隆（Deep Copy）。
    /// 克隆后的对象应与原对象具有相同的值状态，但不共享引用类型字段的内存。
    /// </summary>
    /// <returns>当前对象的一个深拷贝。</returns>
    T DeepClone();
}