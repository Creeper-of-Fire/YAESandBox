using System.Reflection;
using Namotion.Reflection;
using NJsonSchema.Generation;
using YAESandBox.Workflow.Config;

namespace YAESandBox.Workflow.Module.ModuleAttribute;

/// <summary>
/// 一个独立的 SchemaProcessor，用于处理所有与工作流模块规则相关的 Attribute，
/// 并将它们序列化为一个统一的对象附加到 Schema 中。
/// </summary>
public class ModuleRuleAttributeProcessor : ISchemaProcessor
{
    /// <inheritdoc />
    public void Process(SchemaProcessorContext context)
    {
        // 1. 检查当前处理的类型是否是我们的目标类型（模块配置）
        if (!typeof(AbstractModuleConfig).IsAssignableFrom(context.ContextualType))
        {
            return;
        }

        // 2. 在方法内部创建临时的、局部的 acles 字典
        var rules = new Dictionary<string, object>();
        var typeInfo = context.ContextualType;

        // 3. 逐个检查并处理每个规则 Attribute
        
        // 处理 [NoConfig]
        if (typeInfo.GetContextAttribute<NoConfigAttribute>(true) != null)
        {
            rules["noConfig"] = true;
        }

        // 处理 [SingleInStep]
        if (typeInfo.GetContextAttribute<SingleInStepAttribute>(true) != null)
        {
            rules["singleInStep"] = true;
        }
        
        // 处理 [InLastStep]
        if (typeInfo.GetContextAttribute<InLastStepAttribute>(true) != null)
        {
            rules["inLastStep"] = true;
        }

        // 处理 [InFrontOf]
        if (typeInfo.GetContextAttribute<InFrontOfAttribute>(true) is { } inFrontOfAttr)
        {
            rules["inFrontOf"] = inFrontOfAttr.InFrontOfType.Select(t => t.Name).ToArray();
        }

        // 处理 [Behind]
        if (typeInfo.GetContextAttribute<BehindAttribute>(true) is { } behindAttr)
        {
            rules["behind"] = behindAttr.BehindType.Select(t => t.Name).ToArray();
        }

        // ...未来可以继续在这里添加对新规则Attribute的处理...

        // 4. 如果收集到了任何规则，就将它们一次性添加到 Schema 的扩展数据中
        if (rules.Count <= 0) return;
        context.Schema.ExtensionData ??= new Dictionary<string, object?>();
        context.Schema.ExtensionData["x-workflow-module-rules"] = rules;
    }
}