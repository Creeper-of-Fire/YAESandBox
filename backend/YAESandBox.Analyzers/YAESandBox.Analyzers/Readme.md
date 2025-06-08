# YAESandBox Roslyn 分析器示例

这是一个包含 Roslyn 分析器和对应代码修复器的示例项目。你可以基于这个模板进行学习，或修改它来满足你自己的需求。

## 项目内容

### 1. YAESandBox.Analyzers

这是一个 **.NET** 项目，其中包含了我们自定义的分析器和代码修复器的实现。
**你必须先成功生成此项目，才能在 IDE 中看到分析器给出的诊断警告。**

- **`ResultUsageAnalyzer.cs`**: 一个分析器，用于检查所有返回 `Result` 或 `Result<T>` 类型（包括其派生类型和泛型包装，如 `Task<Result>`
  ）的方法。如果返回值未被使用（既未赋值给变量，也未被显式丢弃），它会报告一个错误 `RU0001`。这可以有效防止开发者忽略可能包含错误信息的结果。
- **`ResultUsageCodeFixProvider.cs`**: 一个代码修复程序，与 `ResultUsageAnalyzer.cs` 配套使用。当 `RU0001` 错误出现时，它会提供一个快速修复选项，自动在未使用的返回值前添加
  `_ = `，将其显式丢弃，从而消除编译错误。

### 2. YAESandBox.Analyzers.Sample

这是一个示例项目，它引用了上面定义的分析器。请注意 `YAESandBox.Analyzers.Sample.csproj` 文件中 `ProjectReference`
的特殊参数设置，这确保了项目被作为一套分析器来引用，而不是普通的库。

### 3. YAESandBox.Analyzers.Tests

针对我们编写的分析器和代码修复器的单元测试项目。开发语言相关功能时，从单元测试入手通常是最简单、最高效的方式。
目前里面的代码有问题，别动。

## 常见问题 (How To?)

### 如何调试分析器?

- 直接使用项目自带的 `Properties/launchSettings.json` 调试配置来启动一个附加了调试器的 IDE 实例。
- 编写并调试单元测试。这是验证分析器逻辑最快的方法。

### 如何知道我应该处理哪种语法节点?

- 强烈推荐使用 **Roslyn Syntax Visualizer** (Roslyn 语法可视化工具)。在 Visual Studio 中，可以通过
  `View -> Other Windows -> Roslyn Syntax Visualizer` 打开。它可以让你实时观察代码对应的语法树结构，帮助你精确找到需要分析的节点类型。

### 在哪里可以学到更多关于分析器的知识?

- 最全面、最权威的资料都可以在 [Roslyn 的 GitHub Wiki](https://github.com/dotnet/roslyn/blob/main/docs/wiki/README.md) 中找到。