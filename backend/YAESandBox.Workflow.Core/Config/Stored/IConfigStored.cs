using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Schema.SchemaProcessor;

namespace YAESandBox.Workflow.Core.Config.Stored;

/// <summary>
/// 一个可被<see cref="StoredConfig{TConfig}"/>>存储的配置
/// </summary>
public interface IConfigStored : IConfigWithName;

/// <summary>
/// 一个有 <see cref="Name"/> 属性的配置
/// </summary>
public interface IConfigWithName
{
    /// <summary>
    /// 配置的名称
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    [HiddenInForm(true)]
    public string Name { get; }
}