using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace YAESandBox.Workflow.VarSpec;

/// <summary>
/// 一个变量的纯粹类型定义，不包含上下文信息。
/// 这是我们类型系统中的 "原子"。
/// </summary>
/// <remarks>使用 System.Text.Json 的特性来支持多态序列化 (用于API传输) 。
/// 这会让JSON中包含一个 "$type" 字段，前端可以据此区分不同的定义。</remarks>
[JsonDerivedType(typeof(PrimitiveVarSpecDef), typeDiscriminator: "primitive")]
[JsonDerivedType(typeof(RecordVarSpecDef), typeDiscriminator: "record")]
[JsonDerivedType(typeof(ListVarSpecDef), typeDiscriminator: "list")]
public abstract record VarSpecDef(string TypeName, string? Description)
{
    /// <summary>变量的类型基础名称/定义的别名</summary>
    [Required]
    public string TypeName { get; init; } = TypeName;

    /// <summary>对该类型的全局描述</summary>
    public string? Description { get; init; } = Description;
}

/// <summary>
/// 代表一个基础类型，如 String, Number, Boolean。
/// </summary>
public record PrimitiveVarSpecDef(string TypeName, string? Description)
    : VarSpecDef(TypeName, Description);

/// <summary>
/// 代表一个结构化的记录/对象类型，包含一组带类型的属性。
/// </summary>
public record RecordVarSpecDef(string TypeName, string? Description, Dictionary<string, VarSpecDef> Properties)
    : VarSpecDef(TypeName, Description)
{
    /// <summary>
    /// 定义了此记录类型的所有属性。
    /// Key: 属性名 (e.g., "age")
    /// Value: 该属性的类型定义 (VarSpecDef)，允许嵌套。
    /// </summary>
    [Required]
    public Dictionary<string, VarSpecDef> Properties { get; init; } = Properties;
}
/// <summary>
/// 代表一个列表类型，包含一组相同类型的元素。
/// </summary>
public record ListVarSpecDef(string TypeName, string? Description, VarSpecDef ElementDef)
    : VarSpecDef(TypeName, Description)
{
    /// <summary>
    /// 定义了列表中每个元素的类型。
    /// </summary>
    [Required]
    public VarSpecDef ElementDef { get; init; } = ElementDef;
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