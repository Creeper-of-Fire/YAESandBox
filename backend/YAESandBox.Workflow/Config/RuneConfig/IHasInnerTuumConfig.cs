namespace YAESandBox.Workflow.Config.RuneConfig;

/// <summary>
/// 一个契约接口，标记一个配置类内部封装了一个 TuumConfig。
/// 这可以被前端用来触发特殊的UI渲染，例如一个专用的枢机编辑器。
/// </summary>
public interface IHasInnerTuumConfig
{
    /// <summary>
    /// 获取被封装的枢机配置。
    /// </summary>
    public TuumConfig InnerTuum { get; }
}