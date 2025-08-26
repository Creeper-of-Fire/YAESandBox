using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using YAESandBox.Depend.Schema;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Depend.Storage;
using static YAESandBox.Depend.Tests.Utility;

// 引用你的 Attribute 和 Processor

namespace YAESandBox.Depend.Tests;

/// <summary>
/// 包含 YaeSchemaExporter 中 [Flatten] 属性处理机制的单元测试。
/// </summary>
public class YaeSchemaExporterFlattenTests
{
    // --- 测试用的模型定义 ---

    #region Test Models
    // 用于基础扁平化测试
    public record ChildObject
    {
        [Display(Description = "Child Property 1")]
        public string? ChildProperty1 { get; init; }
        public int ChildProperty2 { get; init; }
    }

    public record ParentWithSimpleFlatten
    {
        public string? ParentProperty { get; init; }
        [Flatten]
        public ChildObject? TheChildObject { get; init; }
    }

    // 用于冲突解决测试
    public record ChildWithConflict
    {
        [Display(Description = "From Child")]
        public string? ConflictingProperty { get; init; }
    }
    public record ParentWithConflictThrow
    {
        [Display(Description = "From Parent")]
        public string? ConflictingProperty { get; init; }
        [Flatten(FlattenConflictResolution.ThrowOnError)]
        public ChildWithConflict? TheChild { get; init; }
    }
    public record ParentWithConflictPreferParent
    {
        [Display(Description = "From Parent")]
        public string? ConflictingProperty { get; init; }
        [Flatten(FlattenConflictResolution.PreferParent)]
        public ChildWithConflict? TheChild { get; init; }
    }
    public record ParentWithConflictPreferChild
    {
        [Display(Description = "From Parent")]
        public string? ConflictingProperty { get; init; }
        [Flatten(FlattenConflictResolution.PreferChild)]
        public ChildWithConflict? TheChild { get; init; }
    }
    
    // 用于 `required` 关键字合并测试
    public record ChildWithRequired
    {
        [Required]
        public string? ChildRequired { get; init; }
        public string? ChildOptional { get; init; }
    }
    public record ParentWithRequiredAndFlatten
    {
        [Required]
        public string? ParentRequired { get; init; }
        [Flatten]
        public ChildWithRequired? TheChild { get; init; }
    }

    // 用于嵌套扁平化测试
    public record GrandChildToFlatten
    {
        public string? GrandChildProperty { get; init; }
    }
    public record ChildWithNestedFlatten
    {
        public int ChildProperty { get; init; }
        [Flatten]
        public GrandChildToFlatten? TheGrandChild { get; init; }
    }
    public record ParentWithNestedFlatten
    {
        public string? ParentProperty { get; init; }
        [Flatten]
        public ChildWithNestedFlatten? TheChild { get; init; }
    }

    // 用于对比的非扁平化测试
    public record ParentWithoutFlatten
    {
        public string? ParentProperty { get; init; }
        public ChildObject? TheChildObject { get; init; }
    }
    #endregion

    [Fact]
    public void BasicFlatten_ShouldMergeChildPropertiesAndRemoveOriginal()
    {
        // Act
        var schema = GenerateSchemaFor<ParentWithSimpleFlatten>();
        var properties = schema["properties"]!.AsObject();

        // Assert
        Assert.Equal(3, properties.Count); // parentProperty, childProperty1, childProperty2
        Assert.True(properties.ContainsKey("parentProperty"));
        Assert.True(properties.ContainsKey("childProperty1"));
        Assert.True(properties.ContainsKey("childProperty2"));
        Assert.False(properties.ContainsKey("theChildObject")); // 原始属性已被移除
    }
    
    [Fact]
    public void WithoutFlatten_ShouldRemainNested()
    {
        // Act
        var schema = GenerateSchemaFor<ParentWithoutFlatten>();
        var properties = schema["properties"]!.AsObject();

        // Assert
        Assert.Equal(2, properties.Count);
        Assert.True(properties.ContainsKey("parentProperty"));
        Assert.True(properties.ContainsKey("theChildObject"));

        var childSchema = properties["theChildObject"]!.AsObject();

        // --- 修正：健壮地检查 type 关键字 ---
        var typeNode = childSchema["type"];
        Assert.NotNull(typeNode);

        // 检查 "type" 是否为 "object" 或者一个包含 "object" 的数组
        bool isObjectType = (typeNode is JsonValue val && val.GetValue<string>() == "object") ||
                            (typeNode is JsonArray arr && arr.Any(n => n is JsonValue v && v.GetValue<string>() == "object"));
    
        Assert.True(isObjectType, "Schema 'type' should be 'object' or contain 'object'.");
    
        Assert.True(childSchema["properties"]!.AsObject().ContainsKey("childProperty1"));
    }

    [Fact]
    public void ConflictWithThrowPolicy_ShouldThrowException()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(GenerateSchemaFor<ParentWithConflictThrow>);
        Assert.Contains("Flattening failed due to a property name conflict", exception.Message);
        Assert.Contains("'conflictingProperty'", exception.Message);
    }

    [Fact]
    public void ConflictWithPreferParentPolicy_ShouldKeepParentPropertySchema()
    {
        // Act
        var schema = GenerateSchemaFor<ParentWithConflictPreferParent>();
        var properties = schema["properties"]!.AsObject();
        var conflictingPropSchema = properties["conflictingProperty"]!.AsObject();

        // Assert
        Assert.Single(properties); // 只有一个属性
        Assert.True(properties.ContainsKey("conflictingProperty"));
        Assert.Equal("From Parent", conflictingPropSchema["description"]!.GetValue<string>());
    }

    [Fact]
    public void ConflictWithPreferChildPolicy_ShouldUseChildPropertySchema()
    {
        // Act
        var schema = GenerateSchemaFor<ParentWithConflictPreferChild>();
        var properties = schema["properties"]!.AsObject();
        var conflictingPropSchema = properties["conflictingProperty"]!.AsObject();

        // Assert
        Assert.Single(properties);
        Assert.True(properties.ContainsKey("conflictingProperty"));
        Assert.Equal("From Child", conflictingPropSchema["description"]!.GetValue<string>());
    }

    [Fact]
    public void Flatten_ShouldMergeRequiredFieldsCorrectly()
    {
        // Act
        var schema = GenerateSchemaFor<ParentWithRequiredAndFlatten>();
        var requiredArray = schema["required"]!.AsArray();
        
        // Assert
        Assert.Equal(2, requiredArray.Count);
        Assert.Contains("parentRequired", requiredArray.Select(n => n!.GetValue<string>()));
        Assert.Contains("childRequired", requiredArray.Select(n => n!.GetValue<string>()));
    }

    [Fact]
    public void NestedFlatten_ShouldFlattenAllLevelsToRoot()
    {
        // Act
        var schema = GenerateSchemaFor<ParentWithNestedFlatten>();
        var properties = schema["properties"]!.AsObject();

        // Assert
        Assert.Equal(3, properties.Count);
        Assert.True(properties.ContainsKey("parentProperty"));
        Assert.True(properties.ContainsKey("childProperty"));
        Assert.True(properties.ContainsKey("grandChildProperty"));
        
        // 确保中间层级的属性已被移除
        Assert.False(properties.ContainsKey("theChild"));
        Assert.False(properties.ContainsKey("theGrandChild"));
    }
}