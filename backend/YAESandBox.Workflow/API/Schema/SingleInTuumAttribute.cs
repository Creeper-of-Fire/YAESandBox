using JetBrains.Annotations;
using YAESandBox.Workflow.Config;

namespace YAESandBox.Workflow.API.Schema;

/// <summary>
/// 标明这个符文在单一祝祷中只能出现一次
/// </summary>
[BaseTypeRequired(typeof(AbstractRuneConfig))]
[AttributeUsage(AttributeTargets.Class)]
public class SingleInTuumAttribute : Attribute;

/// <summary>
/// 标明——
/// <para>前端：这个符文只能出现祝祷列表的最后一个祝祷里。</para> 后端：这个祝祷在进行并行分析时，会被强制最后执行。
/// </summary>
[BaseTypeRequired(typeof(AbstractRuneConfig))]
[AttributeUsage(AttributeTargets.Class)]
public class InLastTuumAttribute : Attribute;

/// <summary>
/// 标明这个符文不需要配置，也不能被保存为全局符文，只能直接从符文种类中新建。（目前的想法）
/// 也有可能是不能从符文种类中新建，只能从不可删除的全局符文中选择一个；又或者祝祷生成时会自带这个符文；又或者有一个单独的不可配置符文库……
/// </summary>
[BaseTypeRequired(typeof(AbstractRuneConfig))]
[AttributeUsage(AttributeTargets.Class)]
public class NoConfigAttribute : Attribute;

/// <summary>
/// 标明这个符文必须在某些符文的前面执行
/// </summary>
/// <param name="inFrontOfType">请至少有一个！</param>
[BaseTypeRequired(typeof(AbstractRuneConfig))]
[AttributeUsage(AttributeTargets.Class)]
public class InFrontOfAttribute(params Type[] inFrontOfType) : Attribute
{
    /// <summary>
    /// 前面的符文类型
    /// </summary>
    public Type[] InFrontOfType { get; } = inFrontOfType;
}

/// <summary>
/// 标明这个符文必须在某些符文的后面执行
/// </summary>
/// <param name="behindType">请至少有一个！</param>
[BaseTypeRequired(typeof(AbstractRuneConfig))]
[AttributeUsage(AttributeTargets.Class)]
public class BehindAttribute(params Type[] behindType) : Attribute
{
    /// <summary>
    /// 后面的符文类型
    /// </summary>
    public Type[] BehindType { get; } = behindType;
}