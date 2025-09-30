using YAESandBox.Depend.Annotations;

namespace YAESandBox.Workflow.DebugDto;

/// <summary>
/// 发给前端，用来显示Debug信息的DTO。
/// 建议使用record来实现
/// </summary>
[RequireRecordImplementation]
public interface IDebugDto;


/// <summary>
/// 一种有日志的Debug信息
/// </summary>
public interface IDebugDtoWithLogs
{
    /// <summary>
    /// 获得Debug信息
    /// </summary>
    List<string> Logs { get; }

    /// <summary>
    /// 运行时错误
    /// </summary>
    public string? RuntimeError { get; set; }
}