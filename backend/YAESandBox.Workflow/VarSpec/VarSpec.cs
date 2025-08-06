namespace YAESandBox.Workflow.VarSpec;

/// <summary>
/// 一个变量的纯粹类型定义，不包含上下文信息。
/// 这是我们类型系统中的 "原子"。
/// </summary>
/// <param name="TypeName">变量的类型基础名称/定义的别名</param>
/// <param name="Description">对该类型的全局描述</param>
public record VarSpecDef(string TypeName, string? Description);

/// <summary>
/// 定义了一个变量
/// </summary>
public interface IVarSpec
{
    /// <summary>
    /// 变量在当前上下文中的名称。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 变量的类型定义。
    /// </summary>
    VarSpecDef Def { get; }

    /// <summary>
    /// 此变量是否可为空。默认为 false。
    /// </summary>
    public bool IsNullable { get; init; }
}

/// <summary>
/// 描述一个被生产出的变量。
/// </summary>
/// <param name="Name">被生产的变量名</param>
/// <param name="Def">变量的类型定义。</param>
public record ProducedSpec(string Name, VarSpecDef Def) : IVarSpec
{
    /// <summary>
    /// 此变量是否可为空。默认为 false。
    /// </summary>
    public bool IsNullable { get; init; } = false;
}

/// <inheritdoc/>
/// <summary>
/// 描述一个被消费的变量。
/// </summary>
/// <param name="Name">被消费的变量名</param>
/// <param name="Def">变量的类型定义。</param>
public record ConsumedSpec(string Name, VarSpecDef Def) : IVarSpec
{
    /// <summary>
    /// 此变量是否可为空。默认为 false。
    /// </summary>
    public bool IsNullable { get; init; } = false;
}