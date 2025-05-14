using NJsonSchema.Generation;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 通用
/// </summary>
public class NormalActionProcessor(Action<SchemaProcessorContext> action) : ISchemaProcessor
{
    /// <inheritdoc/>
    public void Process(SchemaProcessorContext context) => action(context);
}