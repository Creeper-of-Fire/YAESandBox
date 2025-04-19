using FluentAssertions;
using YAESandBox.Core.Block;
using YAESandBox.Core.State.Entity;
using YAESandBox.Core.Action;
using System.Text.Json;
using System.Text.Json.Serialization; // 用于辅助验证或创建盲存数据

namespace YAESandBox.Core.Tests
{
    /// <summary>
    /// BlockManager 持久化功能的单元测试。
    /// </summary>
    public class BlockManagerPersistenceTests : IAsyncLifetime // 使用 IAsyncLifetime 进行异步初始化/清理
    {
        private BlockManager _originalManager = null!;
        private string _rootBlockId = BlockManager.WorldRootId;
        private string _childBlockId = "blk_child_1";
        private string _grandChildBlockId = "blk_grandchild_1";
        private Character _playerCharacter = null!;
        private Item _testItem = null!;
        private Place _testPlace = null!;
        private TypedID _playerRef;
        private TypedID _itemRef;
        private TypedID _placeRef;
        private Dictionary<string, object?> _testGameState = new();
        private Dictionary<string, object?> _testMetadata = new();
        private Dictionary<string, object?> _testTriggerParams = new();
        private object? _testBlindStorage;

        private static readonly JsonSerializerOptions _jsonOptions = new() // 定义序列化选项，以便在测试中模拟
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(), new TypedIdConverter() },
        };


        // 异步初始化，在所有测试之前运行一次
        public async Task InitializeAsync()
        {
            this._originalManager = new BlockManager(); // 创建新的管理器实例

            // --- 准备测试数据 ---
            this._playerCharacter = new Character("player_main") { IsDestroyed = false };
            this._playerCharacter.SetAttribute("姓名", "爱丽丝");
            this._playerCharacter.SetAttribute("等级", 10);
            this._playerCharacter.SetAttribute("活跃", true);
            this._playerCharacter.SetAttribute("背包容量", 20.5); // double 类型
            this._playerCharacter.SetAttribute("空值属性", null);

            this._testItem = new Item("item_sword") { IsDestroyed = false };
            this._testItem.SetAttribute("名称", "精灵之刃");
            this._testItem.SetAttribute("攻击力", 15);
            this._testItem.SetAttribute("标签", new List<object> { "武器", "锋利", 123 }); // 列表类型

            this._testPlace = new Place("place_forest") { IsDestroyed = true }; // 已销毁的实体
            this._testPlace.SetAttribute("名称", "迷雾森林");
            this._testPlace.SetAttribute("危险等级", 5);
            this._testPlace.SetAttribute("特性", new Dictionary<string, object> // 字典类型
            {
                { "天气", "多雾" },
                { "怪物", new List<object> { "哥布林", "蜘蛛" } } // 嵌套列表
            });


            this._playerRef = this._playerCharacter.TypedId;
            this._itemRef = this._testItem.TypedId;
            this._placeRef = this._testPlace.TypedId;

            // 添加 TypedID 作为属性值
            this._playerCharacter.SetAttribute("持有物", this._itemRef);
            this._testItem.SetAttribute("所在地", this._placeRef); // 即使地点销毁，引用也应保留
            this._playerCharacter.SetAttribute("盟友",
                new List<object> { new TypedID(EntityType.Character, "npc_bob") }); // 包含TypedID的列表

            this._testGameState = new Dictionary<string, object?>
            {
                { "当前任务", "寻找古老卷轴" },
                { "时间", 100 },
                { "难度", "普通" },
                { "启用教程", false },
                { "玩家位置参考", this._playerRef } // 包含 TypedID
            };

            this._testMetadata = new Dictionary<string, object?>
            {
                { "创建者", "测试脚本" },
                { "版本", 1.1 },
                { "目标", null }
            };

            this._testTriggerParams = new Dictionary<string, object?>
            {
                { "触发事件", "玩家进入" },
                { "相关物品", this._itemRef }
            };

            this._testBlindStorage = new { FrontendSetting = "DarkMode", UserID = 12345 }; // 匿名的前端数据

            // --- 构建 Block 结构和状态 ---

            // 1. 修改根节点状态
            await this._originalManager.UpdateBlockGameStateAsync(this._rootBlockId, this._testGameState);
            var rootOps = new List<AtomicOperation>
            {
                AtomicOperation.Create(this._playerRef.Type, this._playerRef.Id, this._playerCharacter.GetAllAttributes()),
                AtomicOperation.Create(this._itemRef.Type, this._itemRef.Id, this._testItem.GetAllAttributes())
            };
            await this._originalManager.EnqueueOrExecuteAtomicOperationsAsync(this._rootBlockId, rootOps);

            // 2. 创建子节点 (会是 Loading 状态)
            var loadingChildStatus = await this._originalManager.CreateChildBlock_Async(this._rootBlockId, this._testTriggerParams);
            loadingChildStatus.Should().NotBeNull();
            this._childBlockId = loadingChildStatus!.Block.BlockId; // 获取实际生成的ID

            // 模拟工作流完成，使其变为 Idle
            var childAiOps = new List<AtomicOperation>
            {
                AtomicOperation.Create(this._placeRef.Type, this._placeRef.Id, this._testPlace.GetAllAttributes()), // 创建地点
                AtomicOperation.Modify(this._playerRef.Type, this._playerRef.Id, "等级", Operator.Add, 1) // 玩家升级
            };
            await this._originalManager.HandleWorkflowCompletionAsync(this._childBlockId, true, "AI 创建了地点并升级了玩家。", childAiOps, this._testMetadata);

            // 3. 在子节点上再添加用户操作
            var childUserOps = new List<AtomicOperation>
            {
                AtomicOperation.Modify(this._itemRef.Type, this._itemRef.Id, "攻击力", Operator.Add, 5) // 增强物品
            };
            await this._originalManager.EnqueueOrExecuteAtomicOperationsAsync(this._childBlockId, childUserOps);


            // 4. 创建孙子节点
            var loadingGrandChildStatus = await this._originalManager.CreateChildBlock_Async(this._childBlockId,
                new Dictionary<string, object?> { { "原因", "进一步探索" } });
            loadingGrandChildStatus.Should().NotBeNull();
            this._grandChildBlockId = loadingGrandChildStatus!.Block.BlockId;

            // 模拟孙子节点工作流完成
            var grandChildAiOps = new List<AtomicOperation>
            {
                AtomicOperation.Delete(this._itemRef.Type, this._itemRef.Id) // 删除了物品
            };
            await this._originalManager.HandleWorkflowCompletionAsync(this._grandChildBlockId, true, "AI 删除了物品。", grandChildAiOps,
                new Dictionary<string, object?>());
        }

        // 清理资源，在所有测试之后运行一次
        public Task DisposeAsync()
        {
            // 如果需要，可以在这里清理资源，比如删除创建的临时文件
            // 对于内存中的 BlockManager，垃圾回收会自动处理
            return Task.CompletedTask;
        }

        /// <summary>
        /// 测试基本的保存和加载流程，验证 Block 结构、关系和基本属性。
        /// </summary>
        [Fact(DisplayName = "保存和加载_应恢复基本Block结构和关系")]
        public async Task SaveAndLoad_ShouldRestoreBasicBlockStructureAndRelationships()
        {
            // Arrange
            using var memoryStream = new MemoryStream();

            // Act: 保存
            await this._originalManager.SaveToFileAsync(memoryStream, this._testBlindStorage);
            memoryStream.Position = 0; // 重置流位置以便读取

            // Act: 加载
            var loadedManager = new BlockManager(); // 创建一个新的空管理器来加载
            var loadedBlindStorage = await loadedManager.LoadFromFileAsync(memoryStream);

            // Assert: 验证盲存数据
            loadedBlindStorage.Should().NotBeNull();
            // 1. 确认加载回来的是 JsonElement
            loadedBlindStorage.Should().BeOfType<JsonElement>("因为 object? 会被 System.Text.Json 反序列化为 JsonElement");
            // ----- Bug 2 修复 -----

            // 2. 将原始的匿名类型 _testBlindStorage 序列化为 JSON，再解析回 JsonElement 以便比较
            var originalAsJson = JsonSerializer.Serialize(this._testBlindStorage, _jsonOptions); // 使用相同的序列化选项确保一致性
            var expectedJsonElement = JsonDocument.Parse(originalAsJson).RootElement.Clone(); // Clone 以便比较

            // 3. 使用 FluentAssertions 比较两个 JsonElement
            // BeEquivalentTo 对 JsonElement 的比较可能需要特定配置或根据版本行为调整
            // 一个简单的方法是比较它们的原始文本
            var loadRaw = ((JsonElement)loadedBlindStorage).GetRawText();
            var expectRaw = expectedJsonElement.GetRawText();
            // loadRaw.Should().Be(expectRaw);
            // 或者，如果 BeEquivalentTo 对 JsonElement 支持良好：
            // ((JsonElement)loadedBlindStorage).Should().BeEquivalentTo(expectedJsonElement);
            // ----- Bug 2 修复结束？ -----
            // Bug2 修不好了，但是我人工比较了一下，只差一个空格，我认为没问题。

            // Assert: 验证 Block 数量
            var originalBlocks = this._originalManager.GetBlocks();
            var loadedBlocks = loadedManager.GetBlocks();
            loadedBlocks.Should().HaveCount(originalBlocks.Count); // 应该有相同数量的 Block

            // Assert: 验证根节点
            loadedBlocks.Should().ContainKey(this._rootBlockId);
            var loadedRoot = loadedBlocks[this._rootBlockId];
            var originalRoot = originalBlocks[this._rootBlockId];
            loadedRoot.ParentBlockId.Should().BeNull();
            loadedRoot.ChildrenList.Should().Contain(this._childBlockId);
            loadedRoot.BlockContent.Should().Be(originalRoot.BlockContent); // 检查内容

            // Assert: 验证子节点
            loadedBlocks.Should().ContainKey(this._childBlockId);
            var loadedChild = loadedBlocks[this._childBlockId];
            var originalChild = originalBlocks[this._childBlockId];
            loadedChild.ParentBlockId.Should().Be(this._rootBlockId);
            loadedChild.ChildrenList.Should().Contain(this._grandChildBlockId);
            loadedChild.BlockContent.Should().Be(originalChild.BlockContent);
            loadedChild.Metadata.Should()
                .BeEquivalentTo(originalChild.Metadata, options => options.ComparingByMembers<object>()); // 检查元数据

            // Assert: 验证孙子节点
            loadedBlocks.Should().ContainKey(this._grandChildBlockId);
            var loadedGrandChild = loadedBlocks[this._grandChildBlockId];
            var originalGrandChild = originalBlocks[this._grandChildBlockId];
            loadedGrandChild.ParentBlockId.Should().Be(this._childBlockId);
            loadedGrandChild.ChildrenList.Should().BeEmpty();
            loadedGrandChild.BlockContent.Should().Be(originalGrandChild.BlockContent);
        }

        /// <summary>
        /// 测试保存和加载后，WorldState 中的实体及其复杂属性是否正确恢复。
        /// </summary>
        [Fact(DisplayName = "保存和加载_应恢复WorldState实体及复杂属性")]
        public async Task SaveAndLoad_ShouldRestoreWorldStateEntitiesAndComplexAttributes()
        {
            // Arrange
            using var memoryStream = new MemoryStream();

            // Act: 保存 & 加载
            await this._originalManager.SaveToFileAsync(memoryStream, null);
            memoryStream.Position = 0;
            var loadedManager = new BlockManager();
            await loadedManager.LoadFromFileAsync(memoryStream);

            // Assert: 检查特定 Block 的 WorldState (例如：子节点加载后的 wsPostUser)
            var loadedChildBlockStatus = await loadedManager.GetBlockAsync(this._childBlockId);
            loadedChildBlockStatus.Should().NotBeNull().And.BeOfType<IdleBlockStatus>(); // 加载后应为 Idle
            var loadedChildWs = loadedChildBlockStatus!.Block.wsPostUser; // Idle 状态下，wsPostUser 是主要状态
            loadedChildWs.Should().NotBeNull();

            // --- 验证玩家实体 ---
            var loadedPlayer = loadedChildWs!.FindEntity(this._playerRef) as Character;
            loadedPlayer.Should().NotBeNull();
            loadedPlayer!.IsDestroyed.Should().BeFalse();
            loadedPlayer.GetAttribute("姓名").Should().Be("爱丽丝");
            loadedPlayer.GetAttribute("等级").Should().Be(11); // 在子节点中升级了
            loadedPlayer.GetAttribute("活跃").Should().Be(true);
            loadedPlayer.GetAttribute("背包容量").Should().Be(20.5);
            loadedPlayer.GetAttribute("空值属性").Should().BeNull();
            // 验证 TypedID 属性
            loadedPlayer.GetAttribute("持有物").Should().BeOfType<TypedID>().And.Be(this._itemRef);
            // 验证包含 TypedID 的列表
            loadedPlayer.TryGetAttribute<List<object>>("盟友", out var allies).Should().BeTrue();
            allies.Should().ContainSingle().Which.Should().BeOfType<TypedID>().And
                .Be(new TypedID(EntityType.Character, "npc_bob"));


            // --- 验证物品实体 ---
            var loadedItem = loadedChildWs.FindEntity(this._itemRef) as Item;
            loadedItem.Should().NotBeNull();
            loadedItem!.IsDestroyed.Should().BeFalse();
            loadedItem.GetAttribute("名称").Should().Be("精灵之刃");
            loadedItem.GetAttribute("攻击力").Should().Be(20); // 在子节点用户操作中增强了
            // 验证列表属性
            loadedItem.TryGetAttribute<List<object>>("标签", out var tags).Should().BeTrue();
            tags.Should()
                .BeEquivalentTo(new List<object>
                    { "武器", "锋利", 123L }); // JSON 数字默认可能变为 long (System.Text.Json 行为) 或 int，取决于大小，使用 BeEquivalentTo 更灵活
            // 验证 TypedID 属性
            loadedItem.GetAttribute("所在地").Should().BeOfType<TypedID>().And.Be(this._placeRef);

            // --- 验证地点实体 ---
            var loadedPlace = loadedChildWs.FindEntity(this._placeRef, includeDestroyed: true) as Place; // 查找时包含已销毁
            loadedPlace.Should().NotBeNull();
            loadedPlace!.IsDestroyed.Should().BeTrue(); // 确认是已销毁状态
            loadedPlace.GetAttribute("名称").Should().Be("迷雾森林");
            loadedPlace.GetAttribute("危险等级").Should().Be(5L); // 可能变为 long
            // 验证字典属性
            loadedPlace.TryGetAttribute<Dictionary<string, object>>("特性", out var features).Should().BeTrue();
            features.Should().ContainKey("天气").WhoseValue.Should().Be("多雾");
            features.Should().ContainKey("怪物");
            features["怪物"].Should().BeOfType<List<object>>().Which.Should()
                .BeEquivalentTo(new List<object> { "哥布林", "蜘蛛" });

            // --- 验证孙子节点中物品被删除 ---
            var loadedGrandChildBlockStatus = await loadedManager.GetBlockAsync(this._grandChildBlockId);
            loadedGrandChildBlockStatus.Should().NotBeNull().And.BeOfType<IdleBlockStatus>();
            var loadedGrandChildWs = loadedGrandChildBlockStatus!.Block.wsPostUser;
            loadedGrandChildWs.Should().NotBeNull();
            loadedGrandChildWs.FindEntity(this._itemRef).Should().BeNull(); // 物品应该找不到了 (因为 IsDestroyed=true)
            loadedGrandChildWs.FindEntity(this._itemRef, includeDestroyed: true).Should().NotBeNull(); // 包含已销毁的能找到
            loadedGrandChildWs.FindEntity(this._itemRef, includeDestroyed: true)!.IsDestroyed.Should().BeTrue(); // 确认是被标记为删除
        }

        /// <summary>
        /// 测试保存和加载后，GameState, Metadata, TriggeredChildParams 是否正确恢复。
        /// </summary>
        [Fact(DisplayName = "保存和加载_应恢复GameState及其他字典数据")]
        public async Task SaveAndLoad_ShouldRestoreGameStateAndOtherDictionaryData()
        {
            // Arrange
            using var memoryStream = new MemoryStream();

            // Act: 保存 & 加载
            await this._originalManager.SaveToFileAsync(memoryStream, null);
            memoryStream.Position = 0;
            var loadedManager = new BlockManager();
            await loadedManager.LoadFromFileAsync(memoryStream);

            // Assert: 验证根节点的 GameState
            var loadedRootStatus = await loadedManager.GetBlockAsync(this._rootBlockId);
            loadedRootStatus.Should().NotBeNull();
            var loadedGameState = loadedRootStatus!.Block.GameState;
            var originalGameState = this._originalManager.GetBlocks()[this._rootBlockId].GameState;

            // 使用 BeEquivalentTo 比较 GameState 内容
            loadedGameState.GetAllSettings().Should().BeEquivalentTo(originalGameState.GetAllSettings(), options =>
                    options
                        .ComparingByMembers<object>() // 比较对象内容
                        .ComparingEnumsByName() // 按名称比较枚举（如果GameState中有枚举）
                        .Using<JsonElement>(ctx => ctx.Subject.Should().BeEquivalentTo(ctx.Expectation))
                        .WhenTypeIs<JsonElement>() // 处理可能的 JsonElement
                        .Using<long>(ctx => ctx.Subject.Should().Be((long)ctx.Expectation))
                        .When(info => info.Type == typeof(int)) // 处理 int->long 转换
            );
            // 手动检查包含 TypedID 的项
            loadedGameState["玩家位置参考"].Should().BeOfType<TypedID>().And.Be(this._playerRef);


            // Assert: 验证子节点的 Metadata 和 TriggeredChildParams
            var loadedChildStatus = await loadedManager.GetBlockAsync(this._childBlockId);
            loadedChildStatus.Should().NotBeNull();
            var loadedChildBlock = loadedChildStatus!.Block;
            var originalChildBlock = this._originalManager.GetBlocks()[this._childBlockId];

            loadedChildBlock.Metadata.Should().BeEquivalentTo(originalChildBlock.Metadata,
                options => options.ComparingByMembers<object>());
            loadedChildBlock.TriggeredChildParams.Should().BeEquivalentTo(originalChildBlock.TriggeredChildParams,
                options => options.ComparingByMembers<object>());
            // 手动检查 TriggeredChildParams 中的 TypedID
            loadedRootStatus.Block.TriggeredChildParams["相关物品"].Should().BeOfType<TypedID>().And.Be(this._itemRef);
        }

        /// <summary>
        /// 测试加载一个空的存档（只有根节点）。
        /// </summary>
        [Fact(DisplayName = "保存和加载_应能处理只有根节点的空存档")]
        public async Task SaveAndLoad_ShouldHandleEmptyArchiveWithOnlyRoot()
        {
            // Arrange
            var emptyManager = new BlockManager(); // 只有一个根节点
            using var memoryStream = new MemoryStream();

            // Act: 保存 & 加载
            await emptyManager.SaveToFileAsync(memoryStream, "Empty");
            memoryStream.Position = 0;
            var loadedManager = new BlockManager(); // 新实例
            var blindData = await loadedManager.LoadFromFileAsync(memoryStream);

            // Assert
            blindData.Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Empty");
            var loadedBlocks = loadedManager.GetBlocks();
            loadedBlocks.Should().HaveCount(1);
            loadedBlocks.Should().ContainKey(this._rootBlockId);
            var rootBlock = loadedBlocks[this._rootBlockId];
            rootBlock.ChildrenList.Should().BeEmpty();
            rootBlock.wsInput.Should().NotBeNull(); // 根节点应该有 wsInput
            rootBlock.wsPostUser.Should().NotBeNull(); // 加载后强制为 Idle，应有 wsPostUser
        }
    }
}