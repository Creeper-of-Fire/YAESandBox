// 文件: YAESandBox.Analyzers/ResultUsageCodeFixProvider.cs

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Rename;

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
                createChangedDocument: c => AddDiscardAssignmentAsync(context.Document, expressionStatement, c),
                equivalenceKey: "AddDiscardAssignment"),
            diagnostic);

        // --- 修复选项 2: 声明为局部变量并重命名 ---
        context.RegisterCodeFix(
            CodeAction.Create(
                // 标题可以更明确一点
                title: "声明为局部变量(var result)并重命名(?)...",
                // 注意：createChangedSolution 而不是 createChangedDocument
                createChangedSolution: c => IntroduceLocalVariableAndRenameAsync(context.Document, expressionStatement, c),
                equivalenceKey: "IntroduceLocalVariableAndRename"),
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
    private async Task<Solution> IntroduceLocalVariableAndRenameAsync(Document document, ExpressionStatementSyntax oldStatement,
        CancellationToken cancellationToken)
    {
        // --- 第一阶段: 创建并应用修改 ---

        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        var cleanExpression = oldStatement.Expression.WithoutLeadingTrivia();
        var tempVariableName = "newResult"; // 保持使用临时名称

        var variableDeclaration = SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
            .AddVariables(
                SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(tempVariableName))
                    .WithInitializer(SyntaxFactory.EqualsValueClause(cleanExpression))
            );

        var newStatement = SyntaxFactory.LocalDeclarationStatement(variableDeclaration)
            .WithTriviaFrom(oldStatement);

        editor.ReplaceNode(oldStatement, newStatement);

        // 从编辑器获取包含所有修改的【新文档】
        var changedDocument = editor.GetChangedDocument();

        // --- 第二阶段: 在新文档上进行语义分析和重命名 ---

        // 从【新文档】获取【新的】语法树根节点
        var newRoot = await changedDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (newRoot is null) return document.Project.Solution;

        // 从【新文档】获取【新的】语义模型
        var newSemanticModel = await changedDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (newSemanticModel is null) return document.Project.Solution;

        // 在新的根节点中，找到我们刚刚创建的那个变量声明节点
        // oldStatement.Span 依然可以用来定位修改后新节点的位置
        var newVariableDeclarator = newRoot.FindNode(oldStatement.Span).DescendantNodesAndSelf().OfType<VariableDeclaratorSyntax>().First();

        // 使用【新的】语义模型来获取符号信息
        var variableSymbol = newSemanticModel.GetDeclaredSymbol(newVariableDeclarator, cancellationToken);

        if (variableSymbol is null)
        {
            // 如果符号获取失败，返回修改后的文档，至少基础功能是好的
            return changedDocument.Project.Solution;
        }

        // --- 第三阶段: 调用重命名服务 ---

        // 使用 changedDocument.Project.Solution 作为基础来应用重命名
        var finalSolution = await Renamer.RenameSymbolAsync(
            changedDocument.Project.Solution,
            variableSymbol,
            new SymbolRenameOptions(),
            "result", // 新名字的默认值
            cancellationToken
        ).ConfigureAwait(false);

        return finalSolution;
    }
}