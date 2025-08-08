namespace YAESandBox.Depend.Storage;

/// <summary>
/// 一个用来描述文件路径的类，主要是为了减少手动的地址拼接。
/// </summary>
/// <param name="RootPath"></param>
/// <param name="FileName"></param>
/// <param name="SubDirectory"></param>
public record FilePath(string RootPath, string FileName, string SubDirectory)
{
    internal FilePath(string rootPath, string fileName, params string[] subDirectories) :
        this(rootPath, fileName, Path.Combine(subDirectories)) { }

    internal string TotalPath => Path.Combine(this.RootPath, this.SubDirectory, this.FileName);
    internal string TotalDirectory => Path.Combine(this.RootPath, this.SubDirectory);

    internal string SubPath => Path.Combine(this.SubDirectory, this.FileName);

    /// <inheritdoc />
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