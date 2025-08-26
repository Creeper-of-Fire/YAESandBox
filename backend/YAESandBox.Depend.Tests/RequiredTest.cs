using System.ComponentModel.DataAnnotations;
using static YAESandBox.Depend.Tests.Utility;

namespace YAESandBox.Depend.Tests;

public record SimpleRequiredModel
{
    [Required] public string RequiredProperty { get; init; } = "";
    public int OptionalProperty { get; init; }
}

public record NestedRequiredModel
{
    [Required] public string ParentRequired { get; init; } = "";

    // 注意：这里的 Child 属性本身不是必需的
    public SimpleRequiredModel? Child { get; init; }
}

public class YaeSchemaExporterRequiredTests
{
    [Fact]
    public void RequiredProperty_ShouldBeInRequiredArray()
    {
        // Act
        var schema = GenerateSchemaFor<SimpleRequiredModel>();
        var requiredArray = schema["required"]!.AsArray();

        // Assert
        Assert.NotNull(schema["required"]);
        Assert.Single(requiredArray);
        Assert.Equal("requiredProperty", requiredArray[0]!.GetValue<string>());
    }

    [Fact]
    public void OptionalProperty_ShouldNotBeInRequiredArray()
    {
        // Act
        var schema = GenerateSchemaFor<SimpleRequiredModel>();
        var properties = schema["properties"]!.AsObject();

        // Assert
        // 确认 'required' 数组存在且只包含 'requiredProperty'
        Assert.True(schema.AsObject().ContainsKey("required"));
        var requiredArray = schema["required"]!.AsArray();
        Assert.DoesNotContain("optionalProperty", requiredArray.Select(n => n!.GetValue<string>()));

        // 同时确认 optionalProperty 作为一个属性是存在的
        Assert.True(properties.ContainsKey("optionalProperty"));
    }

    [Fact]
    public void NestedObject_ShouldHaveItsOwnRequiredArray()
    {
        // Act
        var schema = GenerateSchemaFor<NestedRequiredModel>();

        // 检查根对象的 'required' 数组
        var rootRequiredArray = schema["required"]!.AsArray();
        Assert.Single(rootRequiredArray);
        Assert.Equal("parentRequired", rootRequiredArray[0]!.GetValue<string>());

        // 检查嵌套对象的 'required' 数组
        var childSchema = schema["properties"]!["child"]!.AsObject();
        var childRequiredArray = childSchema["required"]!.AsArray();
        Assert.Single(childRequiredArray);
        Assert.Equal("requiredProperty", childRequiredArray[0]!.GetValue<string>());
    }

    [Fact]
    public void CombinedFlattenAndRequired_ShouldMergeCorrectly()
    {
        // 在之前的测试模型区添加这个模型
        // public record ParentWithRequiredAndFlatten { ... }
        // public record ChildWithRequired { ... }

        // Act
        var schema = GenerateSchemaFor<YaeSchemaExporterFlattenTests.ParentWithRequiredAndFlatten>();
        var requiredArray = schema["required"]!.AsArray();

        // Assert
        Assert.NotNull(requiredArray);
        Assert.Equal(2, requiredArray.Count);

        var requiredValues = requiredArray.Select(n => n?.GetValue<string>()).ToHashSet();
        Assert.Contains("parentRequired", requiredValues);
        Assert.Contains("childRequired", requiredValues);
    }
}