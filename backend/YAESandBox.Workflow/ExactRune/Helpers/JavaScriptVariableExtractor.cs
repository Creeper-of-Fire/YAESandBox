using Esprima.Ast;
using Esprima.Utils;

namespace YAESandBox.Workflow.ExactRune.Helpers;

/// <summary>
/// 一个 AST 访问者，用于从 Esprima 的语法树中提取自由变量标识符。
/// </summary>
internal class JavaScriptVariableExtractor : AstVisitor
{
    public ISet<string> Identifiers { get; } = new HashSet<string>();
    private Stack<ISet<string>> ScopeStack { get; } = new();

    // 排除已知的JS全局变量和关键字
    private static readonly ISet<string> KnownGlobals = new HashSet<string>
    {
        "true", "false", "null", "undefined", "NaN", "Infinity",
        "Object", "Array", "String", "Number", "Boolean", "Function",
        "Math", "JSON", "Date", "RegExp", "Error", "Symbol",
        "console", "window", "document"
    };

    public JavaScriptVariableExtractor()
    {
        // 推入全局作用域，用于存放顶级声明
        this.ScopeStack.Push(new HashSet<string>());
    }

    /// <summary>
    /// 这是核心修正。我们接管对成员表达式的访问。
    /// 对于 "player.level"，我们只对 "player" (Object) 感兴趣，而忽略 "level" (Property)。
    /// </summary>
    protected override object? VisitMemberExpression(MemberExpression memberExpression)
    {
        // 只访问表达式的 "Object" 部分，不访问 "Property" 部分。
        // 这就阻止了 "level" 被错误地识别为一个独立的变量。
        this.Visit(memberExpression.Object);
        return memberExpression;
    }

    /// <summary>
    /// 当访问到一个标识符时，判断它是否是一个需要从外部注入的自由变量。
    /// </summary>
    protected override object? VisitIdentifier(Identifier identifier)
    {
        string name = identifier.Name;

        // 如果不是全局变量，并且在当前作用域链的任何层级都找不到它的声明
        if (!KnownGlobals.Contains(name) && !this.ScopeStack.Any(scope => scope.Contains(name)))
        {
            this.Identifiers.Add(name);
        }

        return base.VisitIdentifier(identifier);
    }
    
    // --- 作用域管理 ---

    private void EnterScope(IFunction function)
    {
        var newScope = new HashSet<string>();

        // 将函数名（如果有）添加到新作用域
        if (function.Id is not null)
        {
            // 函数名在其自身内部是可见的
            newScope.Add(function.Id.Name);
        }
        
        // 将所有参数添加到新作用域
        foreach (var param in function.Params)
        {
            if (param is Identifier paramIdentifier)
            {
                newScope.Add(paramIdentifier.Name);
            }
            // 可以扩展以处理更复杂的参数，如解构赋值
        }
        this.ScopeStack.Push(newScope);
    }

    private void ExitScope()
    {
        this.ScopeStack.Pop();
    }

    /// <summary>
    /// 当遇到一个变量声明时，将其名称添加到当前作用域。
    /// </summary>
    protected override object? VisitVariableDeclarator(VariableDeclarator variableDeclarator)
    {
        // 将声明的变量 (如 `var x = ...` 中的 `x`) 添加到当前作用域
        if (variableDeclarator.Id is Identifier id)
        {
            this.ScopeStack.Peek().Add(id.Name);
        }
        
        // 继续访问初始化表达式 (如 `var x = y` 中的 `y`)
        if (variableDeclarator.Init is not null)
        {
            this.Visit(variableDeclarator.Init);
        }

        return variableDeclarator;
    }
    
    // --- 下面的方法负责在进入和退出函数时，正确地管理作用域栈 ---

    protected override object? VisitFunctionDeclaration(FunctionDeclaration functionDeclaration)
    {
        // 函数声明的名字在父作用域和自身作用域都可见
        if (functionDeclaration.Id is not null)
        {
            this.ScopeStack.Peek().Add(functionDeclaration.Id.Name);
        }

        this.EnterScope(functionDeclaration);
        this.Visit(functionDeclaration.Body);
        this.ExitScope();
        return functionDeclaration;
    }

    protected override object? VisitFunctionExpression(FunctionExpression functionExpression)
    {
        this.EnterScope(functionExpression);
        this.Visit(functionExpression.Body);
        this.ExitScope();
        return functionExpression;
    }

    protected override object? VisitArrowFunctionExpression(ArrowFunctionExpression arrowFunctionExpression)
    {
        this.EnterScope(arrowFunctionExpression);
        this.Visit(arrowFunctionExpression.Body);
        this.ExitScope();
        return arrowFunctionExpression;
    }
}