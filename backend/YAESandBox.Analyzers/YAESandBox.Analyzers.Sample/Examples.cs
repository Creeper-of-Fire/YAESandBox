// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using YAESandBox.Depend.Results;

namespace YAESandBox.Analyzers.Sample;

class TestClass
{
    public Result DoWork() => Result.Ok();

    public void Run()
    {
        var result = this.DoWork();
    }
}