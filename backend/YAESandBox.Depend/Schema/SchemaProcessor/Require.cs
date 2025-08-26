using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

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
        // context.ParentSchema 在 .NET 9 的预览版中可能还不稳定或不存在。
        // 一个更可靠的方法是，在后处理阶段完成这个操作，或者在 TransformNode 之外操作。
        // 但既然我们已经有了这个模式，我们可以尝试找到父级。
        // 不幸的是，JsonSchemaExporterContext 没有直接提供对父级 Schema 的引用。

        // --- 更健壮的策略：使用临时标记 + 后处理 ---
        // 为了避免在处理当前属性时去修改一个可能还不存在的父级 Schema，
        // 我们也采用和 Flatten 类似的“标记”策略。

        // 1. 在属性自己的 Schema 上添加一个临时标记。
        schema["x-temp-is-required"] = true;
        return;
    }
}