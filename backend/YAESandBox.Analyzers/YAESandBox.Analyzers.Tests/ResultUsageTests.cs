// 文件: YAESandBox.Analyzers.Tests/ResultUsageAnalyzerTests.cs

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    YAESandBox.Analyzers.ResultUsageAnalyzer,
    YAESandBox.Analyzers.ResultUsageCodeFixProvider>;

namespace YAESandBox.Analyzers.Tests;

// 1. 创建一个我们自己的测试基类，它继承了 CSharpCodeFixTest
//    这给了我们对测试过程的完全控制权
public abstract class ResultUsageVerifier : CSharpCodeFixTest<ResultUsageAnalyzer, ResultUsageCodeFixProvider, XUnitVerifier>
{
    // 2. 将 FluentResults 的源码定义移到这里
    private const string FluentResultsSource = """
                                               namespace FluentResults
                                               {
                                                   public interface IResult { bool IsSuccess { get; } }
                                                   public class Result : IResult 
                                                   {
                                                       public bool IsSuccess { get; }
                                                       public static Result Ok() => new();
                                                       public static Result<T> Ok<T>(T value) => new();
                                                   }
                                                   public class Result<T> : Result, IResult { }
                                               }
                                               """;

    // 3. 在构造函数中，将 FluentResults.cs 作为额外的源文件添加到每个测试中
    protected ResultUsageVerifier()
    {
        // 这就是解决问题的关键：通过 TestState.Sources 添加额外的文件
        // 所有继承这个类的测试都会自动包含 FluentResults.cs
        this.TestState.Sources.Add(("FluentResults.cs", FluentResultsSource));
    }
}

public class ResultUsageAnalyzerTests : ResultUsageVerifier
{
    // 注意：FluentResultsSource 已经移到了基类中

    [Fact(DisplayName = "分析器测试：当返回值被赋值给变量时，不应报告诊断")]
    public async Task WhenResultIsAssignedToVariable_ShouldNotReportDiagnostic()
    {
        // 现在不再调用静态方法，而是直接设置当前测试实例的属性
        this.TestCode = """
        using FluentResults;
        
        public class TestClass
        {
            public Result MyMethod() => Result.Ok();
            
            public void Usage()
            {
                var r = MyMethod();
            }
        }
        """;

        // 因为我们期望没有诊断，所以不需要设置 ExpectedDiagnostics
        // 直接运行测试
        await this.RunAsync();
    }

    [Fact(DisplayName = "代码修复测试：对未使用的同步 Result 调用提供 '赋值给弃元' 修复")]
    public async Task ForUnusedSyncResult_ShouldReportDiagnosticAndOfferFix()
    {
        this.TestCode = """
        using FluentResults;
        
        public class TestClass
        {
            public Result MyMethod() => Result.Ok();
            
            public void Usage()
            {
                {|#0:MyMethod()|};
            }
        }
        """;

        this.FixedCode = """
        using FluentResults;
        
        public class TestClass
        {
            public Result MyMethod() => Result.Ok();
            
            public void Usage()
            {
                _ = MyMethod();
            }
        }
        """;

        // 设置期望的诊断
        this.ExpectedDiagnostics.Add(
            DiagnosticResult.CompilerError("RU0001") // 或者用 ResultUsageAnalyzer.DiagnosticId
                .WithLocation(0)
                .WithArguments("MyMethod")
        );
        
        // 运行测试
        await this.RunAsync();
    }

    [Fact(DisplayName = "代码修复测试：对返回继承自 Result 的类型的调用提供修复")]
    public async Task ForUnusedInheritedResult_ShouldReportDiagnosticAndOfferFix()
    {
        // CustomResult 的定义只存在于这个测试的代码中
        this.TestCode = """
        using FluentResults;
        
        public class CustomResult : FluentResults.Result { }

        public class TestClass
        {
            public CustomResult GetCustomResult() => new();
            
            public void Usage()
            {
                {|#0:GetCustomResult()|};
            }
        }
        """;

        this.FixedCode = """
        using FluentResults;
        
        public class CustomResult : FluentResults.Result { }
        
        public class TestClass
        {
            public CustomResult GetCustomResult() => new();
            
            public void Usage()
            {
                _ = GetCustomResult();
            }
        }
        """;

        this.ExpectedDiagnostics.Add(
            DiagnosticResult.CompilerError("RU0001")
                .WithLocation(0)
                .WithArguments("GetCustomResult")
        );

        await this.RunAsync();
    }
}