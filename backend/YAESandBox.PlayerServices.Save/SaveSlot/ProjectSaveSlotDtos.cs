using System.ComponentModel.DataAnnotations;

namespace YAESandBox.PlayerServices.Save.SaveSlot;

/// <summary>
/// 表示一个存档槽的完整信息。这是高层API的主要数据结构。
/// </summary>
public record SaveSlot(string Id, string Token, string Name, string Type, long CreatedAt)
{
    /// <summary>
    /// 存档槽的唯一标识符，使用其对目录进行操作。
    /// 这个ID用于在高层API中进行引用，例如删除或复制操作 (`DELETE /saves/{Id}`)。
    /// </summary>
    /// <remarks>这个ID通常为其目录名，但是该细节不对前端暴露。</remarks>
    [Required]
    public string Id { get; init; } = Id;

    /// <summary>
    /// 一个不透明的访问令牌。前端应将其视为一个必须保存的句柄。
    /// </summary>
    /// <remarks>这个Token的实际值是用于低层数据读写API的完整相对路径 (例如: 'my-project/autosave_abc123')，但是该细节不对前端暴露。</remarks>
    [Required]
    public string Token { get; init; } = Token;

    /// <summary>用户定义的存档名称，来自meta.json。</summary>
    [Required]
    public string Name { get; init; } = Name;

    /// <summary>存档类型，来自meta.json。后端对此字段不作任何解释。</summary>
    [Required]
    public string Type { get; init; } = Type;

    /// <summary>
    /// 存档创建时间，来自meta.json。
    /// 使用Unix毫秒级时间戳。
    /// </summary>
    [Required]
    public long CreatedAt { get; init; } = CreatedAt;
}

/// <summary>
/// 存储在每个存档槽目录中 meta.json 文件的内部结构。
/// 后端仅用它来反序列化，不会基于其内容做任何业务逻辑判断。
/// </summary>
public record SaveSlotMeta
{
    /// <summary>用户定义的存档名称。</summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>存档类型。</summary>
    [Required]
    public required string Type { get; init; }

    /// <summary>存档创建时间的UTC时间戳。</summary>
    [Required]
    public required long CreatedAt { get; init; }
}

/// <summary>
/// 用于创建新存档槽的请求体。
/// 它同时用于“从零创建”和“从副本创建”两种场景。
/// </summary>
/// <param name="Name">新存档的名称。</param>
/// <param name="Type">新存档的类型 (例如 'autosave', 'snapshot')。后端只负责存储，不关心其含义。</param>
public record CreateSaveSlotRequest(string Name, string Type)
{
    /// <summary>新存档的名称。</summary>
    [Required]
    public string Name { get; init; } = Name;

    /// <summary>新存档的类型 (例如 'autosave', 'snapshot')。后端只负责存储，不关心其含义。</summary>
    [Required]
    public string Type { get; init; } = Type;
}