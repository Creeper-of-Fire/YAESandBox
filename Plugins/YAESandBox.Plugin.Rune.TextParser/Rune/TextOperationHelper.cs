using System.Text.Json;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.AIService;

namespace YAESandBox.Plugin.Rune.TextParser.Rune;

/// <summary>
/// 一个数据传输对象，用于封装文本处理操作的多种可能输入类型。
/// 它可以代表一个简单的字符串，或是一个结构化的提示词列表。
/// 使用静态工厂方法 <see cref="FromString"/> 和 <see cref="FromPromptList"/> 来创建实例。
/// </summary>
public sealed class TextProcessingInput
{
    /// <summary>
    /// 当输入为普通字符串时，此属性包含该字符串。
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// 当输入为提示词列表时，此属性包含该列表。
    /// </summary>
    public List<RoledPromptDto>? Prompts { get; }

    /// <summary>
    /// 指示当前输入是否为字符串。
    /// </summary>
    public bool IsString => Text is not null;

    /// <summary>
    /// 指示当前输入是否为提示词列表。
    /// </summary>
    public bool IsPromptList => Prompts is not null;

    // 私有构造函数，强制使用静态工厂方法
    private TextProcessingInput(string? text, List<RoledPromptDto>? prompts)
    {
        Text = text;
        Prompts = prompts;
    }

    /// <summary>
    /// 从一个字符串创建输入对象。
    /// </summary>
    public static TextProcessingInput FromString(string? text) => new(text, null);

    /// <summary>
    /// 从一个提示词列表创建输入对象。
    /// </summary>
    public static TextProcessingInput FromPromptList(List<RoledPromptDto>? prompts) => new(null, prompts);
}

/// <summary>
/// 提供通用的文本提取与替换操作的辅助方法。
/// 这个类抽象了“提取”和“替换”的通用流程，具体的实现逻辑通过委托传入。
/// </summary>
public static class TextOperationHelper
{
    /// <summary>
    /// 【主入口】执行文本处理操作，根据输入类型和操作模式选择提取或替换。
    /// </summary>
    /// <param name="input">封装了输入数据（字符串或PromptList）的DTO。</param>
    /// <param name="operationMode">操作模式（提取或替换）。</param>
    /// <param name="extractor">提取逻辑委托：接收输入字符串，返回提取出的字符串列表。</param>
    /// <param name="replacer">替换逻辑委托：接收输入字符串，返回完成替换后的新字符串。</param>
    /// <param name="returnFormat">输出格式枚举。仅在提取模式下生效。</param>
    /// <returns>处理后的结果。根据输入类型和操作模式，可能是 string, List&lt;string&gt;, JSON string 或 List&lt;RoledPromptDto&gt;。</returns>
    public static object Process(
        TextProcessingInput input,
        OperationModeEnum operationMode,
        Func<string, List<string>> extractor,
        Func<string, string> replacer,
        ReturnFormatEnum returnFormat)
    {
        if (input.IsString)
            return ProcessSingleString(input.Text, operationMode, extractor, replacer, returnFormat);

        if (input.IsPromptList)
        {
            var prompts = input.Prompts ?? [];
            if (operationMode == OperationModeEnum.Extract)
            {
                // 提取模式：遍历所有prompt的Content，将所有提取结果聚合到一个列表中
                var allExtractedValues = prompts
                    .SelectMany(p => extractor(p.Content))
                    .ToList();
                return FormatOutput(allExtractedValues, returnFormat);
            }
            else // OperationModeEnum.Replace
            {
                // 替换模式：遍历所有prompt，对每个Content应用替换，返回一个新的PromptList
                var newPrompts = prompts
                    .Select(p => p with { Content = replacer(p.Content) })
                    .ToList();
                return newPrompts;
            }
        }

        // 默认处理时，调用字符串处理逻辑
        return ProcessSingleString(input.Text, operationMode, extractor, replacer, returnFormat);
    }

    /// <summary>
    /// 专门处理单个字符串的提取与替换逻辑。
    /// </summary>
    private static object ProcessSingleString(
        string? inputText,
        OperationModeEnum operationMode,
        Func<string, List<string>> extractor,
        Func<string, string> replacer,
        ReturnFormatEnum returnFormat)
    {
        // 对空或空白输入的统一处理
        if (string.IsNullOrWhiteSpace(inputText))
        {
            return operationMode == OperationModeEnum.Replace
                ? string.Empty // 替换模式下，输入为空，输出也为空字符串
                : FormatOutput([], returnFormat); // 提取模式下，输入为空，输出为空结果的格式化形式
        }

        // 根据操作模式调用相应的委托
        if (operationMode == OperationModeEnum.Extract)
        {
            var extractedValues = extractor(inputText);
            return FormatOutput(extractedValues, returnFormat);
        }
        else // OperationModeEnum.Replace
        {
            return replacer(inputText);
        }
    }

    /// <summary>
    /// 根据指定的返回格式，将字符串列表转换为最终的输出对象。
    /// </summary>
    /// <param name="values">提取出的原始字符串列表。</param>
    /// <param name="format">目标输出格式。</param>
    /// <returns>格式化后的对象。</returns>
    public static object FormatOutput(List<string> values, ReturnFormatEnum format)
    {
        return format switch
        {
            ReturnFormatEnum.First => values.FirstOrDefault() ?? string.Empty,
            ReturnFormatEnum.AsList => values,
            ReturnFormatEnum.AsJsonString => JsonSerializer.Serialize(values, YaeSandBoxJsonHelper.JsonSerializerOptions),
            _ => string.Empty
        };
    }
}