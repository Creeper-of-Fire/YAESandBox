using System.Text.Json.Nodes;
using YAESandBox.Depend.Results;
using static YAESandBox.Depend.Storage.IGeneralJsonStorage;

namespace YAESandBox.Depend.Storage;

/// <see cref="ScopedJsonStorage"/> 的定义、拓展方法和工厂方法
public static class ScopedStorageFactory
{
    /// <summary>
    /// 创建一个模板，用于创建配置文件作用域。
    /// </summary>
    /// <remarks>Config顶层作用域专用</remarks>
    public static ScopeTemplate ConfigRoot() =>
        ScopeTemplate.Root.CreateScope(StorageType.Configs.GetSubDirectory());

    /// <summary>
    /// 创建一个模板，用于创建存档文件作用域。
    /// </summary>
    /// <remarks>Save顶层作用域专用</remarks>
    public static ScopeTemplate SaveRoot() =>
        ScopeTemplate.Root.CreateScope(StorageType.Saves.GetSubDirectory());

    /// <summary>
    /// 创建一个<see cref="ScopedJsonStorage"/>
    /// </summary>
    /// <param name="jsonStorage">原始的IGeneralJsonStorage</param>
    /// <param name="additionalScope">在原始IGeneralJsonStorage后增加的新作用域（或者说子文件夹）</param>
    /// <returns></returns>
    public static ScopedJsonStorage CreateScope(this IGeneralJsonStorage jsonStorage, params string[] additionalScope) =>
        new(jsonStorage, additionalScope);

    private const string ConfigDirectory = "Configurations";
    private const string SaveDirectory = "Saves";

    /// <summary>
    /// 根据<see cref="StorageType"/>获取对应的子文件夹名称
    /// </summary>
    /// <param name="storageType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private static string GetSubDirectory(this StorageType storageType)
    {
        return storageType switch
        {
            StorageType.Configs => ConfigDirectory,
            StorageType.Saves => SaveDirectory,
            _ => throw new ArgumentOutOfRangeException(nameof(storageType), storageType, null)
        };
    }

    /// <summary>
    /// 存储类型
    /// </summary>
    public enum StorageType
    {
        /// <summary>
        /// 配置文件
        /// </summary>
        Configs,

        /// <summary>
        /// 存档文件
        /// </summary>
        Saves,
    }

    /// <summary>
    /// 在特定作用域下运行的JsonStorage。
    /// </summary>
    /// <remarks>实际上是对于IGeneralJsonStorage的一个包装，理论上你可以反复调用它以实现作用域的逐渐组装。</remarks>
    public record ScopedJsonStorage : IGeneralJsonStorage
    {
        /// <inheritdoc cref="ScopedJsonStorage"/>
        internal ScopedJsonStorage(IGeneralJsonStorage generalJsonStorage, string[] scopePrefixPathParts)
        {
            if (generalJsonStorage is ScopedJsonStorage scopedJsonStorage)
            {
                this.GeneralJsonStorage = scopedJsonStorage.GeneralJsonStorage;
                this.ScopePrefixPathParts = scopedJsonStorage.ScopePrefixPathParts.Concat(scopePrefixPathParts).ToArray();
            }
            else
            {
                this.GeneralJsonStorage = generalJsonStorage;
                this.ScopePrefixPathParts = scopePrefixPathParts;
            }

            this.WorkPath = Path.Combine(generalJsonStorage.WorkPath, Path.Combine(scopePrefixPathParts));
        }

        /// <inheritdoc />
        public string WorkPath { get; }

        private IGeneralJsonStorage GeneralJsonStorage { get; }

        /// <summary>
        /// 在原始IGeneralJsonStorage后增加的新作用域（或者说子文件夹）
        /// </summary>
        private string[] ScopePrefixPathParts { get; }

        /// <inheritdoc />
        public Task<Result<JsonNode?>> LoadJsonNodeAsync(string fileName, params string[] subDirectories) =>
            this.GeneralJsonStorage.LoadJsonNodeAsync(fileName, this.ScopePrefixPathParts.Concat(subDirectories).ToArray());

        /// <inheritdoc />
        public Task<Result> SaveJsonNodeAsync(JsonNode? jsonNode, string fileName, params string[] subDirectories) =>
            this.GeneralJsonStorage.SaveJsonNodeAsync(jsonNode, fileName, this.ScopePrefixPathParts.Concat(subDirectories).ToArray());

        /// <inheritdoc />
        public Task<Result> SaveAllAsync<T>(T? needSaveObj, string fileName, params string[] subDirectories) =>
            this.GeneralJsonStorage.SaveAllAsync(needSaveObj, fileName, this.ScopePrefixPathParts.Concat(subDirectories).ToArray());

        /// <inheritdoc />
        public Task<Result<T?>> LoadAllAsync<T>(string fileName, params string[] subDirectories) =>
            this.GeneralJsonStorage.LoadAllAsync<T>(fileName, this.ScopePrefixPathParts.Concat(subDirectories).ToArray());

        /// <inheritdoc />
        public Task<Result<IEnumerable<string>>> ListFileNamesAsync(ListFileOption? listOption = null, params string[] subDirectories) =>
            this.GeneralJsonStorage.ListFileNamesAsync(listOption, this.ScopePrefixPathParts.Concat(subDirectories).ToArray());

        /// <inheritdoc />
        public Task<Result> DeleteFileAsync(string fileName, params string[] subDirectoryParts) =>
            this.GeneralJsonStorage.DeleteFileAsync(fileName, this.ScopePrefixPathParts.Concat(subDirectoryParts).ToArray());
    }
}

/// <summary>
/// 一个不可变的模板，用于定义一个相对的作用域路径。
/// 它可以被应用到任何 IGeneralJsonStorage 实例上，从而创建一个新的 ScopedJsonStorage。
/// 这提供了一种安全、可复用的方式来构建分层存储结构。
/// </summary>
/// <param name="ScopePathParts">组成相对路径的目录名。</param>
public sealed record ScopeTemplate(params string[] ScopePathParts)
{
    /// <summary>
    /// 定义此模板的相对路径部分。
    /// </summary>
    private string[] ScopePathParts { get; } = ScopePathParts;

    /// <summary>
    /// 从一个基础模板创建一个新的子模板。
    /// </summary>
    /// <param name="additionalScopeParts">要附加到当前模板路径的新目录名。</param>
    /// <returns>一个新的 ScopeTemplate 实例，包含了组合后的路径。</returns>
    public ScopeTemplate CreateScope(params string[] additionalScopeParts)
    {
        string[] newPathParts = this.ScopePathParts.Concat(additionalScopeParts).ToArray();
        return new ScopeTemplate(newPathParts);
    }

    /// <summary>
    /// 将此模板应用到一个基础存储实例上，从而创建一个具有作用域的存储。
    /// </summary>
    /// <param name="baseStorage">要应用此模板的基础存储（可以是根存储，也可以是另一个作用域存储）。</param>
    /// <returns>一个新的 ScopedJsonStorage 实例。</returns>
    public ScopedStorageFactory.ScopedJsonStorage ApplyOn(IGeneralJsonStorage baseStorage)
    {
        return baseStorage.CreateScope(this.ScopePathParts);
    }

    /// <summary>
    /// 创建一个表示根作用域的模板（即没有路径）。
    /// </summary>
    public static ScopeTemplate Root { get; } = new();
}