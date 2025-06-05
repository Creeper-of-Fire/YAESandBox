using NJsonSchema.Annotations;

namespace YAESandBox.Depend.Storage;

internal record FilePath(string RootPath, string FileName, string SubDirectory)
{
    internal FilePath(string rootPath, string fileName, [CanBeNull] params string[] subDirectories) :
        this(rootPath, fileName, Path.Combine(subDirectories)) { }

    internal string TotalPath => Path.Combine(this.RootPath, this.SubDirectory, this.FileName);
    internal string TotalDirectory => Path.Combine(this.RootPath, this.SubDirectory);

    internal string SubPath => Path.Combine(this.SubDirectory, this.FileName);
    public override string ToString() => this.TotalPath;

    /// <summary>
    /// 组合路径
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="rootPath"></param>
    /// <param name="subDirectories"></param>
    /// <returns></returns>
    public static string CombinePath(string fileName, string rootPath, params string[] subDirectories) =>
        Path.Combine(rootPath, Path.Combine(subDirectories), fileName);
}