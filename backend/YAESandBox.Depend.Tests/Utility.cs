using System.Text.Json.Nodes;
using YAESandBox.Depend.Schema;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Depend.Storage;

namespace YAESandBox.Depend.Tests;

public static class Utility
{
    /// <summary>
    /// 核心测试帮助方法，用于生成 Schema。
    /// 它模拟了 YaeSchemaExporter 的完整配置，确保测试环境与实际运行一致。
    /// </summary>
    public static JsonNode GenerateSchemaFor<T>()
    {
        // 关键点：为JsonHelper提供一个包含camelCase命名策略的选项
        // 这样测试中的属性名断言 ("parentProperty" vs "ParentProperty") 才会正确
        var testJsonOptions = new System.Text.Json.JsonSerializerOptions(YaeSandBoxJsonHelper.JsonSerializerOptions)
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
        
        
        // 我们直接调用 YaeSchemaExporter，它内部应该已经配置好了 Processor 和后处理流程。
        // 这里为了测试的独立性，我们手动配置一遍。
        return YaeSchemaExporter.GenerateSchema(typeof(T), options =>
        {
            // 清空默认，确保只测试我们关心的部分
            options.SchemaProcessors.Clear();
            options.SchemaProcessors.AddRange([
                new DisplayAttributeProcessor(), // 添加它，因为测试模型中用到了
                new FlattenMarkerProcessor(),
                new RequiredProcessor()
            ]);

            options.PostProcessSchema = rootSchema =>
            {
                YaeSchemaExporter.PerformFlattening(rootSchema); 
                YaeSchemaExporter.BuildUiOrder(rootSchema);
                YaeSchemaExporter.BuildRequiredArrays(rootSchema);
            };
        });
    }
}