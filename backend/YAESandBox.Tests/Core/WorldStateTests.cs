using Xunit;
using FluentAssertions;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;

namespace YAESandBox.Core.Tests;

/// <summary>
/// 针对 WorldState 的单元测试。
/// </summary>
public class WorldStateTests
{
    private WorldState CreateTestWorldState() => new();

    /// <summary>
    /// 测试向 WorldState 添加不同类型的实体。
    /// </summary>
    [Fact]
    public void AddEntity_应能成功添加不同类型的实体()
    {
        // Arrange
        var ws = this.CreateTestWorldState();
        var item = new Item("item_sword");
        var character = new Character("char_hero");
        var place = new Place("place_castle");

        // Act
        ws.AddEntity(item);
        ws.AddEntity(character);
        ws.AddEntity(place);

        // Assert
        ws.Items.Should().ContainKey("item_sword").WhoseValue.Should().BeSameAs(item);
        ws.Characters.Should().ContainKey("char_hero").WhoseValue.Should().BeSameAs(character);
        ws.Places.Should().ContainKey("place_castle").WhoseValue.Should().BeSameAs(place);
    }

    /// <summary>
    /// 测试添加具有相同 ID 和类型的实体（应覆盖）。
    /// </summary>
    [Fact]
    public void AddEntity_添加重复ID和类型的实体_应覆盖旧实体()
    {
        // Arrange
        var ws = this.CreateTestWorldState();
        var item1 = new Item("item_apple") { IsDestroyed = false };
        var item2 = new Item("item_apple") { IsDestroyed = false }; // 相同 ID
        item2.SetAttribute("color", "red"); // 新实体有不同属性

        // Act
        ws.AddEntity(item1);
        ws.AddEntity(item2); // 添加第二个，应覆盖第一个

        // Assert
        ws.Items.Should().HaveCount(1); // 确认只有一个实体
        ws.Items.Should().ContainKey("item_apple").WhoseValue.Should().BeSameAs(item2); // 确认是第二个实体
        ws.Items["item_apple"].GetAttribute("color").Should().Be("red"); // 确认属性来自第二个实体
        // 注意：根据 AddEntity 实现，覆盖已存在且未销毁的实体会记录警告
    }

     /// <summary>
    /// 测试添加具有相同 ID 但不同类型的实体。
    /// </summary>
    [Fact]
    public void AddEntity_添加相同ID但不同类型的实体_应都存在()
    {
        // Arrange
        var ws = this.CreateTestWorldState();
        var item = new Item("shared_id");
        var character = new Character("shared_id");

        // Act
        ws.AddEntity(item);
        ws.AddEntity(character);

        // Assert
        ws.Items.Should().ContainKey("shared_id").WhoseValue.Should().BeSameAs(item);
        ws.Characters.Should().ContainKey("shared_id").WhoseValue.Should().BeSameAs(character);
    }


    /// <summary>
    /// 测试 FindEntity 使用 TypedID 查找实体。
    /// </summary>
    [Fact]
    public void FindEntity_使用TypedID_应能精确查找实体()
    {
        // Arrange
        var ws = this.CreateTestWorldState();
        var item = new Item("item_find_tid");
        var character = new Character("char_find_tid");
        var destroyedItem = new Item("item_destroyed_tid") { IsDestroyed = true };
        ws.AddEntity(item);
        ws.AddEntity(character);
        ws.AddEntity(destroyedItem);

        var itemTid = new TypedID(EntityType.Item, "item_find_tid");
        var charTid = new TypedID(EntityType.Character, "char_find_tid");
        var destroyedTid = new TypedID(EntityType.Item, "item_destroyed_tid");
        var nonExistentTid = new TypedID(EntityType.Place, "place_nonexistent");

        // Act & Assert
        // 查找存在的
        ws.FindEntity(itemTid).Should().BeSameAs(item);
        ws.FindEntity(charTid).Should().BeSameAs(character);

        // 查找不存在的
        ws.FindEntity(nonExistentTid).Should().BeNull();

        // 查找已销毁的 (默认不包含)
        ws.FindEntity(destroyedTid).Should().BeNull();

        // 查找已销毁的 (显式包含)
        ws.FindEntity(destroyedTid, includeDestroyed: true).Should().BeSameAs(destroyedItem);
    }

    /// <summary>
    /// 测试 FindEntityById 使用 ID 和类型查找实体。
    /// </summary>
    [Fact]
    public void FindEntityById_使用ID和类型_应能精确查找实体()
    {
        // Arrange
        var ws = this.CreateTestWorldState();
        var item = new Item("item_find_id");
        var character = new Character("char_find_id");
        var destroyedItem = new Item("item_destroyed_id") { IsDestroyed = true };
        ws.AddEntity(item);
        ws.AddEntity(character);
        ws.AddEntity(destroyedItem);

        // Act & Assert
        // 查找存在的
        ws.FindEntityById("item_find_id", EntityType.Item).Should().BeSameAs(item);
        ws.FindEntityById("char_find_id", EntityType.Character).Should().BeSameAs(character);

        // 查找不存在的 ID
        ws.FindEntityById("nonexistent_id", EntityType.Item).Should().BeNull();

        // 查找存在的 ID 但类型错误
        ws.FindEntityById("item_find_id", EntityType.Place).Should().BeNull();

        // 查找已销毁的 (默认不包含)
        ws.FindEntityById("item_destroyed_id", EntityType.Item).Should().BeNull();

        // 查找已销毁的 (显式包含)
        ws.FindEntityById("item_destroyed_id", EntityType.Item, includeDestroyed: true).Should().BeSameAs(destroyedItem);
    }

     /// <summary>
    /// 测试 WorldState 的 Clone 方法。
    /// </summary>
    [Fact]
    public void Clone_应创建包含独立实体副本的WorldState副本()
    {
        // Arrange
        var originalWs = this.CreateTestWorldState();
        var item1 = new Item("item_clone1");
        item1.SetAttribute("value", 10);
        var char1 = new Character("char_clone1");
        char1.SetAttribute("level", 1);
        originalWs.AddEntity(item1);
        originalWs.AddEntity(char1);

        // Act
        var clonedWs = originalWs.Clone();

        // Assert
        clonedWs.Should().NotBeSameAs(originalWs); // 确认是不同实例

        // 确认包含相同 ID 的实体
        clonedWs.Items.Should().ContainKey("item_clone1");
        clonedWs.Characters.Should().ContainKey("char_clone1");

        // 获取克隆体中的实体
        var clonedItem = clonedWs.FindEntityById("item_clone1", EntityType.Item);
        var clonedChar = clonedWs.FindEntityById("char_clone1", EntityType.Character);

        clonedItem.Should().NotBeNull();
        clonedChar.Should().NotBeNull();

        // 确认实体本身是不同的实例
        clonedItem.Should().NotBeSameAs(item1);
        clonedChar.Should().NotBeSameAs(char1);

        // 确认克隆体中的实体属性与原始一致
        clonedItem!.GetAttribute("value").Should().Be(10);
        clonedChar!.GetAttribute("level").Should().Be(1);

        // --- 验证深拷贝行为 ---
        // 修改克隆体中的实体属性
        clonedItem.SetAttribute("value", 20);
        clonedChar.SetAttribute("level", 2);
        clonedWs.AddEntity(new Place("place_new_in_clone")); // 在克隆体中添加新实体

        // 确认原始 WorldState 中的实体属性未改变
        item1.GetAttribute("value").Should().Be(10);
        char1.GetAttribute("level").Should().Be(1);

        // 确认原始 WorldState 中没有新添加的实体
        originalWs.FindEntityById("place_new_in_clone", EntityType.Place).Should().BeNull();
        originalWs.Places.Should().BeEmpty();
    }
}