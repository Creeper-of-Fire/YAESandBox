using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace YAESandBox.Workflow.Config.Stored;

/// <summary>
/// 一个通用的、可持久化的配置包装器。
/// </summary>
/// <typeparam name="TConfig">具体的配置类型，如 WorkflowConfig, TuumConfig 等。</typeparam>
public record StoredConfig<TConfig> where TConfig : IConfigStored
{
    /// <summary>
    /// 配置的逻辑引用标识。可空，为空时就不能引用它。
    /// </summary>
    public StoredConfigRef? StoreRef { get; init; }

    /// <summary>
    /// 用户可读的名称。
    /// </summary>
    [JsonIgnore]
    public string Name => this.Content.Name;

    /// <summary>
    /// 实际的配置内容。
    /// </summary>
    [Required]
    public required TConfig Content { get; set; }
    
    /// <summary>
    /// 指示此配置是否为只读。
    /// 只读配置（如内置模板）不能通过API进行修改或删除。
    /// </summary>
    [Required]
    [DefaultValue(false)]
    public bool IsReadOnly { get; set; } = false;

    /// <summary>
    /// 存储实体的元数据。
    /// </summary>
    public StoredConfigMeta Meta { get; init; } = new();
}

/// <summary>
/// 配置的逻辑引用，用于持久化、可重复地标识一个配置蓝图。
/// </summary>
public record StoredConfigRef
{
    /// <summary>
    /// 配置的引用ID，例如 "yae.templates.standard-rag-workflow"。
    /// </summary>
    /// <remarks>
    /// 这个ID是持久的、不可变的，并且可重复的，它用来标识一个配置。以便引用。
    /// 它不可以作为存储系统存储的键，因为它是可重复的。
    /// </remarks>
    [Required(AllowEmptyStrings =  true)]
    public required string RefId { get; init; }

    /// <summary>
    /// 配置的版本号。
    /// </summary>
    [Required(AllowEmptyStrings =  true)]
    public required string Version { get; init; }
}

/// <summary>
/// 存储实体的元数据类型。
/// </summary>
public record StoredConfigMeta
{
    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 标签
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 其他信息
    /// </summary>
    public Dictionary<string, string>? AdditionalThing { get; set; }
}