using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace YAESandBox.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExceptionUsageCodeFixProvider)), Shared]
public class ExceptionUsageCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(ExceptionUsageAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        
        // 从诊断位置找到对应的 CatchClauseSyntax 节点
        var catchClause = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<CatchClauseSyntax>().FirstOrDefault();
        if (catchClause == null) return;
        
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "添加 'throw;' 以重新抛出异常",
                createChangedDocument: c => this.AddRethrowAsync(context.Document, catchClause, c),
                equivalenceKey: "AddRethrow"),
            diagnostic);
    }

    private async Task<Document> AddRethrowAsync(Document document, CatchClauseSyntax catchClause, CancellationToken cancellationToken)
    {
        // 1. 创建一个新的 `throw;` 语句
        //    添加 Formatter.Annotation 以确保代码格式正确
        var rethrowStatement = SyntaxFactory.ThrowStatement()
            .WithAdditionalAnnotations(Formatter.Annotation);

        // 2. 获取旧的 catch 块并添加新语句
        var oldBlock = catchClause.Block;
        var newStatements = oldBlock.Statements.Add(rethrowStatement);
        var newBlock = oldBlock.WithStatements(newStatements);

        // 3. 创建一个新的 catch 子句来替换旧的
        var newCatchClause = catchClause.WithBlock(newBlock);

        // 4. 使用 DocumentEditor 执行替换
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        editor.ReplaceNode(catchClause, newCatchClause);
        
        return editor.GetChangedDocument();
    }
}