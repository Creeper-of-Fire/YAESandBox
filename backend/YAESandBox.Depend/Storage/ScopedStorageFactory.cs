using System.Text.Json.Nodes;
using FluentResults;
using static YAESandBox.Depend.Storage.IGeneralJsonStorage;

namespace YAESandBox.Depend.Storage;

/// <see cref="ScopedJsonStorage"/> 的定义、拓展方法和工厂方法
public static class ScopedStorageFactory
{
    /// <summary>
    /// 创建一个<see cref="ScopedJsonStorage"/>，通过<see cref="StorageType"/>生成顶层作用域。
    /// （但并非真正的根作用域，根作用域的具体位置——基于相对或绝对路径，应当在前置的IGeneralJsonStorage中定义）
    /// </summary>
    /// <param name="jsonStorage">原始的IGeneralJsonStorage</param>
    /// <param name="storageType">存储格式</param>
    /// <returns></returns>
    public static ScopedJsonStorage CreateScope(this IGeneralJsonStorage jsonStorage, StorageType storageType) =>
        CreateScope(jsonStorage, storageType.GetSubDirectory());

    /// <inheritdoc cref="CreateScope(IGeneralJsonStorage, StorageType)"/>
    /// <remarks>Config顶层作用域专用</remarks>
    public static ScopedJsonStorage ForConfig(this IGeneralJsonStorage jsonStorage) =>
        CreateScope(jsonStorage, StorageType.Configs);

    /// <inheritdoc cref="CreateScope(IGeneralJsonStorage, StorageType)"/>
    /// <remarks>Save顶层作用域专用</remarks>
    public static ScopedJsonStorage ForSave(this IGeneralJsonStorage jsonStorage) => CreateScope(jsonStorage, StorageType.Saves);

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

    private static string GetSubDirectory(this StorageType configType)
    {
        return configType switch
        {
            StorageType.Configs => ConfigDirectory,
            StorageType.Saves => SaveDirectory,
            _ => throw new ArgumentOutOfRangeException(nameof(configType), configType, null)
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
            this.GeneralJsonStorage = generalJsonStorage is ScopedJsonStorage scopedJsonStorage
                ? scopedJsonStorage.GeneralJsonStorage
                : generalJsonStorage;
            this.ScopePrefixPathParts = scopePrefixPathParts;
            this.WorkPath = Path.Combine(generalJsonStorage.WorkPath, Path.Combine(scopePrefixPathParts));
        }

        private IGeneralJsonStorage GeneralJsonStorage { get; }

        /// <summary>
        /// 在原始IGeneralJsonStorage后增加的新作用域（或者说子文件夹）
        /// </summary>
        private string[] ScopePrefixPathParts { get; }

        /// <inheritdoc />
        /// <remarks>这里不使用它，但是合成后可以传递</remarks>
        public string WorkPath { get; }

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