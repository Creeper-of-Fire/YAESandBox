using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace YAESandBox.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RequireRecordImplementationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "RR0001";
    private const string Title = "接口实现者必须是 record 类型";
    private const string MessageFormat = "类型 '{0}' 实现了被 [RequireRecordImplementation] 标记的接口 '{1}'，因此它必须被声明为 'record'。";
    private const string Description = "为了确保数据契约的一致性和不可变性，某些接口要求其实现类型必须是 record。";
    private const string Category = "Design";

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
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        // 我们只关心 class 和 struct 类型，因为 record 本身就符合要求
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
        if (namedTypeSymbol.TypeKind != TypeKind.Class && namedTypeSymbol.TypeKind != TypeKind.Struct)
        {
            return;
        }

        // 如果这个类型本身就是 record，那它肯定是合规的
        if (namedTypeSymbol.IsRecord)
        {
            return;
        }
        
        var requiredAttributeSymbol = context.Compilation.GetTypeByMetadataName("YAESandBox.Depend.Annotations.RequireRecordImplementationAttribute");

        // 遍历该类型实现的所有接口
        foreach (var implementedInterface in namedTypeSymbol.AllInterfaces)
        {
            // 检查接口是否有我们定义的那个属性
            bool hasAttribute = implementedInterface.GetAttributes()
                .Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, requiredAttributeSymbol));

            if (hasAttribute)
            {
                // 找到了一个违规的实现！报告错误。
                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name, implementedInterface.Name);
                context.ReportDiagnostic(diagnostic);
                // 找到一个就够了，不用继续检查其他接口了
                return;
            }
        }
    }
}