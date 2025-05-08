namespace YAESandBox.Workflow.AIService.AiConfigSchema;

[AttributeUsage(AttributeTargets.Property)]
public class SelectOptionsAttribute(params SelectOption[] options) : Attribute
{
    public SelectOption[] Options { get; } = options;

    public SelectOptionsAttribute(params string[] options) : this(options.ToList()
        .ConvertAll(str => new SelectOption { Value = str, Label = str }).ToArray()) { }
}