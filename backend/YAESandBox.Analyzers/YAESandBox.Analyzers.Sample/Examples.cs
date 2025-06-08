// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using FluentResults;

class TestClass
{
    public Result DoWork() => Result.Ok();

    public void Run()
    {
        var result = this.DoWork();
    }
}