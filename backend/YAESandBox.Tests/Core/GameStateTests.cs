using FluentAssertions;
using YAESandBox.Core.State;

// 需要 для Dictionary

namespace YAESandBox.Core.Tests;

/// <summary>
/// 针对 GameState 的单元测试。
/// </summary>
public class GameStateTests
{
    private GameState CreateTestGameState() => new();

    /// <summary>
    /// 测试使用索引器设置和获取不同类型的值。
    /// </summary>
    [Fact]
    public void Indexer_应能设置和获取各种类型的值()
    {
        // Arrange
        var gs = this.CreateTestGameState();
        var stringVal = "test string";
        var intVal = 42;
        var boolVal = false;
        var listVal = new List<int> { 1, 2, 3 };
        var nullVal = (object?)null;

        // Act
        gs["stringKey"] = stringVal;
        gs["intKey"] = intVal;
        gs["boolKey"] = boolVal;
        gs["listKey"] = listVal;
        gs["nullKey"] = nullVal;

        // Assert
        gs["stringKey"].Should().Be(stringVal);
        gs["intKey"].Should().Be(intVal);
        gs["boolKey"].Should().Be(boolVal);
        gs["listKey"].Should().BeSameAs(listVal); // GameState 默认浅拷贝，应为同一实例
        gs["nullKey"].Should().BeNull();
        gs["nonExistentKey"].Should().BeNull(); // 获取不存在的键应返回 null
    }

    /// <summary>
    /// 测试 TryGetValue 方法。
    /// </summary>
    [Fact]
    public void TryGetValue_应能成功获取正确类型的值并处理类型不匹配()
    {
        // Arrange
        var gs = this.CreateTestGameState();
        gs["age"] = 30;
        gs["name"] = "Alice";

        // Act & Assert
        // 成功获取
        gs.TryGetValue<int>("age", out var age).Should().BeTrue();
        age.Should().Be(30);

        gs.TryGetValue<string>("name", out var name).Should().BeTrue();
        name.Should().Be("Alice");

        // 获取不存在的键
        gs.TryGetValue<bool>("isActive", out var isActive).Should().BeFalse();
        isActive.Should().Be(default); // false

        // 类型不匹配
        gs.TryGetValue<string>("age", out var ageString).Should().BeFalse();
        ageString.Should().Be(default); // null
    }

    /// <summary>
    /// 测试 Remove 方法。
    /// </summary>
    [Fact]
    public void Remove_应能移除存在的键并对不存在的键返回false()
    {
        // Arrange
        var gs = this.CreateTestGameState();
        gs["keyToRemove"] = "value";
        gs["keyToKeep"] = 123;

        // Act
        var resultRemoved = gs.Remove("keyToRemove");
        var resultNonExistent = gs.Remove("nonExistentKey");

        // Assert
        resultRemoved.Should().BeTrue();
        gs["keyToRemove"].Should().BeNull(); // 确认值已被移除 (访问返回 null)
        // 或者检查 GetAllSettings()
        gs.GetAllSettings().Should().NotContainKey("keyToRemove");


        resultNonExistent.Should().BeFalse();

        gs["keyToKeep"].Should().Be(123); // 确认其他键未受影响
    }

    /// <summary>
    /// 测试 GetAllSettings 方法。
    /// </summary>
    [Fact]
    public void GetAllSettings_应返回包含所有设置的只读字典副本()
    {
        // Arrange
        var gs = this.CreateTestGameState();
        gs["setting1"] = "value1";
        gs["setting2"] = 2;

        // Act
        var settings = gs.GetAllSettings();

        // Assert
        settings.Should().HaveCount(2);
        settings.Should().ContainKey("setting1").WhoseValue.Should().Be("value1");
        settings.Should().ContainKey("setting2").WhoseValue.Should().Be(2);

        // 验证返回的是副本
        // 尝试修改返回的字典（如果它不是真正的只读类型，这会抛出异常，但 IReadOnlyDictionary 本身不支持修改）
        // settings.Add("setting3", 3); // 这行会编译错误，证明是只读接口
        // 如果返回的是 Dictionary 的副本，可以这样测试：
        if (settings is Dictionary<string, object?> settingsDict) // 确认是字典实例
        {
             settingsDict["setting1"] = "newValue"; // 修改副本
             gs["setting1"].Should().Be("value1"); // 确认原始 GameState 未改变
        }

    }

    /// <summary>
    /// 测试 GameState 的 Clone 方法。
    /// </summary>
    [Fact]
    public void Clone_应创建包含相同设置的独立副本()
    {
        // Arrange
        var originalGs = this.CreateTestGameState();
        var listValue = new List<string> { "a", "b" };
        originalGs["key1"] = "value1";
        originalGs["listKey"] = listValue;

        // Act
        var clonedGs = originalGs.Clone();

        // Assert
        clonedGs.Should().NotBeSameAs(originalGs); // 确认是不同实例

        // 确认包含相同的值
        clonedGs["key1"].Should().Be("value1");
        clonedGs["listKey"].Should().BeEquivalentTo(listValue); // 内容相同

        // --- 验证浅拷贝行为 ---
        // 修改克隆体
        clonedGs["key1"] = "newValue";
        var clonedList = clonedGs["listKey"] as List<string>;
        clonedList?.Add("c"); // 修改克隆体中的列表

        // 确认原始 GameState 未改变
        originalGs["key1"].Should().Be("value1"); // 字符串是不可变的，所以这里没问题
        // 但列表是引用类型，浅拷贝意味着它们指向同一个列表实例
        listValue.Should().HaveCount(3).And.Contain("c"); // 原始列表也被修改了！
        originalGs["listKey"].Should().BeSameAs(clonedGs["listKey"]); // 确认是同一个列表实例
    }
}