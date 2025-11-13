using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using YAESandBox.Depend.Schema.SchemaProcessor.Abstract;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 处理 [Required] 特性。
/// 当一个属性被标记为 [Required] 时，此处理器会将其名称添加到其父级 Schema 的 "required" 数组中。
/// </summary>
internal class RequiredProcessor : YaePropertyAttributeProcessor<RequiredAttribute>
{
    /// <inheritdoc />
    protected override void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema, RequiredAttribute attribute)
    {
        // --- 使用临时标记 + 后处理 ---
        // 为了避免在处理当前属性时去修改一个可能还不存在的父级 Schema，
        // 我们也采用“标记”策略。

        // 1. 在属性自己的 Schema 上添加一个临时标记。
        schema["x-temp-is-required"] = true;
    }
}