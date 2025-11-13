using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace YAESandBox.Analyzers;

#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public class ExceptionUsageAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "EU0001";
    private const string Title = "异常对象必须被完整使用";
    private const string MessageFormat = "在 catch 块中声明的异常变量未被传递或完整传递。只使用其属性（如.Message）会丢失堆栈信息。";
    private const string Description = "为了保留完整的错误上下文（包括堆栈跟踪和内部异常），捕获到的异常对象应该被完整地传递给日志记录器、Result 类型或通过 'throw;' 重新抛出。";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId, Title, MessageFormat, Category,
#pragma warning disable RS1033
        DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);
#pragma warning restore RS1033

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(this.AnalyzeCatchClause, SyntaxKind.CatchClause);
    }

    private void AnalyzeCatchClause(SyntaxNodeAnalysisContext context)
    {
        var catchClause = (CatchClauseSyntax)context.Node;

        // 1. 规则仅适用于声明了异常变量的 catch 块, e.g., `catch (Exception ex)`
        if (catchClause.Declaration is null || catchClause.Declaration.Identifier.IsKind(SyntaxKind.None))
        {
            return;
        }

        // 2. 如果 catch 块中包含 `throw;` 语句，这是一个有效的处理方式，直接返回。
        if (catchClause.Block.Statements.OfType<ThrowStatementSyntax>().Any(ts => ts.Expression is null))
        {
            return;
        }

        var semanticModel = context.SemanticModel;
        var exceptionSymbol = semanticModel.GetDeclaredSymbol(catchClause.Declaration);
        if (exceptionSymbol is null)
        {
            return;
        }

        // 3. 查找所有使用该异常变量的地方
        var dataFlowAnalysis = semanticModel.AnalyzeDataFlow(catchClause.Block);
        if (dataFlowAnalysis is not null && !dataFlowAnalysis.ReadInside.Contains(exceptionSymbol))
        {
            // 变量根本没被读取，报告错误
            this.ReportDiagnostic(context, catchClause.Declaration);
            return;
        }

        // 4. 检查所有用法，确定是否至少有一次是“完整使用”
        var identifierNodes = catchClause.Block.DescendantNodes().OfType<IdentifierNameSyntax>();
        bool hasValidUsage = false;

        foreach (var identifier in identifierNodes)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(identifier, context.CancellationToken);
            if (!SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, exceptionSymbol))
            {
                continue; // 这不是我们关心的那个异常变量
            }

            // 关键逻辑：检查这个标识符的父节点。
            // 如果父节点是 MemberAccessExpression，并且该标识符是表达式的左侧部分
            // (e.g., the `ex` in `ex.Message`), 那么这不是一个“完整使用”。
            // 否则，它很可能被用作方法参数，这是一个“完整使用”。
            if (identifier.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Expression == identifier)
            {
                // 如果该成员访问的父节点是方法调用 `ex.Something()`，则视为有效使用
                // 这会正确处理 `ex.ToString()` 和 `ex.ToFormattedString()` 等扩展方法
                if (memberAccess.Parent is InvocationExpressionSyntax)
                {
                    hasValidUsage = true;
                    break;
                }
                
                // 否则，这只是一个属性访问 `ex.Message`，我们继续寻找其他有效用法
                continue;
            }
            
            // 找到了一个“完整使用”（例如作为参数传递），分析可以结束了。
            hasValidUsage = true;
            break;
        }

        // 5. 如果遍历完所有用法，都没有找到一个“完整使用”，则报告错误。
        if (!hasValidUsage)
        {
            this.ReportDiagnostic(context, catchClause.Declaration);
        }
    }

    private void ReportDiagnostic(SyntaxNodeAnalysisContext context, CatchDeclarationSyntax declaration)
    {
        var diagnostic = Diagnostic.Create(Rule, declaration.Identifier.GetLocation(), declaration.Identifier.Text);
        context.ReportDiagnostic(diagnostic);
    }
}