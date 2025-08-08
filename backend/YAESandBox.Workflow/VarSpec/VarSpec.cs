using System.ComponentModel.DataAnnotations;

namespace YAESandBox.Workflow.VarSpec;

/// <summary>
/// 一个变量的纯粹类型定义，不包含上下文信息。
/// 这是我们类型系统中的 "原子"。
/// </summary>
public record VarSpecDef(string TypeName, string? Description)
{
    /// <summary>变量的类型基础名称/定义的别名</summary>
    [Required]
    public string TypeName { get; init; } = TypeName;

    /// <summary>对该类型的全局描述</summary>
    public string? Description { get; init; } = Description;
}

/// <summary>
/// 定义了一个变量
/// </summary>
public interface IVarSpec
{
    /// <summary>
    /// 变量在当前上下文中的名称。
    /// </summary>
    [Required]
    string Name { get; }

    /// <summary>
    /// 变量的类型定义。
    /// </summary>
    [Required]
    VarSpecDef Def { get; }
}

/// <summary>
/// 描述一个被生产出的变量。
/// </summary>
public record ProducedSpec(string Name, VarSpecDef Def) : IVarSpec
{
    /// <summary>被生产的变量名</summary>
    [Required]
    public string Name { get; init; } = Name;

    /// <summary>变量的类型定义。</summary>
    [Required]
    public VarSpecDef Def { get; init; } = Def;
}

/// <inheritdoc/>
/// <summary>
/// 描述一个被消费的变量。
/// </summary>
public record ConsumedSpec(string Name, VarSpecDef Def) : IVarSpec
{
    /// <summary>被消费的变量名</summary>
    [Required]
    public string Name { get; init; } = Name;

    /// <summary>变量的类型定义。</summary>
    [Required]
    public VarSpecDef Def { get; init; } = Def;

    /// <summary>
    /// 此变量是否可选。默认为 false。
    /// </summary>
    [Required]
    public bool IsOptional { get; init; } = false;
}