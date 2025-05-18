namespace YAESandBox.Workflow.DebugDto;

public interface IWithDebugDto<out T> where T : IDebugDto
{
    /// <summary>
    /// 获得Debug信息
    /// </summary>
    T DebugDto { get; }
};

/// <summary>
/// 发给前端，用来显示Debug信息的DTO。
/// 建议使用record来实现
/// </summary>
public interface IDebugDto;