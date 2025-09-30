using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Rune.Interface;
using YAESandBox.Workflow.Runtime;

namespace YAESandBox.Workflow.Rune.Config;

/// <summary>
/// 一个特殊的符文配置，用于表示在反序列化过程中未能成功解析的符文。
/// 它会捕获原始的JSON数据和解析错误，允许用户在UI中查看和修复。
/// </summary>
[ClassLabel("❓未知符文")]
[RenderWithCustomWidget("UnknownRuneEditor")]
public sealed record UnknownRuneConfig : AbstractRuneConfig<UnknownRuneProcessor>
{
    /// <summary>
    /// 原始JSON中声明的、但无法被解析的 RuneType。
    /// </summary>
    [Display(Name = "原始类型", Description = "原始JSON中声明的、但无法被系统识别的符文类型。")]
    [ReadOnly(true)]
    public string OriginalRuneType { get; init; } = string.Empty;

    /// <summary>
    /// 导致解析失败的错误信息。
    /// </summary>
    [Display(Name = "错误信息", Description = "描述为什么这个符文无法被正确解析。")]
    public string ErrorMessage { get; init; } = "未知错误。";

    /// <summary>
    /// 捕获到的原始JSON数据。
    /// 使用 JsonObject 可以在后端进行一定程度的操作，并且可以方便地序列化回前端。
    /// </summary>
    [Display(Name = "原始JSON数据", Description = "该符文的原始JSON配置，您可以直接编辑以尝试修复。")]
    public JsonObject RawJsonData { get; init; } = new();

    /// <summary>
    /// 未知符文总是被禁用的。
    /// </summary>
    [Display(Name = "是否启用", Description = "由于无法识别的符文类型，因此该符文被禁用。")]
    [DefaultValue(false)]
    public override bool Enabled => false;

    // 这个符文不可执行，所以它的 Processor 应该抛出异常。
    /// <inheritdoc />
    protected override UnknownRuneProcessor ToCurrentRune(ICreatingContext creatingContext)
    {
        // 或者直接在这里抛出异常，因为它根本不应该被执行
        throw new NotSupportedException("UnknownRuneConfig 是一个不可执行的回退配置，它代表一个解析失败的符文。");
    }
}

/// <summary>
/// 一个虚拟的、不可执行的符文运行时，仅为了满足类型约束。
/// </summary>
public class UnknownRuneProcessor : IRuneProcessor<UnknownRuneConfig, IRuneProcessorDebugDto>
{
    /// <inheritdoc />
    public UnknownRuneConfig Config => throw new NotSupportedException();

    /// <inheritdoc />
    public IRuneProcessorDebugDto DebugDto => throw new NotSupportedException();

    /// <inheritdoc />
    public ProcessorContext ProcessorContext => throw new NotSupportedException();
}