namespace YAESandBox.Workflow.DebugDto;

public interface IWithDebugDto<out T> where T : IDebugDto
{
    /// <summary>
    /// 获得Debug信息
    /// </summary>
    T DebugDto { get; }
}

public interface ILogsDebugDto
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

/// <summary>
/// 发给前端，用来显示Debug信息的DTO。
/// 建议使用record来实现
/// </summary>
public interface IDebugDto;