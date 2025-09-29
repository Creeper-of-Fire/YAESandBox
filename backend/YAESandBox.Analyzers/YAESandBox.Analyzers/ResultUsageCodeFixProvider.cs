using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace YAESandBox.Analyzers;
// <-- 注意这里的命名空间

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ResultUsageCodeFixProvider)), Shared]
public class ResultUsageCodeFixProvider : CodeFixProvider
{
    // 确保这里的 ID 与你的分析器 ID 匹配
    public sealed override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(ResultUsageAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        // *** 已修正: 直接查找 ExpressionStatementSyntax ***
        // 从诊断位置向上查找包含它的整个表达式语句，这是我们要替换的节点。
        var expressionStatement = root.FindNode(diagnosticSpan).AncestorsAndSelf().OfType<ExpressionStatementSyntax>().FirstOrDefault();

        if (expressionStatement is null) return;

        // --- 修复选项 1: 使用弃元 `_` 接收 ---
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "使用弃元' _ '接收返回值",
                // 将整个语句传给修复方法
                createChangedDocument: c => this.AddDiscardAssignmentAsync(context.Document, expressionStatement, c),
                equivalenceKey: "AddDiscardAssignment"),
            diagnostic);

        // --- 修复选项 2: 声明为局部变量 ---
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "声明为局部变量 (var result = ...)",
                createChangedDocument: c => this.IntroduceLocalVariableAsync(context.Document, expressionStatement, c),
                equivalenceKey: "IntroduceLocalVariable"),
            diagnostic);
    }

    // --- 已修正并简化的方法 1 ---
    private async Task<Document> AddDiscardAssignmentAsync(Document document, ExpressionStatementSyntax oldStatement,
        CancellationToken cancellationToken)
    {
        // *** 关键修正 ***
        // 1. 获取一个移除了前导 Trivia (注释等) 的干净表达式。
        var cleanExpression = oldStatement.Expression.WithoutLeadingTrivia();

        // 2. 用干净的表达式构建新的赋值语句。
        var discardAssignment = SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.IdentifierName("_"),
            cleanExpression);

        // 3. 将旧语句的所有 Trivia (包括注释) 应用到新语句的最外层。
        var newStatement = SyntaxFactory.ExpressionStatement(discardAssignment)
            .WithTriviaFrom(oldStatement);

        // 4. 执行替换。
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        editor.ReplaceNode(oldStatement, newStatement);
        return editor.GetChangedDocument();
    }

    // --- 已修正并简化的方法 2 ---
    private async Task<Document> IntroduceLocalVariableAsync(Document document, ExpressionStatementSyntax oldStatement,
        CancellationToken cancellationToken)
    {
        // *** 逻辑与上面完全对称 ***
        // 1. 获取一个移除了前导 Trivia 的干净表达式。
        var cleanExpression = oldStatement.Expression.WithoutLeadingTrivia();

        // 2. 用干净的表达式构建新的变量声明语句。
        var variableDeclaration = SyntaxFactory.VariableDeclaration(
                SyntaxFactory.IdentifierName("var"))
            .AddVariables(
                SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier("result"))
                    .WithInitializer(SyntaxFactory.EqualsValueClause(cleanExpression))
            );

        // 3. 将旧语句的所有 Trivia 应用到新语句的最外层。
        var newStatement = SyntaxFactory.LocalDeclarationStatement(variableDeclaration)
            .WithTriviaFrom(oldStatement);

        // 4. 执行替换。
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        editor.ReplaceNode(oldStatement, newStatement);
        return editor.GetChangedDocument();
    }
}