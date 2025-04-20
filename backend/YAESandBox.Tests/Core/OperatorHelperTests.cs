using FluentAssertions;
using YAESandBox.Core.State.Entity;

// 需要 для List 和 Dictionary

// 需要 для INumber<T>

namespace YAESandBox.Tests.Core;

/// <summary>
/// 针对 OperatorHelper 的单元测试。
/// </summary>
public class OperatorHelperTests
{
    /// <summary>
    /// 测试 StringToOperator 方法的转换。
    /// </summary>
    [Theory]
    [InlineData("=", Operator.Equal)]
    [InlineData("+=", Operator.Add)]
    [InlineData("+", Operator.Add)]
    [InlineData("-=", Operator.Sub)]
    [InlineData("-", Operator.Sub)]
    public void StringToOperator_对于有效字符串_应返回正确的Operator枚举(string input, Operator expected)
    {
        // Act
        var result = OperatorHelper.StringToOperator(input);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// 测试 StringToOperator 对无效字符串抛出异常。
    /// </summary>
    [Fact]
    public void StringToOperator_对于无效字符串_应抛出ArgumentException()
    {
        // Arrange
        string? invalidOp = "*";
        Action act = () => OperatorHelper.StringToOperator(invalidOp);

        // Act & Assert
        act.Should().Throw<ArgumentException>().WithMessage($"不支持 '{invalidOp}'*"); // 检查异常消息
    }

    // --- 测试 ChangedValue ---

    /// <summary>
    /// 测试当 oldValue 为 null 时，ChangedValue 应返回 newValue。
    /// </summary>
    [Fact]
    public void ChangedValue_当OldValue为Null时_应返回NewValue()
    {
        // Arrange
        object? oldValue = null;
        string? newValue = "new value";
        var op = Operator.Equal; // 操作符不重要

        // Act
        object? result = op.ChangedValue(oldValue, newValue);

        // Assert
        result.Should().Be(newValue);
    }

    /// <summary>
    /// 测试对数值类型的 ChangedValue。
    /// </summary>
    [Theory]
    // int
    [InlineData(Operator.Equal, 10, 5, 5)]
    [InlineData(Operator.Add, 10, 5, 15)]
    [InlineData(Operator.Sub, 10, 3, 7)]
    // double
    [InlineData(Operator.Equal, 10.5d, 2.5d, 2.5d)]
    [InlineData(Operator.Add, 10.5d, 2.0d, 12.5d)]
    [InlineData(Operator.Sub, 10.5d, 1.5d, 9.0d)]
    // float - 注意 C# 中常量需要 'f' 后缀
    [InlineData(Operator.Equal, 10.5f, 2.5f, 2.5f)]
    [InlineData(Operator.Add, 10.5f, 2.0f, 12.5f)]
    [InlineData(Operator.Sub, 10.5f, 1.5f, 9.0f)]
    public void ChangedValue_对于数值类型_应正确应用操作符<T>(Operator op, T oldValue, T newValue, T expected) where T : struct
    {
        // Act
        object? result = op.ChangedValue(oldValue, newValue);

        // Assert
        result.Should().BeOfType<T>(); // 确认类型正确
        result.Should().Be(expected);
    }

    /// <summary>
    /// 测试对字符串类型的 ChangedValue。
    /// </summary>
    [Theory]
    [InlineData(Operator.Equal, "old", "new", "new")]
    [InlineData(Operator.Add, "old", "new", "oldnew")]
    [InlineData(Operator.Sub, "old", "new", "old")] // Sub 不支持，应返回 oldValue
    public void ChangedValue_对于字符串类型_应正确应用操作符(Operator op, string oldValue, string newValue, string expected)
    {
        // Act
        object? result = op.ChangedValue(oldValue, newValue);

        // Assert
        result.Should().BeOfType<string>();
        result.Should().Be(expected);
    }

    /// <summary>
    /// 测试对 TypedID 类型的 ChangedValue。
    /// </summary>
    [Fact]
    public void ChangedValue_对于TypedID类型_应只支持Equal()
    {
        // Arrange
        var oldTid = new TypedID(EntityType.Item, "item_old");
        var newTid = new TypedID(EntityType.Item, "item_new");

        // Act & Assert
        Operator.Equal.ChangedValue(oldTid, newTid).Should().Be(newTid);
        Operator.Add.ChangedValue(oldTid, newTid).Should().Be(oldTid); // Add 不支持，返回 old
        Operator.Sub.ChangedValue(oldTid, newTid).Should().Be(oldTid); // Sub 不支持，返回 old

        // 测试 newValue 类型不匹配的情况
        Operator.Equal.ChangedValue(oldTid, "not a tid").Should().Be(oldTid); // 类型不匹配，返回 old
    }

    /// <summary>
    /// 测试对 List<object> 类型的 ChangedValue。
    /// </summary>
    [Fact]
    public void ChangedValue_对于List类型_应正确应用操作符()
    {
        // Arrange
        var oldList = new List<object> { 1, "a" };

        // --- Operator.Equal ---
        var newListForEq = new List<object> { "b", 2 };
        Operator.Equal.ChangedValue(oldList, newListForEq).Should().BeEquivalentTo(newListForEq).And
            .NotBeSameAs(newListForEq); // 内容相等，实例不同
        Operator.Equal.ChangedValue(oldList, "not a list").Should()
            .BeEquivalentTo(new List<object> { "not a list" }); // newValue 不是列表，返回包含 newValue 的新列表

        // --- Operator.Add ---
        // Add single item
        Operator.Add.ChangedValue(oldList, "b").Should().BeEquivalentTo(new List<object> { 1, "a", "b" });
        // Add list
        var listToAdd = new List<object> { 2, "c" };
        Operator.Add.ChangedValue(oldList, listToAdd).Should().BeEquivalentTo(new List<object> { 1, "a", 2, "c" });

        // --- Operator.Sub ---
        // Remove single item
        Operator.Sub.ChangedValue(oldList, "a").Should().BeEquivalentTo(new List<object> { 1 });
        // Remove non-existent item
        Operator.Sub.ChangedValue(oldList, "z").Should().BeEquivalentTo(oldList);
        // Remove list
        var listToRemove = new List<object> { 1, "z" };
        Operator.Sub.ChangedValue(oldList, listToRemove).Should().BeEquivalentTo(new List<object> { "a" });
    }

    /// <summary>
    /// 测试对 Dictionary<string, object?> 类型的 ChangedValue。
    /// </summary>
    [Fact]
    public void ChangedValue_对于Dictionary类型_应正确应用操作符()
    {
        // Arrange
        var oldDict = new Dictionary<string, object?> { { "key1", 1 }, { "key2", "a" } };

        // --- Operator.Equal ---
        var newDictForEq = new Dictionary<string, object?> { { "key3", true } };
        Operator.Equal.ChangedValue(oldDict, newDictForEq).Should().BeEquivalentTo(newDictForEq).And
            .NotBeSameAs(newDictForEq); // 内容相等，实例不同
        Operator.Equal.ChangedValue(oldDict, "not a dict").Should().BeEquivalentTo(oldDict); // newValue 类型不匹配，返回 oldDict

        // --- Operator.Add ---
        // Add KeyValuePair (new key)
        Operator.Add.ChangedValue(oldDict, new KeyValuePair<string, object?>("key3", "new")).Should()
            .BeEquivalentTo(new Dictionary<string, object?> { { "key1", 1 }, { "key2", "a" }, { "key3", "new" } });
        // Add KeyValuePair (existing key - ignored)
        Operator.Add.ChangedValue(oldDict, new KeyValuePair<string, object?>("key1", 99)).Should()
            .BeEquivalentTo(oldDict);
        // Add Dictionary (adds only new keys)
        var dictToAdd = new Dictionary<string, object?> { { "key2", "ignored" }, { "key4", null } };
        Operator.Add.ChangedValue(oldDict, dictToAdd).Should().BeEquivalentTo(new Dictionary<string, object?>
            { { "key1", 1 }, { "key2", "a" }, { "key4", null } });
        // Add invalid type
        Operator.Add.ChangedValue(oldDict, 123).Should().BeEquivalentTo(oldDict); // 类型不匹配，返回 oldDict

        // --- Operator.Sub ---
        // Remove single key (string)
        Operator.Sub.ChangedValue(oldDict, "key1").Should()
            .BeEquivalentTo(new Dictionary<string, object?> { { "key2", "a" } });
        // Remove non-existent key (string)
        Operator.Sub.ChangedValue(oldDict, "keyZ").Should().BeEquivalentTo(oldDict);
        // Remove multiple keys (List<string>)
        Operator.Sub.ChangedValue(oldDict, new List<string> { "key2", "keyZ" }).Should()
            .BeEquivalentTo(new Dictionary<string, object?> { { "key1", 1 } });
        // Remove invalid type
        Operator.Sub.ChangedValue(oldDict, 123).Should().BeEquivalentTo(oldDict); // 类型不匹配，返回 oldDict
    }
}