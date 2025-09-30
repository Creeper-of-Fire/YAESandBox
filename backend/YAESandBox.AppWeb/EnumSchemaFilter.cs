using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace YAESandBox.AppWeb;

/// <inheritdoc />
internal class EnumSchemaFilter : ISchemaFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum) return;
        schema.Enum.Clear();
        schema.Type = "string"; // Represent enum as string
        schema.Format = null;
        foreach (string enumName in Enum.GetNames(context.Type)) schema.Enum.Add(new OpenApiString(enumName));
    }
}