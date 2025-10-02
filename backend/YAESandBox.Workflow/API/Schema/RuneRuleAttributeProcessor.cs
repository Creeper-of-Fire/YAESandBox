using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using YAESandBox.Depend.Schema;
using YAESandBox.Workflow.Config.RuneConfig;

namespace YAESandBox.Workflow.API.Schema;

/// <summary>
/// 一个独立的 SchemaProcessor，用于处理所有与工作流符文规则相关的 Attribute，
/// 并将它们序列化为一个统一的对象附加到 Schema 中。
/// </summary>
public class RuneRuleAttributeProcessor : IYaeSchemaProcessor
{
    /// <inheritdoc />
    public void Process(JsonSchemaExporterContext context, JsonObject schema)
    {
        var typeInfo = context.TypeInfo.Type;

        // 1. 检查当前处理的类型是否是我们的目标类型（符文配置）
        if (!typeof(AbstractRuneConfig).IsAssignableFrom(typeInfo))
            return;

        // 2. 在方法内部创建临时的、局部的字典
        var rules = new JsonObject();

        // 3. 逐个检查并处理每个规则 Attribute
        object[] attrs = typeInfo.GetCustomAttributes(true);

        // 处理 [NoConfig]
        if (attrs.OfType<NoConfigAttribute>().Any())
        {
            rules["noConfig"] = true;
        }

        // 处理 [SingleInTuum]
        if (attrs.OfType<SingleInTuumAttribute>().Any())
        {
            rules["singleInTuum"] = true;
        }

        // 处理 [InFrontOf]
        if (attrs.OfType<InFrontOfAttribute>().FirstOrDefault() is { } inFrontOfAttr)
        {
            rules["inFrontOf"] = inFrontOfAttr.InFrontOfType.Select(t => t.Name).ToJsonArray();
        }

        // 处理 [Behind]
        if (attrs.OfType<BehindAttribute>().FirstOrDefault() is { } behindAttr)
        {
            rules["behind"] = behindAttr.BehindType.Select(t => t.Name).ToJsonArray();
        }

        // ...未来可以继续在这里添加对新规则Attribute的处理...

        // 4. 如果收集到了任何规则，就将它们一次性添加到 Schema 的扩展数据中
        if (rules.Count <= 0) return;
        schema["x-workflow-rune-rules"] = rules;
    }
}