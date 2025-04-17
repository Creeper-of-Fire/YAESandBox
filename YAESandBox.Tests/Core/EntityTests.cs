using Xunit;
using FluentAssertions;
using YAESandBox.Core.State.Entity;
using System.Collections.Generic; // 需要 для List 和 Dictionary

namespace YAESandBox.Core.Tests;

/// <summary>
/// 针对 BaseEntity 及其子类的单元测试。
/// </summary>
public class EntityTests
{
    private Item CreateTestItem(string id = "item_test", bool isDestroyed = false)
    {
        return new Item(id) { IsDestroyed = isDestroyed };
    }

    private Character CreateTestCharacter(string id = "char_test", bool isDestroyed = false)
    {
        return new Character(id) { IsDestroyed = isDestroyed };
    }

    private Place CreateTestPlace(string id = "place_test", bool isDestroyed = false)
    {
        return new Place(id) { IsDestroyed = isDestroyed };
    }

    /// <summary>
    /// 测试实体构造函数是否正确设置 ID。
    /// </summary>
    [Fact]
    public void Constructor_应正确设置EntityId()
    {
        // Arrange
        var expectedId = "unique_id_123";

        // Act
        var item = CreateTestItem(expectedId);
        var character = CreateTestCharacter(expectedId);
        var place = CreateTestPlace(expectedId);

        // Assert
        item.EntityId.Should().Be(expectedId);
        character.EntityId.Should().Be(expectedId);
        place.EntityId.Should().Be(expectedId);
    }

    /// <summary>
    /// 测试实体构造函数是否拒绝 null 或空白 ID。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_对于无效ID_应抛出ArgumentException(string invalidId)
    {
        // Arrange
        System.Action createItem = () => new Item(invalidId);
        System.Action createChar = () => new Character(invalidId);
        System.Action createPlace = () => new Place(invalidId);

        // Act & Assert
        createItem.Should().Throw<ArgumentException>();
        createChar.Should().Throw<ArgumentException>();
        createPlace.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// 测试获取核心属性。
    /// </summary>
    [Fact]
    public void GetAttribute_获取核心属性_应返回正确的值()
    {
        // Arrange
        var entity = CreateTestItem("item_core", isDestroyed: true);

        // Act
        var id = entity.GetAttribute(nameof(BaseEntity.EntityId));
        var type = entity.GetAttribute(nameof(BaseEntity.EntityType));
        var destroyed = entity.GetAttribute(nameof(BaseEntity.IsDestroyed));

        // Assert
        id.Should().Be("item_core");
        type.Should().Be(EntityType.Item);
        destroyed.Should().Be(true);
    }

    /// <summary>
    /// 测试设置和获取动态属性。
    /// </summary>
    [Fact]
    public void SetAttribute_GetAttribute_对于动态属性_应能设置和获取各种类型的值()
    {
        // Arrange
        var entity = CreateTestCharacter("char_dynamic");
        var stringVal = "value1";
        var intVal = 123;
        var boolVal = true;
        var listVal = new List<object> { 1, "two", true };
        var dictVal = new Dictionary<string, object?> { { "key1", 10 }, { "key2", null } };
        var typedIdVal = new TypedID(EntityType.Place, "place_home");

        // Act
        entity.SetAttribute("stringAttr", stringVal);
        entity.SetAttribute("intAttr", intVal);
        entity.SetAttribute("boolAttr", boolVal);
        entity.SetAttribute("listAttr", listVal);
        entity.SetAttribute("dictAttr", dictVal);
        entity.SetAttribute("typedIdAttr", typedIdVal);
        entity.SetAttribute("nullAttr", null);

        // Assert
        entity.GetAttribute("stringAttr").Should().Be(stringVal);
        entity.GetAttribute("intAttr").Should().Be(intVal);
        entity.GetAttribute("boolAttr").Should().Be(boolVal);
        entity.GetAttribute("listAttr").Should().BeEquivalentTo(listVal); // 比较列表内容
        entity.GetAttribute("dictAttr").Should().BeEquivalentTo(dictVal); // 比较字典内容
        entity.GetAttribute("typedIdAttr").Should().Be(typedIdVal);
        entity.GetAttribute("nullAttr").Should().BeNull();
        entity.HasAttribute("nullAttr").Should().BeTrue(); // 确认属性存在，即使值为 null
    }

     /// <summary>
    /// 测试 TryGetAttribute 方法。
    /// </summary>
    [Fact]
    public void TryGetAttribute_应能成功获取正确类型的值并处理类型不匹配()
    {
        // Arrange
        var entity = CreateTestItem("item_tryget");
        entity.SetAttribute("count", 5);
        entity.SetAttribute("description", "A test item.");
        entity.SetAttribute("location", new TypedID(EntityType.Place, "place_chest"));
        entity.SetAttribute("tags", new List<object> { "tag1", "tag2" }); // 存为 List<object>

        // Act & Assert
        // 成功获取
        entity.TryGetAttribute<int>("count", out var count).Should().BeTrue();
        count.Should().Be(5);

        entity.TryGetAttribute<string>("description", out var desc).Should().BeTrue();
        desc.Should().Be("A test item.");

        entity.TryGetAttribute<TypedID>("location", out var loc).Should().BeTrue();
        loc.Should().Be(new TypedID(EntityType.Place, "place_chest"));

        // 尝试获取 nullable TypedID
        entity.TryGetAttribute<TypedID?>("location", out var nullableLoc).Should().BeTrue();
        nullableLoc.Should().Be(new TypedID(EntityType.Place, "place_chest"));


        // 获取不存在的属性
        entity.TryGetAttribute<double>("weight", out var weight).Should().BeFalse();
        weight.Should().Be(default); // 默认值 0.0

        // 类型不匹配
        entity.TryGetAttribute<bool>("count", out var isCountBool).Should().BeFalse();
        isCountBool.Should().Be(default); // 默认值 false

        // 注意：TryGetAttribute<List<TypedID>> 对于 List<object> 的转换比较特殊，需要测试
        // 因为 List<object> 不能直接转换为 List<TypedID>，除非里面的元素确实都是 TypedID
        // entity.TryGetAttribute<List<string>>("tags", out var tags).Should().BeFalse(); // 如果 Cast 失败会返回 false

        // 如果 List<object> 存的是 TypedID
        var typedIdList = new List<object> { new TypedID(EntityType.Item, "item_1"), new TypedID(EntityType.Item, "item_2") };
        entity.SetAttribute("relatedItems", typedIdList);
        entity.TryGetAttribute<List<TypedID>>("relatedItems", out var related).Should().BeTrue();
        related.Should().HaveCount(2).And.ContainInOrder(typedIdList.Cast<TypedID>());

         // 如果 List<object> 存的不是 TypedID
        entity.SetAttribute("mixedList", new List<object> { 1, "string" });
        entity.TryGetAttribute<List<TypedID>>("mixedList", out _).Should().BeFalse();
    }


    /// <summary>
    /// 测试设置核心属性 IsDestroyed。
    /// </summary>
    [Fact]
    public void SetAttribute_设置IsDestroyed_应更新属性值()
    {
        // Arrange
        var entity = CreateTestPlace("place_set");

        // Act
        entity.SetAttribute(nameof(BaseEntity.IsDestroyed), true);

        // Assert
        entity.IsDestroyed.Should().BeTrue();
        entity.GetAttribute(nameof(BaseEntity.IsDestroyed)).Should().Be(true);
    }

    /// <summary>
    /// 测试尝试设置只读核心属性 (EntityId, EntityType)。
    /// </summary>
    [Fact]
    public void SetAttribute_尝试设置只读核心属性_应被忽略()
    {
        // Arrange
        var entity = CreateTestItem("item_readonly");
        var originalId = entity.EntityId;
        var originalType = entity.EntityType;

        // Act
        entity.SetAttribute(nameof(BaseEntity.EntityId), "new_id");
        entity.SetAttribute(nameof(BaseEntity.EntityType), EntityType.Character);

        // Assert
        entity.EntityId.Should().Be(originalId); // ID 未改变
        entity.EntityType.Should().Be(originalType); // Type 未改变
        // 应该有日志警告，但单元测试通常不检查日志输出
    }

    /// <summary>
    /// 测试 ModifyAttribute 使用 Operator.Equal。
    /// </summary>
    [Fact]
    public void ModifyAttribute_使用Equal操作符_应等同于SetAttribute()
    {
        // Arrange
        var entity = CreateTestCharacter("char_modify_eq");
        var typedIdVal = new TypedID(EntityType.Item, "item_weapon");

        // Act
        entity.ModifyAttribute("level", Operator.Equal, 5);
        entity.ModifyAttribute("weapon", Operator.Equal, typedIdVal);
        entity.ModifyAttribute(nameof(BaseEntity.IsDestroyed), Operator.Equal, true); // 修改核心属性

        // Assert
        entity.GetAttribute("level").Should().Be(5);
        entity.GetAttribute("weapon").Should().Be(typedIdVal);
        entity.IsDestroyed.Should().BeTrue();
    }

    /// <summary>
    /// 测试 ModifyAttribute 对数值使用 Add/Sub 操作符。
    /// </summary>
    [Theory]
    [InlineData(10, Operator.Add, 5, 15)] // int
    [InlineData(10, Operator.Sub, 3, 7)]  // int
    [InlineData(10.5, Operator.Add, 2.0, 12.5)] // double
    [InlineData(10.5f, Operator.Sub, 1.5f, 9.0f)] // float
    public void ModifyAttribute_对数值使用AddSub_应执行算术运算<T>(T initialValue, Operator op, T changeValue, T expectedValue) where T : struct
    {
        // Arrange
        var entity = CreateTestItem("item_modify_num");
        entity.SetAttribute("value", initialValue);

        // Act
        entity.ModifyAttribute("value", op, changeValue);

        // Assert
        entity.GetAttribute("value").Should().Be(expectedValue);
    }

     /// <summary>
    /// 测试 ModifyAttribute 对字符串使用 Add 操作符。
    /// </summary>
    [Fact]
    public void ModifyAttribute_对字符串使用Add_应执行拼接()
    {
        // Arrange
        var entity = CreateTestPlace("place_modify_str");
        entity.SetAttribute("description", "Old ");

        // Act
        entity.ModifyAttribute("description", Operator.Add, "description.");

        // Assert
        entity.GetAttribute("description").Should().Be("Old description.");
    }

    /// <summary>
    /// 测试 ModifyAttribute 对列表使用 Add/Sub 操作符。
    /// </summary>
    [Fact]
    public void ModifyAttribute_对列表使用AddSub_应添加或移除元素()
    {
        // Arrange
        var entity = CreateTestCharacter("char_modify_list");
        var initialList = new List<object> { "apple", 1 };
        entity.SetAttribute("inventory", initialList); // 设置初始列表

        // Act: Add single item
        entity.ModifyAttribute("inventory", Operator.Add, "banana");
        // Assert: Add single item
        entity.GetAttribute("inventory").Should().BeEquivalentTo(new List<object> { "apple", 1, "banana" });

        // Act: Add multiple items (as a list)
        entity.ModifyAttribute("inventory", Operator.Add, new List<object> { 2, "orange" });
         // Assert: Add multiple items
        entity.GetAttribute("inventory").Should().BeEquivalentTo(new List<object> { "apple", 1, "banana", 2, "orange" });

        // Act: Remove single item
        entity.ModifyAttribute("inventory", Operator.Sub, 1);
         // Assert: Remove single item
        entity.GetAttribute("inventory").Should().BeEquivalentTo(new List<object> { "apple", "banana", 2, "orange" });

        // Act: Remove multiple items (as a list)
        entity.ModifyAttribute("inventory", Operator.Sub, new List<object> { "apple", "nonexistent" });
         // Assert: Remove multiple items
        entity.GetAttribute("inventory").Should().BeEquivalentTo(new List<object> { "banana", 2, "orange" });
    }

    /// <summary>
    /// 测试 ModifyAttribute 对字典使用 Add/Sub 操作符。
    /// </summary>
    [Fact]
    public void ModifyAttribute_对字典使用AddSub_应添加或移除键值对()
    {
        // Arrange
        var entity = CreateTestItem("item_modify_dict");
        var initialDict = new Dictionary<string, object?> { { "color", "red" }, { "weight", 10 } };
        entity.SetAttribute("properties", initialDict); // 设置初始字典

        // Act: Add single KeyValuePair
        entity.ModifyAttribute("properties", Operator.Add, new KeyValuePair<string, object?>("material", "iron"));
        // Assert: Add single KeyValuePair
        entity.GetAttribute("properties").Should().BeEquivalentTo(new Dictionary<string, object?> { { "color", "red" }, { "weight", 10 }, { "material", "iron" } });

        // Act: Add multiple from another Dictionary (only adds new keys)
        entity.ModifyAttribute("properties", Operator.Add, new Dictionary<string, object?> { { "weight", 15 }, { "volume", 5 } });
        // Assert: Add multiple from another Dictionary
        entity.GetAttribute("properties").Should().BeEquivalentTo(new Dictionary<string, object?> { { "color", "red" }, { "weight", 10 }, { "material", "iron" }, { "volume", 5 } }); // weight 保持不变

        // Act: Remove single key (as string)
        entity.ModifyAttribute("properties", Operator.Sub, "weight");
        // Assert: Remove single key
        entity.GetAttribute("properties").Should().BeEquivalentTo(new Dictionary<string, object?> { { "color", "red" }, { "material", "iron" }, { "volume", 5 } });

        // Act: Remove multiple keys (as List<string>)
        entity.ModifyAttribute("properties", Operator.Sub, new List<string> { "color", "nonexistent" });
        // Assert: Remove multiple keys
        entity.GetAttribute("properties").Should().BeEquivalentTo(new Dictionary<string, object?> { { "material", "iron" }, { "volume", 5 } });
    }

     /// <summary>
    /// 测试 ModifyAttribute 对 TypedID 使用不支持的操作符。
    /// </summary>
    [Fact]
    public void ModifyAttribute_对TypedID使用不支持的操作符_应保持不变()
    {
        // Arrange
        var entity = CreateTestPlace("place_modify_tid");
        var initialTid = new TypedID(EntityType.Character, "char_owner");
        entity.SetAttribute("owner", initialTid);

        // Act
        entity.ModifyAttribute("owner", Operator.Add, new TypedID(EntityType.Character, "char_visitor"));

        // Assert
        entity.GetAttribute("owner").Should().Be(initialTid); // 值未改变
    }

    /// <summary>
    /// 测试 DeleteAttribute 方法。
    /// </summary>
    [Fact]
    public void DeleteAttribute_应能删除动态属性但不能删除核心属性()
    {
        // Arrange
        var entity = CreateTestItem("item_delete");
        entity.SetAttribute("dynamicAttr", "to_be_deleted");
        entity.SetAttribute("anotherAttr", 123);

        // Act
        var resultDynamic = entity.DeleteAttribute("dynamicAttr");
        var resultCore = entity.DeleteAttribute(nameof(BaseEntity.EntityId));
        var resultNonExistent = entity.DeleteAttribute("nonExistentAttr");

        // Assert
        resultDynamic.Should().BeTrue();
        entity.HasAttribute("dynamicAttr").Should().BeFalse(); // 确认已删除

        resultCore.Should().BeFalse();
        entity.HasAttribute(nameof(BaseEntity.EntityId)).Should().BeTrue(); // 核心属性未删除

        resultNonExistent.Should().BeFalse();

        entity.HasAttribute("anotherAttr").Should().BeTrue(); // 其他属性不受影响
    }

    /// <summary>
    /// 测试 GetAllAttributes 方法。
    /// </summary>
    [Fact]
    public void GetAllAttributes_应返回包含所有核心和动态属性的字典()
    {
        // Arrange
        var entity = CreateTestCharacter("char_getall");
        entity.SetAttribute("level", 5);
        entity.SetAttribute("inventory", new List<object> { "sword" });
        entity.IsDestroyed = true;

        // Act
        var allAttributes = entity.GetAllAttributes();

        // Assert
        allAttributes.Should().HaveCount(5); // EntityId, EntityType, IsDestroyed, level, inventory
        allAttributes.Should().ContainKey(nameof(BaseEntity.EntityId)).WhoseValue.Should().Be(entity.EntityId);
        allAttributes.Should().ContainKey(nameof(BaseEntity.EntityType)).WhoseValue.Should().Be(entity.EntityType);
        allAttributes.Should().ContainKey(nameof(BaseEntity.IsDestroyed)).WhoseValue.Should().Be(true);
        allAttributes.Should().ContainKey("level").WhoseValue.Should().Be(5);
        allAttributes.Should().ContainKey("inventory").WhoseValue.Should().BeEquivalentTo(new List<object> { "sword" });

        // 验证返回的是副本（对于 List/Dictionary）
        var listFromAttrs = allAttributes["inventory"] as List<object>;
        listFromAttrs.Should().NotBeSameAs(entity.GetAttribute("inventory") as IEnumerable<object>); // 应该是不同的列表实例
        listFromAttrs!.Add("shield");
        (entity.GetAttribute("inventory") as List<object>).Should().HaveCount(1); // 原始列表不变
    }

    /// <summary>
    /// 测试 BaseEntity 的 Clone 方法。
    /// </summary>
    [Fact]
    public void Clone_应创建具有相同属性的独立副本()
    {
        // Arrange
        var original = CreateTestItem("item_original");
        original.SetAttribute("quality", "good");
        original.SetAttribute("tags", new List<object> { "metal", "sharp" });
        original.SetAttribute("stats", new Dictionary<string, object?> { { "damage", 10 } });
        original.IsDestroyed = false;

        // Act
        var clone = original.Clone() as Item;

        // Assert
        clone.Should().NotBeNull();
        clone.Should().NotBeSameAs(original); // 确认是不同实例
        clone!.EntityId.Should().Be(original.EntityId);
        clone.EntityType.Should().Be(original.EntityType);
        clone.IsDestroyed.Should().Be(original.IsDestroyed);

        // 比较动态属性
        clone.GetAttribute("quality").Should().Be("good");
        clone.GetAttribute("tags").Should().BeEquivalentTo(original.GetAttribute("tags"));
        clone.GetAttribute("stats").Should().BeEquivalentTo(original.GetAttribute("stats"));

        // --- 验证深拷贝行为 ---
        // 修改克隆体
        clone.IsDestroyed = true;
        clone.SetAttribute("quality", "excellent");
        (clone.GetAttribute("tags") as List<object>)?.Add("heavy");
        var objects = (clone.GetAttribute("stats") as Dictionary<string, object?>);
        if (objects != null) objects["defense"] = 5;


        // 确认原始对象未改变
        original.IsDestroyed.Should().BeFalse();
        original.GetAttribute("quality").Should().Be("good");
        (original.GetAttribute("tags") as List<object>).Should().HaveCount(2).And.NotContain("heavy");
        (original.GetAttribute("stats") as Dictionary<string, object?>).Should().HaveCount(1).And.NotContainKey("defense");

        // 确认克隆体的列表和字典是独立的实例 (虽然内部元素是浅拷贝)
        clone.GetAttribute("tags").Should().NotBeSameAs(original.GetAttribute("tags"));
        clone.GetAttribute("stats").Should().NotBeSameAs(original.GetAttribute("stats"));
    }

    /// <summary>
    /// 测试 Item 的 SetAttribute 对 quantity 的特殊处理。
    /// </summary>
    [Theory]
    [InlineData(5, 5)] // 有效值
    [InlineData(0, 0)] // 有效值 0
    [InlineData(-1, 0)] // 无效负数，应设为 0
    [InlineData("not a number", 0)] // 无效类型，应设为 0
    [InlineData(null, 0)] // null 值，应设为 0
    public void Item_SetAttribute_对于quantity_应进行验证和默认值处理(object? inputValue, int expectedValue)
    {
        // Arrange
        var item = CreateTestItem("item_quantity");

        // Act
        item.SetAttribute("quantity", inputValue);

        // Assert
        item.GetAttribute("quantity").Should().Be(expectedValue);
    }
}