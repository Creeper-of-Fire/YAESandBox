using JetBrains.Annotations;
using YAESandBox.Workflow.Config;

namespace YAESandBox.Workflow.API.Schema;

/// <summary>
/// 标明这个模块在单一步骤中只能出现一次
/// </summary>
[BaseTypeRequired(typeof(AbstractModuleConfig))]
[AttributeUsage(AttributeTargets.Class)]
public class SingleInStepAttribute : Attribute;

/// <summary>
/// 标明——
/// <para>前端：这个模块只能出现步骤列表的最后一个步骤里。</para> 后端：这个步骤在进行并行分析时，会被强制最后执行。
/// </summary>
[BaseTypeRequired(typeof(AbstractModuleConfig))]
[AttributeUsage(AttributeTargets.Class)]
public class InLastStepAttribute : Attribute;

/// <summary>
/// 标明这个模块不需要配置，也不能被保存为全局模块，只能直接从模块种类中新建。（目前的想法）
/// 也有可能是不能从模块种类中新建，只能从不可删除的全局模块中选择一个；又或者步骤生成时会自带这个模块；又或者有一个单独的不可配置模块库……
/// </summary>
[BaseTypeRequired(typeof(AbstractModuleConfig))]
[AttributeUsage(AttributeTargets.Class)]
public class NoConfigAttribute : Attribute;

/// <summary>
/// 标明这个模块必须在某些模块的前面执行
/// </summary>
/// <param name="inFrontOfType">请至少有一个！</param>
[BaseTypeRequired(typeof(AbstractModuleConfig))]
[AttributeUsage(AttributeTargets.Class)]
public class InFrontOfAttribute(params Type[] inFrontOfType) : Attribute
{
    /// <summary>
    /// 前面的模块类型
    /// </summary>
    public Type[] InFrontOfType { get; } = inFrontOfType;
}

/// <summary>
/// 标明这个模块必须在某些模块的后面执行
/// </summary>
/// <param name="behindType">请至少有一个！</param>
[BaseTypeRequired(typeof(AbstractModuleConfig))]
[AttributeUsage(AttributeTargets.Class)]
public class BehindAttribute(params Type[] behindType) : Attribute
{
    /// <summary>
    /// 后面的模块类型
    /// </summary>
    public Type[] BehindType { get; } = behindType;
}