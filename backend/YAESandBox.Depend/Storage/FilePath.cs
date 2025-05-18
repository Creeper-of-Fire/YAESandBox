using NJsonSchema.Annotations;

namespace YAESandBox.Depend.Storage;

internal record FilePath(string RootPath, string FileName, string SubDirectory)
{
    internal FilePath(string rootPath, string fileName, [CanBeNull] params string[] subDirectories) :
        this(rootPath, Path.Combine(subDirectories), fileName) { }

    internal string TotalPath => Path.Combine(this.RootPath, this.SubDirectory, this.FileName);
    internal string TotalDirectory => Path.Combine(this.RootPath, this.SubDirectory);

    internal string SubPath => Path.Combine(this.SubDirectory, this.FileName);
    public override string ToString() => this.TotalPath;
}