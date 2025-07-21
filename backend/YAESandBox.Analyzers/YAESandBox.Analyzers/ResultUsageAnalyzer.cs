using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace YAESandBox.Analyzers;

#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public class ResultUsageAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "RU0001";
    private const string Title = "包含Result的返回值必须被使用";
    private const string MessageFormat = "方法 '{0}' 的返回值包含 'Result' 类型但未被使用，这可能导致错误路径被忽略";
    private const string Description = "所有返回 'Result', 'Result<T>', 或包含这些类型的泛型（如 Task<Result>）的方法，其返回值都应该被处理。.";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId, Title, MessageFormat, Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            const string resultFullName = "YAESandBox.Depend.Results.Result";
            const string genericResultFullName = "YAESandBox.Depend.Results.Result`1";


            var resultSymbol = compilationContext.Compilation.GetTypeByMetadataName(resultFullName);
            var genericResultSymbol = compilationContext.Compilation.GetTypeByMetadataName(genericResultFullName);

            // 如果在用户的编译环境中找不到这些类型，说明用户项目根本没用 Results，
            // 我们的分析器就没必要工作了。
            if (resultSymbol is null || genericResultSymbol is null)
            {
                return;
            }

            // *** 已修正: 监听 ExpressionStatement 而不是 InvocationExpression ***
            // 这种方法更健壮，可以统一处理 M(); 和 await MAsync(); 等情况。
            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => this.AnalyzeExpressionStatement(nodeContext, resultSymbol, genericResultSymbol),
                SyntaxKind.ExpressionStatement);
        });
    }

    private void AnalyzeExpressionStatement(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol resultSymbol,
        INamedTypeSymbol genericResultSymbol)
    {
        var expressionStatement = (ExpressionStatementSyntax)context.Node;
        var expression = expressionStatement.Expression;

        // *** 已修正逻辑 ***
        // 如果表达式语句的内容是一个赋值操作 (例如 a = b, _ = M())，
        // 那么返回值肯定是被使用了。我们应该立即忽略它。
        if (expression is AssignmentExpressionSyntax)
        {
            return;
        }

        // 我们只关心那些作为语句独立存在的调用，例如 M(); 或 await MAsync();
        // 检查表达式的核心是否为一个方法调用。
        var invocationExpr = (expression as InvocationExpressionSyntax) ??
                             ((expression as AwaitExpressionSyntax)?.Expression as InvocationExpressionSyntax);

        if (invocationExpr is null)
        {
            // 如果不是我们关心的调用模式（比如 i++;），就忽略。
            return;
        }

        // 获取最外层表达式的类型信息 (await 会解包 Task<T> 为 T)
        var typeSymbol = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type;
        if (typeSymbol is null) return;

        if (this.ContainsResultTypeRecursive(typeSymbol, resultSymbol, genericResultSymbol,
                new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)))
        {
            var methodSymbol = context.SemanticModel.GetSymbolInfo(invocationExpr).Symbol as IMethodSymbol;
            string methodName = methodSymbol?.Name ?? "未知方法";

            // 报告的诊断位置应该是整个表达式，以便代码修复器替换。
            var diagnostic = Diagnostic.Create(Rule, expression.GetLocation(), methodName);
            context.ReportDiagnostic(diagnostic);
        }
    }


    // 递归检查的辅助方法，这个方法不需要改变
    // *** 已修正: 增加对继承链的检查 ***
    private bool ContainsResultTypeRecursive(
        ITypeSymbol? typeSymbol,
        INamedTypeSymbol resultSymbol,
        INamedTypeSymbol genericResultSymbol,
        HashSet<ITypeSymbol> visitedSymbols)
    {
        // 防止无限递归和重复检查
        if (typeSymbol is null || !visitedSymbols.Add(typeSymbol)) return false;

        // 检查1：类型本身或其泛型定义是否匹配
        var originalDefinition = typeSymbol.OriginalDefinition;
        if (SymbolEqualityComparer.Default.Equals(originalDefinition, resultSymbol) ||
            SymbolEqualityComparer.Default.Equals(originalDefinition, genericResultSymbol))
        {
            return true;
        }

        // 检查2：递归检查泛型参数
        if (typeSymbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType)
        {
            foreach (var typeArgument in namedTypeSymbol.TypeArguments)
            {
                if (this.ContainsResultTypeRecursive(typeArgument, resultSymbol, genericResultSymbol, visitedSymbols))
                {
                    return true;
                }
            }
        }

        // 检查3：递归检查基类 (这是本次修正的关键！)
        if (typeSymbol.BaseType is not null &&
            this.ContainsResultTypeRecursive(typeSymbol.BaseType, resultSymbol, genericResultSymbol, visitedSymbols))
        {
            return true;
        }

        return false;
    }
}