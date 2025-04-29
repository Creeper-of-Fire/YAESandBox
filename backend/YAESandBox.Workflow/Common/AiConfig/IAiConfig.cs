// --- File: YAESandBox.Workflow/Common/AiConfig/IAiConfig.cs---
namespace YAESandBox.Workflow.Common.AiConfig;

/// <summary>
/// 标记接口，代表一个步骤所需的 AI 服务配置。
/// 具体配置细节由实现此接口的类定义。
/// </summary>
public interface IAiConfig
{
    /// <summary>
    /// (可能通用的属性) 指示是否期望 AI 服务以流式方式返回响应。
    /// 如果某个 AI 类型不支持流式，其实现类应忽略此设置或抛出配置错误。
    /// </summary>
    bool IsStreaming { get; } // 可以考虑是否让所有实现都必须包含这个
}