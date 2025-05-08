using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using YAESandBox.Depend;

namespace YAESandBox.Workflow.AIService.AiConfigSchema;

public class ConfigSchemasBuildHelper
{
    public static List<FormFieldSchema> GenerateSchemaForType(Type type, int baseOrder = 0)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead); // 确保属性可读

        int currentOrder = baseOrder;
        var schemaList = properties.Select<PropertyInfo, FormFieldSchema>(prop => MakeFormFieldSchema(prop, ref currentOrder)).ToList();

        return schemaList.OrderBy(s => s.Order).ToList();
    }

    private static FormFieldSchema MakeFormFieldSchema(PropertyInfo prop, ref int currentOrder)
    {
        // 忽略 JsonIgnore 属性等（如果需要）
        // if (prop.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() != null) continue;

        var fieldSchema = new FormFieldSchema { Name = prop.Name };
        currentOrder += 10; // 给排序留一些间隔
        fieldSchema.Order = currentOrder;


        var displayAttr = prop.GetCustomAttribute<DisplayAttribute>();
        fieldSchema.Label = displayAttr?.GetName() ?? prop.Name; // 如果没有Display Name，则使用属性名
        fieldSchema.Description = displayAttr?.GetDescription();
        fieldSchema.Placeholder = displayAttr?.GetPrompt();

        var readOnlyAttr = prop.GetCustomAttribute<ReadOnlyAttribute>();
        // 属性的set访问器是否公开且存在，也决定了是否只读
        fieldSchema.IsReadOnly =
            readOnlyAttr is { IsReadOnly: true } || prop.SetMethod == null || !prop.SetMethod.IsPublic;


        var requiredAttr = prop.GetCustomAttribute<RequiredAttribute>();
        fieldSchema.IsRequired = requiredAttr != null;

        // 处理 SelectOptionsAttribute
        var selectOptionsAttr = prop.GetCustomAttribute<SelectOptionsAttribute>();
        fieldSchema.Options = selectOptionsAttr?.Options.ToList();


        // 数据类型判断
        if (prop.PropertyType == typeof(string))
        {
            var dataTypeAttr = prop.GetCustomAttribute<DataTypeAttribute>();
            if (dataTypeAttr?.DataType == DataType.MultilineText)
                fieldSchema.SchemaDataType = SchemaDataType.MultilineText;
            else if (prop.GetCustomAttribute<PasswordPropertyTextAttribute>()?.Password ?? false)
                fieldSchema.SchemaDataType = SchemaDataType.Password;
            else
                fieldSchema.SchemaDataType = SchemaDataType.String;
        }
        else if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(long) ||
                 prop.PropertyType == typeof(short) || prop.PropertyType == typeof(byte) ||
                 prop.PropertyType == typeof(decimal) || prop.PropertyType == typeof(double) ||
                 prop.PropertyType == typeof(float))
        {
            fieldSchema.SchemaDataType = SchemaDataType.Number;
        }
        else if (prop.PropertyType == typeof(bool))
        {
            fieldSchema.SchemaDataType = SchemaDataType.Boolean;
        }
        else if (prop.PropertyType.IsEnum)
        {
            fieldSchema.SchemaDataType = SchemaDataType.Enum;
            fieldSchema.Options = Enum.GetValues(prop.PropertyType)
                .Cast<object>()
                .Select(e => new SelectOption
                {
                    Value = e, // 或者 (int)e 如果你想传数字
                    Label = (e.GetType().GetField(e.ToString()!)?.GetCustomAttribute<DisplayAttribute>()?.Name ?? e.ToString())!
                }).ToList();
        }
        else if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
        {
            fieldSchema.SchemaDataType = SchemaDataType.Array;
            Type itemType = prop.PropertyType.GetGenericArguments()[0];
            // 简单处理：假设数组项是简单类型或具有可生成Schema的复杂类型
            // 这里可以递归调用 GenerateSchemaForType 来获取数组项的单个 Schema 定义
            // 为了简化，我们仅支持简单类型的数组或已知对象类型的数组
            if (IsSimpleType(itemType))
            {
                fieldSchema.ArrayItemSchema = new FormFieldSchema { Name = "item", SchemaDataType = GetSimpleTypeName(itemType) };
            }
            else if (!itemType.IsPrimitive && itemType != typeof(string)) // 复杂对象
            {
                // 允许嵌套对象数组，但UI会复杂些
                fieldSchema.ArrayItemSchema = new FormFieldSchema
                {
                    Name = "item", // 数组项没有固定名字
                    SchemaDataType = SchemaDataType.Object,
                    NestedSchema = GenerateSchemaForType(itemType, fieldSchema.Order * 100) // 递归生成
                };
            }
        }
        else if (!prop.PropertyType.IsPrimitive && prop.PropertyType != typeof(string) &&
                 prop.PropertyType is { IsEnum: false, IsArray: false }) // 假设是嵌套对象
        {
            // 检查是否是 System 命名空间下的类型，例如 DateTime, Guid 等，我们可能希望有特殊处理
            if (prop.PropertyType.Namespace?.StartsWith("System") == true)
            {
                // 例如： if (prop.PropertyType == typeof(DateTime)) fieldSchema.DataType = "datetime";
                // 否则，默认为 string 或特定类型
                fieldSchema.SchemaDataType = GetSimpleTypeName(prop.PropertyType);
            }
            else
            {
                fieldSchema.SchemaDataType = SchemaDataType.Object;
                // 递归为嵌套对象生成 Schema
                fieldSchema.NestedSchema = GenerateSchemaForType(prop.PropertyType, fieldSchema.Order * 100);
            }
        }
        else
        {
            fieldSchema.SchemaDataType = GetSimpleTypeName(prop.PropertyType); // "unknown" or specific simple type
        }


        // 填充校验规则
        fieldSchema.Validation = new ValidationRules();
        var rangeAttr = prop.GetCustomAttribute<RangeAttribute>();
        if (rangeAttr != null)
        {
            if (fieldSchema.SchemaDataType == SchemaDataType.Number)
            {
                fieldSchema.Validation.Min = Convert.ToDouble(rangeAttr.Minimum);
                fieldSchema.Validation.Max = Convert.ToDouble(rangeAttr.Maximum);
            }

            fieldSchema.Validation.ErrorMessage = rangeAttr.ErrorMessage; // 你可能需要格式化这个
        }

        var lengthAttr = prop.GetCustomAttribute<StringLengthAttribute>();
        if (lengthAttr != null)
        {
            fieldSchema.Validation.MinLength = lengthAttr.MinimumLength;
            fieldSchema.Validation.MaxLength = lengthAttr.MaximumLength;
            fieldSchema.Validation.ErrorMessage = lengthAttr.ErrorMessage;
        }

        var urlAttr = prop.GetCustomAttribute<UrlAttribute>();
        if (urlAttr != null)
        {
            // 前端可以用 type="url" 或 regex
            fieldSchema.Validation.Pattern = "url"; // 特殊标记，让前端处理
            fieldSchema.Validation.ErrorMessage = urlAttr.ErrorMessage;
        }
        // 还可以添加对 RegularExpressionAttribute等的处理

        if (fieldSchema.Validation.Min == null && fieldSchema.Validation.Max == null &&
            fieldSchema.Validation.MinLength == null && fieldSchema.Validation.MaxLength == null &&
            fieldSchema.Validation.Pattern == null && fieldSchema.Validation.ErrorMessage == null)
        {
            fieldSchema.Validation = null; // 如果没有校验规则，则不发送该对象
        }

        return fieldSchema;
    }

    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid);
    }

    private static SchemaDataType GetSimpleTypeName(Type type)
    {
        if (type == typeof(string)) return SchemaDataType.String;
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
            return SchemaDataType.Integer;
        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float)) 
            return SchemaDataType.Number;
        if (type == typeof(bool)) 
            return SchemaDataType.Boolean;
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return SchemaDataType.DateTime;
        if (type == typeof(Guid))
            return SchemaDataType.GUID; 
        // ...其他简单类型
        Log.Error($"Workflow.AIService: 未知的SchemaDataType：{type.Name}");
        return SchemaDataType.Unknown; // 默认小写类型名
    }
}