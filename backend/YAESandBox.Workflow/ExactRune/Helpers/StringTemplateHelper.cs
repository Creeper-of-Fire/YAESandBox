using System.Text.RegularExpressions;
using YAESandBox.Workflow.Core.VarSpec;
using static YAESandBox.Workflow.Core.Runtime.Processor.TuumProcessor;

namespace YAESandBox.Workflow.ExactRune.Helpers;

/// <summary>
/// 字符串模板渲染器。
/// 负责解析字符串中的 {{placeholder}} 并从上下文中填充值。
/// </summary>
internal static partial class StringTemplateHelper
{
    private const string Path = "path";

    // 使用 Regex.Matches 获取所有唯一的占位符名称
    // 使用 lookahead 和 lookbehind 来确保我们只匹配 {{}} 包裹的内容，并且不能处理嵌套 {{}} 的情况
    // TODO 实现嵌套等操作？
    [GeneratedRegex($$"""\{\{(?<{{Path}}>[^}]+?)\}\}""")]
    public static partial Regex PlaceholderRegex();

    /// <summary>
    /// 通用渲染方法。
    /// </summary>
    /// <param name="template">模板字符串。</param>
    /// <param name="tuumContent">上下文内容。</param>
    /// <param name="resolvedStore">用于存储解析成功的字典 (可选)。</param>
    /// <param name="unresolvedStore">用于存储解析失败的列表 (可选)。</param>
    /// <param name="logAction">用于记录详细日志的回调 (可选)。</param>
    /// <returns>渲染后的字符串。</returns>
    public static string Render(
        string? template,
        TuumProcessorContent tuumContent,
        Dictionary<string, string>? resolvedStore = null,
        List<string>? unresolvedStore = null,
        Action<string>? logAction = null)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        return PlaceholderRegex().Replace(template, match =>
        {
            string path = match.Groups[Path].Value.Trim();

            if (tuumContent.TryGetTuumVarByPath<object>(path, out object? value))
            {
                string stringValue = value.ToString() ?? string.Empty;
                resolvedStore?.TryAdd(path, stringValue); // 避免重复键报错
                return stringValue;
            }

            // 记录未解析项
            if (unresolvedStore != null && !unresolvedStore.Contains(path))
            {
                unresolvedStore.Add(path);
                logAction?.Invoke($$$"""占位符 '{{{{{path}}}}}' 未找到或其值为 null，已替换为空字符串。""");
            }

            return string.Empty;
        });
    }
    
    /// <summary>
    /// 通用推断逻辑。
    /// 根据提取出的占位符路径，推断出 ConsumedSpec 列表。
    /// </summary>
    /// <param name="placeholders">占位符路径列表。</param>
    /// <param name="additionalSpecs">额外的 Spec (例如输出变量或固定依赖)。</param>
    /// <returns>完整的 ConsumedSpec 列表。</returns>
    public static List<ConsumedSpec> InferConsumedSpecs(IEnumerable<string> placeholders, IEnumerable<ConsumedSpec>? additionalSpecs = null)
    {
        var rootSpecs = new Dictionary<string, VarSpecDef>();

        foreach (string path in placeholders)
        {
            string[] parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            string rootVarName = parts[0];

            // 1. 顶层变量
            if (parts.Length == 1)
            {
                if (!rootSpecs.ContainsKey(rootVarName))
                {
                    rootSpecs[rootVarName] = CoreVarDefs.Any with { Description = "可被ToString的任意类型。" };
                }
                continue;
            }

            // 2. 嵌套变量 (构建 Record 树)
            if (!rootSpecs.TryGetValue(rootVarName, out var currentDef) || currentDef is not RecordVarSpecDef)
            {
                currentDef = CoreVarDefs.RecordStringAny with
                {
                    Properties = [],
                    Description = $"根据模板为变量'{rootVarName}'推断出的数据结构。"
                };
                rootSpecs[rootVarName] = currentDef;
            }

            var currentRecord = (RecordVarSpecDef)currentDef;

            for (int i = 1; i < parts.Length; i++)
            {
                string propName = parts[i];
                bool isLeaf = (i == parts.Length - 1);

                if (isLeaf)
                {
                    if (!currentRecord.Properties.ContainsKey(propName))
                    {
                        currentRecord.Properties[propName] = CoreVarDefs.Any with { Description = "可被ToString的任意类型。" };
                    }
                }
                else
                {
                    if (!currentRecord.Properties.TryGetValue(propName, out var nextDef) || nextDef is not RecordVarSpecDef)
                    {
                        nextDef = CoreVarDefs.RecordStringAny with
                        {
                            Properties = [],
                            Description = $"为'{propName}'推断出的嵌套数据结构。"
                        };
                        currentRecord.Properties[propName] = nextDef;
                    }
                    currentRecord = (RecordVarSpecDef)nextDef;
                }
            }
        }

        var result = rootSpecs.Select(kvp => new ConsumedSpec(kvp.Key, kvp.Value)).ToList();
        
        if (additionalSpecs != null)
        {
            result.AddRange(additionalSpecs);
        }

        return result;
    }

    /// <summary>
    /// 从多个模板源中提取所有唯一的占位符路径。
    /// </summary>
    public static IEnumerable<string> ExtractPlaceholders(params string?[] templates)
    {
        var allPaths = new HashSet<string>();
        foreach (var template in templates)
        {
            if (string.IsNullOrEmpty(template)) continue;

            var matches = PlaceholderRegex().Matches(template);
            foreach (Match match in matches)
            {
                allPaths.Add(match.Groups[Path].Value.Trim());
            }
        }

        return allPaths;
    }
}