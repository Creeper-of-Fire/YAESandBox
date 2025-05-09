// ConfigSchemasBuildHelper.cs

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using YAESandBox.Depend; // 假设 Log 在这里
using System.Collections;
using YAESandBox.Workflow.AIService.AiConfig; // For IDictionary

namespace YAESandBox.Workflow.AIService.AiConfigSchema;

/// <summary>
/// 帮助类，用于根据C#类型定义动态生成前端表单所需的Schema结构。
/// 它通过反射读取类型的属性及其关联的DataAnnotations特性。
/// </summary>
public class ConfigSchemasBuildHelper
{
    /// <summary>
    /// 预定义的简单C#类型到SchemaDataType的映射表。
    /// 用于快速确定基础类型的Schema表示。
    /// </summary>
    private static readonly Dictionary<Type, SchemaDataType> SimpleTypeMappings = new()
    {
        { typeof(string), SchemaDataType.String },
        { typeof(int), SchemaDataType.Integer },
        { typeof(long), SchemaDataType.Integer },
        { typeof(short), SchemaDataType.Integer },
        { typeof(byte), SchemaDataType.Integer },
        { typeof(sbyte), SchemaDataType.Integer },
        { typeof(uint), SchemaDataType.Integer },
        { typeof(ulong), SchemaDataType.Integer },
        { typeof(ushort), SchemaDataType.Integer },
        { typeof(decimal), SchemaDataType.Number },
        { typeof(double), SchemaDataType.Number },
        { typeof(float), SchemaDataType.Number },
        { typeof(bool), SchemaDataType.Boolean },
        { typeof(DateTime), SchemaDataType.DateTime },
        { typeof(DateTimeOffset), SchemaDataType.DateTime },
        { typeof(Guid), SchemaDataType.GUID }
        // TimeSpan 可以根据需要添加，例如映射到 String 或一个自定义的 TimeSpan SchemaDataType
    };

    /// <summary>
    /// 为给定的C#类型生成表单Schema列表。
    /// </summary>
    /// <param name="type">要为其生成Schema的C#类型。</param>
    /// <param name="baseOrder">生成的Schema字段的起始排序号。用于控制嵌套对象中属性的相对顺序。</param>
    /// <returns>一个包含该类型所有可读公共属性对应FormFieldSchema的列表，按Order排序。</returns>
    public static List<FormFieldSchema> GenerateSchemaForType(Type type, int baseOrder = 0)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead); // 确保属性可读

        int currentOrder = baseOrder;
        var schemaList = properties
            .Select(prop => MakeFormFieldSchema(prop, ref currentOrder))
            .OrderBy(s => s.Order) // 根据Order字段排序
            .ToList();

        return schemaList;
    }

    /// <summary>
    /// 为单个属性创建FormFieldSchema对象。
    /// 这是Schema生成的核心逻辑，会处理属性的各种元数据。
    /// </summary>
    /// <param name="prop">要处理的属性信息。</param>
    /// <param name="currentOrder">当前的排序号引用，处理完此属性后会增加。</param>
    /// <returns>为该属性生成的FormFieldSchema对象。</returns>
    private static FormFieldSchema MakeFormFieldSchema(PropertyInfo prop, ref int currentOrder)
    {
        var fieldSchema = new FormFieldSchema { Name = prop.Name };
        currentOrder += 10; // 为每个属性的Order值增加步长，方便后续插入或调整
        fieldSchema.Order = currentOrder;

        // 填充显示相关的元数据 (Label, Description, Placeholder, IsReadOnly, IsRequired)
        PopulateDisplayProperties(prop, fieldSchema);

        // 填充选项数据 (来自 SelectOptionsAttribute 或 Enum 类型自身)
        PopulateOptions(prop, fieldSchema);

        // 确定核心的 SchemaDataType 并处理特殊/复杂类型 (如字符串变体, 枚举, 数组, 对象, 字典)
        DetermineSchemaDataTypeAndHandleComplexTypes(prop, fieldSchema, currentOrder);

        // 填充校验规则 (来自 RangeAttribute, StringLengthAttribute 等)
        PopulateValidationRules(prop, fieldSchema);

        return fieldSchema;
    }

    /// <summary>
    /// 填充FormFieldSchema的显示相关属性。
    /// 包括：Label, Description, Placeholder, IsReadOnly, IsRequired。
    /// </summary>
    /// <param name="prop">源属性。</param>
    /// <param name="schema">要填充的FormFieldSchema对象。</param>
    private static void PopulateDisplayProperties(PropertyInfo prop, FormFieldSchema schema)
    {
        var displayAttr = prop.GetCustomAttribute<DisplayAttribute>();
        schema.Label = displayAttr?.GetName() ?? prop.Name; // 优先使用DisplayAttribute的Name，否则用属性名
        schema.Description = displayAttr?.GetDescription();
        schema.Placeholder = displayAttr?.GetPrompt();

        var readOnlyAttr = prop.GetCustomAttribute<ReadOnlyAttribute>();
        // 如果属性被标记为ReadOnly，或者它没有公共的set访问器，则认为是只读的
        schema.IsReadOnly =
            (readOnlyAttr?.IsReadOnly ?? false) || prop.SetMethod == null || !prop.SetMethod.IsPublic;

        schema.IsRequired = prop.GetCustomAttribute<RequiredAttribute>() != null;

        // 如果有DefaultValueAttribute
        var defaultValueAttr = prop.GetCustomAttribute<DefaultValueAttribute>();
        if (defaultValueAttr != null) schema.DefaultValue = defaultValueAttr.Value;
    }

    /// <summary>
    /// 填充FormFieldSchema的Options属性。
    /// Options主要用于枚举类型或被SelectOptionsAttribute标记的属性。
    /// </summary>
    /// <param name="prop">源属性。</param>
    /// <param name="schema">要填充的FormFieldSchema对象。</param>
    private static void PopulateOptions(PropertyInfo prop, FormFieldSchema schema)
    {
        var selectOptionsAttr = prop.GetCustomAttribute<SelectOptionsAttribute>();

        // 如果没有 SelectOptionsAttribute，检查是否为枚举类型
        // 设置静态选项 (如果提供了)
        if (selectOptionsAttr == null)
        {
            var propertyOrUnderlyingType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            if (!propertyOrUnderlyingType.IsEnum) return;
            schema.Options = GetEnumSelectOptions(propertyOrUnderlyingType);
            schema.IsEditableSelectOptions = false; // 枚举默认不可创建
            return;
        }

        if (selectOptionsAttr.Options.Length > 0)
            schema.Options = selectOptionsAttr.Options.ToList();

        // 设置动态加载端点和可创建性
        schema.OptionsProviderEndpoint = selectOptionsAttr.OptionsProviderEndpoint;
        schema.IsEditableSelectOptions = selectOptionsAttr.IsEditableSelectOptions;
    }

    /// <summary>
    /// 确定属性的SchemaDataType，并处理需要特殊递归或结构定义的复杂类型。
    /// 例如：字符串的特殊形式（多行、密码）、枚举、数组、对象、字典。
    /// </summary>
    /// <param name="prop">源属性。</param>
    /// <param name="schema">要填充的FormFieldSchema对象。</param>
    /// <param name="baseOrderForNesting">用于嵌套结构中属性排序的基准Order值。</param>
    private static void DetermineSchemaDataTypeAndHandleComplexTypes(PropertyInfo prop, FormFieldSchema schema, int baseOrderForNesting)
    {
        Type propertyType = prop.PropertyType;

        // 1. 处理特定类型的字符串 (多行文本, 密码)
        if (propertyType == typeof(string))
        {
            var dataTypeAttr = prop.GetCustomAttribute<DataTypeAttribute>();
            if (dataTypeAttr?.DataType == DataType.MultilineText)
                schema.SchemaDataType = SchemaDataType.MultilineText;
            else if (prop.GetCustomAttribute<PasswordPropertyTextAttribute>()?.Password ?? false)
                schema.SchemaDataType = SchemaDataType.Password;
            else
                schema.SchemaDataType = SchemaDataType.String; // 默认字符串
            return;
        }

        // 2. 尝试从简单类型映射表获取 (包括可空类型的基础类型)
        SchemaDataType resolvedSimpleType = GetSchemaDataTypeForType(propertyType);
        if (resolvedSimpleType != SchemaDataType.Unknown)
        {
            // 如果是枚举，确保 Options 已被填充 (通常在PopulateOptions中处理)
            if (resolvedSimpleType == SchemaDataType.Enum && schema.Options == null)
            {
                Type enumType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
                schema.Options = GetEnumSelectOptions(enumType);
            }

            schema.SchemaDataType = resolvedSimpleType;
            return;
        }

        // 3. 处理数组/列表 (List<T>)
        if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
        {
            schema.SchemaDataType = SchemaDataType.Array;
            Type itemType = propertyType.GetGenericArguments()[0];
            // 为数组项创建Schema
            // 数组项的Order通常不直接影响顶层布局，但在其内部复杂对象中可能需要
            schema.ArrayItemSchema = CreateItemSchemaForCollection(itemType, "item", baseOrderForNesting + 10000); // 使用较大的Order偏移避免冲突
            return;
        }

        // 4. 处理字典 (IDictionary<TKey, TValue> 或 Dictionary<TKey, TValue>)
        Type? dictionaryInterface = propertyType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>)) // 直接是 Dictionary<,>
        {
            dictionaryInterface = propertyType;
        }

        if (dictionaryInterface != null)
        {
            schema.SchemaDataType = SchemaDataType.Dictionary;
            Type keyType = dictionaryInterface.GetGenericArguments()[0];
            Type valueType = dictionaryInterface.GetGenericArguments()[1];

            schema.KeyInfo = CreateDictionaryKeyInfo(keyType);
            // 为字典的值创建Schema
            schema.DictionaryValueSchema = CreateItemSchemaForCollection(valueType, "value", baseOrderForNesting + 20000);
            return;
        }

        // 5. 处理其他被认为是“复杂对象”的类型 (非简单类型、非数组、非字典)
        //    这些通常是自定义的类，需要递归生成其内部属性的Schema。
        if (!IsConsideredSimpleOrSystemType(propertyType) && !propertyType.IsEnum && !propertyType.IsArray)
        {
            schema.SchemaDataType = SchemaDataType.Object;
            // 递归为嵌套对象生成Schema。使用基于父级Order的新的baseOrder。
            schema.NestedSchema = GenerateSchemaForType(propertyType, baseOrderForNesting * 100);
            return;
        }

        // 6. 如果以上都不是，则标记为未知类型，并记录错误
        Log.Error(
            $"Workflow.AIService.ConfigSchemasBuildHelper: 属性 '{prop.DeclaringType?.Name}.{prop.Name}' 的类型 '{propertyType.Name}' 未能映射到已知的SchemaDataType。");
        schema.SchemaDataType = SchemaDataType.Unknown;
    }

    /// <summary>
    /// 为集合类型（数组、列表、字典的值）的元素或值创建FormFieldSchema。
    /// </summary>
    /// <param name="itemType">元素/值的类型。</param>
    /// <param name="itemName">元素/值的名称（主要用于调试或内部标识）。</param>
    /// <param name="baseOrder">用于嵌套对象排序的基准Order。</param>
    /// <returns>描述元素/值结构的FormFieldSchema。</returns>
    private static FormFieldSchema CreateItemSchemaForCollection(Type itemType, string itemName, int baseOrder)
    {
        var itemSchema = new FormFieldSchema { Name = itemName }; // 集合项的Name通常不直接显示

        SchemaDataType itemDataType = GetSchemaDataTypeForType(itemType);
        itemSchema.SchemaDataType = itemDataType;

        if (itemDataType == SchemaDataType.Enum)
        {
            itemSchema.Options = GetEnumSelectOptions(Nullable.GetUnderlyingType(itemType) ?? itemType);
        }
        else if (itemDataType == SchemaDataType.Object) // 如果项本身是复杂对象
        {
            itemSchema.NestedSchema = GenerateSchemaForType(itemType, baseOrder);
        }
        // 如果项是数组或字典 (List<List<int>>, List<Dictionary<string, int>>)
        // 递归调用 DetermineSchemaDataTypeAndHandleComplexTypes (或类似逻辑) 以填充 ArrayItemSchema/DictionaryValueSchema
        // 这里简化处理：假设集合项不是另一个集合，如果需要支持List<List<T>>等，则需要更深的递归。
        // 对于一个简单的 CreateItemSchema，我们主要关注类型和简单嵌套。
        // 如果itemType本身是 List<T> 或 Dictionary<K,V>，需要更复杂的处理。
        // 当前版本中，如果itemType是List或Dictionary，GetSchemaDataTypeForType会返回Unknown，
        // 除非我们在此处显式处理递归集合类型。
        // 为了保持这个辅助方法相对简单，我们先依赖 GetSchemaDataTypeForType 的结果。
        // 如果是复杂对象，其 NestedSchema 会由 GenerateSchemaForType 填充。

        return itemSchema;
    }

    /// <summary>
    /// 为字典的键类型创建DictionaryKeyInfo对象。
    /// </summary>
    /// <param name="keyType">字典键的C#类型。</param>
    /// <returns>描述键信息的DictionaryKeyInfo对象。</returns>
    private static DictionaryKeyInfo CreateDictionaryKeyInfo(Type keyType)
    {
        var keyInfo = new DictionaryKeyInfo { RawKeyTypeName = keyType.Name };
        Type underlyingKeyType = Nullable.GetUnderlyingType(keyType) ?? keyType;

        keyInfo.KeyType = GetSchemaDataTypeForType(underlyingKeyType);

        if (keyInfo.KeyType == SchemaDataType.Enum)
        {
            keyInfo.EnumOptions = GetEnumSelectOptions(underlyingKeyType);
        }
        else if (keyInfo.KeyType == SchemaDataType.Unknown)
        {
            // 如果键类型不是预定义的简单类型或枚举，记录警告或错误。
            // 对于字典键，通常期望是简单类型或枚举。复杂对象作为键在JSON中不常见。
            Log.Warning(
                $"Workflow.AIService.ConfigSchemasBuildHelper: 字典键类型 '{keyType.FullName}' 无法直接映射为简单的SchemaDataType，可能导致前端处理困难。默认为String。");
            keyInfo.KeyType = SchemaDataType.String; // 默认回退到String，前端可能需要特殊处理
        }

        return keyInfo;
    }

    /// <summary>
    /// 填充FormFieldSchema的Validation属性，从属性的校验特性中提取规则。
    /// </summary>
    /// <param name="prop">源属性。</param>
    /// <param name="schema">要填充的FormFieldSchema对象。</param>
    private static void PopulateValidationRules(PropertyInfo prop, FormFieldSchema schema)
    {
        var validationRules = new ValidationRules();
        bool hasRules = false;
        List<string> errorMessages = new List<string>();

        // RangeAttribute: 用于数字范围
        var rangeAttr = prop.GetCustomAttribute<RangeAttribute>();
        if (rangeAttr != null)
        {
            if (schema.SchemaDataType == SchemaDataType.Number || schema.SchemaDataType == SchemaDataType.Integer)
            {
                try
                {
                    validationRules.Min = Convert.ToDouble(rangeAttr.Minimum);
                    validationRules.Max = Convert.ToDouble(rangeAttr.Maximum);
                    hasRules = true;
                    if (!string.IsNullOrEmpty(rangeAttr.ErrorMessage)) errorMessages.Add(rangeAttr.ErrorMessage);
                }
                catch (Exception ex)
                {
                    Log.Error($"Workflow.AIService.ConfigSchemasBuildHelper: 属性 {prop.Name} 的 RangeAttribute 值无法转换为double: {ex.Message}");
                }
            }
        }

        // StringLengthAttribute: 用于字符串长度
        var lengthAttr = prop.GetCustomAttribute<StringLengthAttribute>();
        if (lengthAttr != null &&
            (schema.SchemaDataType == SchemaDataType.String || schema.SchemaDataType == SchemaDataType.MultilineText ||
             schema.SchemaDataType == SchemaDataType.Password))
        {
            validationRules.MinLength = lengthAttr.MinimumLength;
            validationRules.MaxLength = lengthAttr.MaximumLength; // MaxLength 是必须的
            hasRules = true;
            if (!string.IsNullOrEmpty(lengthAttr.ErrorMessage)) errorMessages.Add(lengthAttr.ErrorMessage);
        }

        // MinLengthAttribute (System.ComponentModel.DataAnnotations)
        var minLengthAttr = prop.GetCustomAttribute<MinLengthAttribute>();
        if (minLengthAttr != null && validationRules.MinLength == null && // 避免与StringLength冲突
            (schema.SchemaDataType == SchemaDataType.String || schema.SchemaDataType == SchemaDataType.MultilineText ||
             schema.SchemaDataType == SchemaDataType.Password))
        {
            validationRules.MinLength = minLengthAttr.Length;
            hasRules = true;
            if (!string.IsNullOrEmpty(minLengthAttr.ErrorMessage)) errorMessages.Add(minLengthAttr.ErrorMessage);
        }

        // MaxLengthAttribute (System.ComponentModel.DataAnnotations)
        var maxLengthAttr = prop.GetCustomAttribute<MaxLengthAttribute>();
        if (maxLengthAttr != null && validationRules.MaxLength == null && // 避免与StringLength冲突
            (schema.SchemaDataType == SchemaDataType.String || schema.SchemaDataType == SchemaDataType.MultilineText ||
             schema.SchemaDataType == SchemaDataType.Password))
        {
            validationRules.MaxLength = maxLengthAttr.Length;
            hasRules = true;
            if (!string.IsNullOrEmpty(maxLengthAttr.ErrorMessage)) errorMessages.Add(maxLengthAttr.ErrorMessage);
        }


        // UrlAttribute: 用于URL校验
        var urlAttr = prop.GetCustomAttribute<UrlAttribute>();
        if (urlAttr != null)
        {
            // 使用 "url" 作为特殊模式，让前端可以应用特定的URL校验逻辑
            validationRules.Pattern = string.IsNullOrEmpty(validationRules.Pattern) ? "url" : validationRules.Pattern + ";url";
            hasRules = true;
            if (!string.IsNullOrEmpty(urlAttr.ErrorMessage)) errorMessages.Add(urlAttr.ErrorMessage);
        }

        // RegularExpressionAttribute: 用于自定义正则校验
        var regexAttr = prop.GetCustomAttribute<RegularExpressionAttribute>();
        if (regexAttr != null)
        {
            validationRules.Pattern = string.IsNullOrEmpty(validationRules.Pattern)
                ? regexAttr.Pattern
                : validationRules.Pattern + ";" + regexAttr.Pattern;
            hasRules = true;
            if (!string.IsNullOrEmpty(regexAttr.ErrorMessage)) errorMessages.Add(regexAttr.ErrorMessage);
        }

        // EmailAddressAttribute
        var emailAttr = prop.GetCustomAttribute<EmailAddressAttribute>();
        if (emailAttr != null)
        {
            validationRules.Pattern =
                string.IsNullOrEmpty(validationRules.Pattern) ? "email" : validationRules.Pattern + ";email"; // 特殊标记 "email"
            hasRules = true;
            if (!string.IsNullOrEmpty(emailAttr.ErrorMessage)) errorMessages.Add(emailAttr.ErrorMessage);
        }

        // PhoneAttribute
        var phoneAttr = prop.GetCustomAttribute<PhoneAttribute>();
        if (phoneAttr != null)
        {
            validationRules.Pattern =
                string.IsNullOrEmpty(validationRules.Pattern) ? "tel" : validationRules.Pattern + ";tel"; // 特殊标记 "tel" for type="tel"
            hasRules = true;
            if (!string.IsNullOrEmpty(phoneAttr.ErrorMessage)) errorMessages.Add(phoneAttr.ErrorMessage);
        }


        if (hasRules)
        {
            if (errorMessages.Any())
            {
                validationRules.ErrorMessage = string.Join(" ", errorMessages); // 合并错误信息
            }

            schema.Validation = validationRules;
        }
    }

    /// <summary>
    /// 获取给定C#类型的SchemaDataType表示。
    /// 优先使用 SimpleTypeMappings 字典，然后处理可空类型和枚举。
    /// </summary>
    /// <param name="type">要解析的C#类型。</param>
    /// <returns>对应的SchemaDataType，如果无法确定则返回Unknown。</returns>
    private static SchemaDataType GetSchemaDataTypeForType(Type type)
    {
        // 1. 直接从映射表查找
        if (SimpleTypeMappings.TryGetValue(type, out var schemaType))
        {
            return schemaType;
        }

        // 2. 处理可空类型 Nullable<T>
        Type? underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            // 递归调用以处理基础类型 (例如 Nullable<int>, Nullable<DateTime>, Nullable<MyEnum>)
            return GetSchemaDataTypeForType(underlyingType);
        }

        // 3. 处理枚举类型 (如果未在 SimpleTypeMappings 中，尽管通常枚举会被单独处理)
        if (type.IsEnum)
        {
            return SchemaDataType.Enum;
        }

        // 4. 对于其他类型（如自定义类、List<T>, Dictionary<K,V> 等），
        //    此方法不直接解析为 Object, Array, Dictionary。
        //    这些复杂类型的识别和处理在 DetermineSchemaDataTypeAndHandleComplexTypes 方法中进行。
        //    如果到这里还没有匹配，说明它不是一个“简单”类型。
        //    调用此方法的地方需要根据上下文判断是否应为Object/Array/Dictionary。
        //    若仅用于判断基础类型，则返回Unknown。
        return SchemaDataType.Unknown;
    }

    /// <summary>
    /// 判断一个类型是否被认为是“简单的”或系统内置的不需要进一步递归展开为复杂对象的类型。
    /// 这有助于决定是否将一个类型视为 SchemaDataType.Object 并为其生成 NestedSchema。
    /// </summary>
    /// <param name="type">要检查的类型。</param>
    /// <returns>如果类型是原始类型、枚举、已知简单映射类型或某些特殊的System类型，则为true。</returns>
    private static bool IsConsideredSimpleOrSystemType(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum || // 枚举有特殊处理，但也算简单系统类型范畴
               SimpleTypeMappings.ContainsKey(type) || // 在我们的简单类型映射中
               (Nullable.GetUnderlyingType(type) != null && IsConsideredSimpleOrSystemType(Nullable.GetUnderlyingType(type)!)) || // 可空简单类型
               type == typeof(object) || // System.Object 本身不应被视为需要展开的复杂对象
               type == typeof(TimeSpan) || // TimeSpan 通常作为字符串或特定格式处理，而非嵌套对象
               (type.Namespace == "System" && !type.IsInterface && !type.IsAbstract); // 宽泛地将其他System命名空间下的非接口、非抽象类视为简单处理，除非有特定规则
    }

    /// <summary>
    /// 为枚举类型生成SelectOption列表。
    /// </summary>
    /// <param name="enumType">枚举类型 (必须是枚举)。</param>
    /// <returns>包含枚举所有成员的SelectOption列表。</returns>
    private static List<SelectOption> GetEnumSelectOptions(Type enumType)
    {
        if (!enumType.IsEnum)
        {
            throw new ArgumentException(@"类型必须是枚举类型。", nameof(enumType));
        }

        return Enum.GetValues(enumType)
            .Cast<object>()
            .Select(e =>
            {
                var memberInfo = enumType.GetField(e.ToString()!);
                var displayAttr = memberInfo?.GetCustomAttribute<DisplayAttribute>();
                return new SelectOption
                {
                    // 对于枚举，通常希望传递其字符串名称或整数值
                    // 这里传递枚举成员本身，前端可能需要根据后端如何处理枚举值来调整
                    // 例如，如果后端期望整数，可以用 Convert.ChangeType(e, Enum.GetUnderlyingType(enumType))
                    Value = e.ToString()!, // 或者 e (枚举成员本身)
                    Label = displayAttr?.GetName() ?? e.ToString()!
                };
            }).ToList();
    }

    private static List<Type>? _availableConfigTypesCache;
    private static readonly Lock _lockAvailableConfigTypes = new();

    /// <summary>
    /// 获取所有继承自 AbstractAiProcessorConfig 的具体配置类型。
    /// 使用缓存以提高性能。
    /// </summary>
    /// <returns>类型列表。</returns>
    public static IEnumerable<Type> GetAvailableAiConfigConcreteTypes()
    {
        // 先尝试快速返回（无锁）
        var cached = _availableConfigTypesCache;
        if (cached != null)
            return cached;
        lock (_lockAvailableConfigTypes)
        {
            // 双重检查锁定模式
            // 再次检查缓存是否已创建（锁内二次检查）
            cached = _availableConfigTypesCache;
            if (cached != null)
                return cached;
            var abstractConfigType = typeof(AbstractAiProcessorConfig);
            // 假设所有配置类都在 AbstractAiProcessorConfig 所在的程序集中。
            // 如果分布在多个程序集中，需要调整 Assembly.GetAssembly() 或提供程序集列表。
            _availableConfigTypesCache = Assembly.GetAssembly(abstractConfigType)!
                .GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && abstractConfigType.IsAssignableFrom(t))
                .ToList();

            int count = _availableConfigTypesCache.Count;
            Log.Info($"已发现 {count} 个 AI 配置类型。");
        }

        return _availableConfigTypesCache;
    }

    /// <summary>
    /// 根据类型的编程名称安全地获取 AI 配置类型。
    /// </summary>
    /// <param name="typeName">类型的名称 (例如 "DoubaoAiProcessorConfig")。</param>
    /// <returns>如果找到则返回类型，否则返回 null。</returns>
    public static Type? GetTypeByName(string typeName) // 使用 new 关键字隐藏基类或其他同名方法（如果存在）
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;
        return GetAvailableAiConfigConcreteTypes()
            .FirstOrDefault(t => t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
    }
}