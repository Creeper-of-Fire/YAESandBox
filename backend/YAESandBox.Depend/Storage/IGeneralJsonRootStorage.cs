namespace YAESandBox.Depend.Storage;

/// <summary>
/// 一个标记接口，用于明确标识一个操作绝对根数据路径的存储服务。
/// 它继承了 IGeneralJsonStorage 的所有功能，但其主要目的是在依赖注入时
/// 区分“根存储”和“作用域存储”（如 ScopedJsonStorage），
/// 以防止将作用域存储意外地作为另一个作用域工厂的根。
/// </summary>
public interface IGeneralJsonRootStorage : IGeneralJsonStorage;