using Xunit;
using FluentAssertions;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Core.Action;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json; // 用于辅助验证或创建盲存数据

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


        // 异步初始化，在所有测试之前运行一次
        public async Task InitializeAsync()
        {
            _originalManager = new BlockManager(); // 创建新的管理器实例

            // --- 准备测试数据 ---
            _playerCharacter = new Character("player_main") { IsDestroyed = false };
            _playerCharacter.SetAttribute("姓名", "爱丽丝");
            _playerCharacter.SetAttribute("等级", 10);
            _playerCharacter.SetAttribute("活跃", true);
            _playerCharacter.SetAttribute("背包容量", 20.5); // double 类型
            _playerCharacter.SetAttribute("空值属性", null);

            _testItem = new Item("item_sword") { IsDestroyed = false };
            _testItem.SetAttribute("名称", "精灵之刃");
            _testItem.SetAttribute("攻击力", 15);
            _testItem.SetAttribute("标签", new List<object> { "武器", "锋利", 123 }); // 列表类型

            _testPlace = new Place("place_forest") { IsDestroyed = true }; // 已销毁的实体
            _testPlace.SetAttribute("名称", "迷雾森林");
            _testPlace.SetAttribute("危险等级", 5);
            _testPlace.SetAttribute("特性", new Dictionary<string, object> // 字典类型
            {
                {"天气", "多雾"},
                {"怪物", new List<object> { "哥布林", "蜘蛛" } } // 嵌套列表
            });


            _playerRef = _playerCharacter.TypedId;
            _itemRef = _testItem.TypedId;
            _placeRef = _testPlace.TypedId;

            // 添加 TypedID 作为属性值
            _playerCharacter.SetAttribute("持有物", _itemRef);
            _testItem.SetAttribute("所在地", _placeRef); // 即使地点销毁，引用也应保留
            _playerCharacter.SetAttribute("盟友", new List<object> { new TypedID(EntityType.Character, "npc_bob") }); // 包含TypedID的列表

            _testGameState = new Dictionary<string, object?>
            {
                { "当前任务", "寻找古老卷轴" },
                { "时间", 100 },
                { "难度", "普通" },
                { "启用教程", false },
                { "玩家位置参考", _playerRef } // 包含 TypedID
            };

            _testMetadata = new Dictionary<string, object?>
            {
                { "创建者", "测试脚本" },
                { "版本", 1.1 },
                { "目标", null }
            };

            _testTriggerParams = new Dictionary<string, object?>
            {
                { "触发事件", "玩家进入" },
                { "相关物品", _itemRef }
            };

            _testBlindStorage = new { FrontendSetting = "DarkMode", UserID = 12345 }; // 匿名的前端数据

            // --- 构建 Block 结构和状态 ---

            // 1. 修改根节点状态
            await _originalManager.UpdateBlockGameStateAsync(_rootBlockId, _testGameState);
            var rootOps = new List<AtomicOperation>
            {
                AtomicOperation.Create(_playerRef.Type, _playerRef.Id, _playerCharacter.GetAllAttributes()),
                AtomicOperation.Create(_itemRef.Type, _itemRef.Id, _testItem.GetAllAttributes())
            };
            await _originalManager.EnqueueOrExecuteAtomicOperationsAsync(_rootBlockId, rootOps);

            // 2. 创建子节点 (会是 Loading 状态)
            var loadingChildStatus = await _originalManager.CreateChildBlock_Async(_rootBlockId, _testTriggerParams);
            loadingChildStatus.Should().NotBeNull();
            _childBlockId = loadingChildStatus!.Block.BlockId; // 获取实际生成的ID

            // 模拟工作流完成，使其变为 Idle
            var childAiOps = new List<AtomicOperation>
            {
                 AtomicOperation.Create(_placeRef.Type, _placeRef.Id, _testPlace.GetAllAttributes()), // 创建地点
                 AtomicOperation.Modify(_playerRef.Type, _playerRef.Id, "等级", Operator.Add, 1) // 玩家升级
            };
            await _originalManager.HandleWorkflowCompletionAsync(_childBlockId, true, "AI 创建了地点并升级了玩家。", childAiOps, _testMetadata);

            // 3. 在子节点上再添加用户操作
            var childUserOps = new List<AtomicOperation>
            {
                AtomicOperation.Modify(_itemRef.Type, _itemRef.Id, "攻击力", Operator.Add, 5) // 增强物品
            };
            await _originalManager.EnqueueOrExecuteAtomicOperationsAsync(_childBlockId, childUserOps);


            // 4. 创建孙子节点
            var loadingGrandChildStatus = await _originalManager.CreateChildBlock_Async(_childBlockId, new Dictionary<string, object?> { { "原因", "进一步探索" } });
            loadingGrandChildStatus.Should().NotBeNull();
            _grandChildBlockId = loadingGrandChildStatus!.Block.BlockId;

            // 模拟孙子节点工作流完成
            var grandChildAiOps = new List<AtomicOperation>
            {
                AtomicOperation.Delete(_itemRef.Type, _itemRef.Id) // 删除了物品
            };
            await _originalManager.HandleWorkflowCompletionAsync(_grandChildBlockId, true, "AI 删除了物品。", grandChildAiOps, new Dictionary<string, object?>());
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
            await _originalManager.SaveToFileAsync(memoryStream, _testBlindStorage);
            memoryStream.Position = 0; // 重置流位置以便读取

            // Act: 加载
            var loadedManager = new BlockManager(); // 创建一个新的空管理器来加载
            var loadedBlindStorage = await loadedManager.LoadFromFileAsync(memoryStream);

            // Assert: 验证盲存数据
            loadedBlindStorage.Should().NotBeNull();
            // 使用 BeEquivalentTo 比较匿名类型或反序列化回已知类型
             var expectedBlindStorage = (JsonElement)_testBlindStorage!; // 强制转换以匹配反序列化类型
             loadedBlindStorage.Should().BeEquivalentTo(expectedBlindStorage);
            // 或者 JsonDocument.Parse(JsonSerializer.Serialize(_testBlindStorage)).RootElement

            // Assert: 验证 Block 数量
            var originalBlocks = _originalManager.GetBlocks();
            var loadedBlocks = loadedManager.GetBlocks();
            loadedBlocks.Should().HaveCount(originalBlocks.Count); // 应该有相同数量的 Block

            // Assert: 验证根节点
            loadedBlocks.Should().ContainKey(_rootBlockId);
            var loadedRoot = loadedBlocks[_rootBlockId];
            var originalRoot = originalBlocks[_rootBlockId];
            loadedRoot.ParentBlockId.Should().BeNull();
            loadedRoot.ChildrenList.Should().Contain(_childBlockId);
            loadedRoot.BlockContent.Should().Be(originalRoot.BlockContent); // 检查内容

            // Assert: 验证子节点
            loadedBlocks.Should().ContainKey(_childBlockId);
            var loadedChild = loadedBlocks[_childBlockId];
            var originalChild = originalBlocks[_childBlockId];
            loadedChild.ParentBlockId.Should().Be(_rootBlockId);
            loadedChild.ChildrenList.Should().Contain(_grandChildBlockId);
            loadedChild.BlockContent.Should().Be(originalChild.BlockContent);
             loadedChild.Metadata.Should().BeEquivalentTo(originalChild.Metadata, options => options.ComparingByMembers<object>());// 检查元数据

            // Assert: 验证孙子节点
            loadedBlocks.Should().ContainKey(_grandChildBlockId);
            var loadedGrandChild = loadedBlocks[_grandChildBlockId];
            var originalGrandChild = originalBlocks[_grandChildBlockId];
            loadedGrandChild.ParentBlockId.Should().Be(_childBlockId);
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
            await _originalManager.SaveToFileAsync(memoryStream, null);
            memoryStream.Position = 0;
            var loadedManager = new BlockManager();
            await loadedManager.LoadFromFileAsync(memoryStream);

            // Assert: 检查特定 Block 的 WorldState (例如：子节点加载后的 wsPostUser)
            var loadedChildBlockStatus = await loadedManager.GetBlockAsync(_childBlockId);
            loadedChildBlockStatus.Should().NotBeNull().And.BeOfType<IdleBlockStatus>(); // 加载后应为 Idle
            var loadedChildWs = loadedChildBlockStatus!.Block.wsPostUser; // Idle 状态下，wsPostUser 是主要状态
            loadedChildWs.Should().NotBeNull();

            // --- 验证玩家实体 ---
            var loadedPlayer = loadedChildWs!.FindEntity(_playerRef) as Character;
            loadedPlayer.Should().NotBeNull();
            loadedPlayer!.IsDestroyed.Should().BeFalse();
            loadedPlayer.GetAttribute("姓名").Should().Be("爱丽丝");
            loadedPlayer.GetAttribute("等级").Should().Be(11); // 在子节点中升级了
            loadedPlayer.GetAttribute("活跃").Should().Be(true);
            loadedPlayer.GetAttribute("背包容量").Should().Be(20.5);
            loadedPlayer.GetAttribute("空值属性").Should().BeNull();
            // 验证 TypedID 属性
            loadedPlayer.GetAttribute("持有物").Should().BeOfType<TypedID>().And.Be(_itemRef);
            // 验证包含 TypedID 的列表
            loadedPlayer.TryGetAttribute<List<object>>("盟友", out var allies).Should().BeTrue();
            allies.Should().ContainSingle().Which.Should().BeOfType<TypedID>().And.Be(new TypedID(EntityType.Character, "npc_bob"));


            // --- 验证物品实体 ---
            var loadedItem = loadedChildWs.FindEntity(_itemRef) as Item;
            loadedItem.Should().NotBeNull();
            loadedItem!.IsDestroyed.Should().BeFalse();
            loadedItem.GetAttribute("名称").Should().Be("精灵之刃");
            loadedItem.GetAttribute("攻击力").Should().Be(20); // 在子节点用户操作中增强了
            // 验证列表属性
            loadedItem.TryGetAttribute<List<object>>("标签", out var tags).Should().BeTrue();
            tags.Should().BeEquivalentTo(new List<object> { "武器", "锋利", 123L }); // JSON 数字默认可能变为 long (System.Text.Json 行为) 或 int，取决于大小，使用 BeEquivalentTo 更灵活
            // 验证 TypedID 属性
            loadedItem.GetAttribute("所在地").Should().BeOfType<TypedID>().And.Be(_placeRef);

            // --- 验证地点实体 ---
            var loadedPlace = loadedChildWs.FindEntity(_placeRef, includeDestroyed: true) as Place; // 查找时包含已销毁
            loadedPlace.Should().NotBeNull();
            loadedPlace!.IsDestroyed.Should().BeTrue(); // 确认是已销毁状态
            loadedPlace.GetAttribute("名称").Should().Be("迷雾森林");
            loadedPlace.GetAttribute("危险等级").Should().Be(5L); // 可能变为 long
            // 验证字典属性
            loadedPlace.TryGetAttribute<Dictionary<string, object>>("特性", out var features).Should().BeTrue();
            features.Should().ContainKey("天气").WhoseValue.Should().Be("多雾");
            features.Should().ContainKey("怪物");
            features["怪物"].Should().BeOfType<List<object>>().Which.Should().BeEquivalentTo(new List<object> { "哥布林", "蜘蛛" });

            // --- 验证孙子节点中物品被删除 ---
             var loadedGrandChildBlockStatus = await loadedManager.GetBlockAsync(_grandChildBlockId);
             loadedGrandChildBlockStatus.Should().NotBeNull().And.BeOfType<IdleBlockStatus>();
             var loadedGrandChildWs = loadedGrandChildBlockStatus!.Block.wsPostUser;
             loadedGrandChildWs.Should().NotBeNull();
             loadedGrandChildWs.FindEntity(_itemRef).Should().BeNull(); // 物品应该找不到了 (因为 IsDestroyed=true)
             loadedGrandChildWs.FindEntity(_itemRef, includeDestroyed:true).Should().NotBeNull(); // 包含已销毁的能找到
             loadedGrandChildWs.FindEntity(_itemRef, includeDestroyed: true)!.IsDestroyed.Should().BeTrue(); // 确认是被标记为删除
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
            await _originalManager.SaveToFileAsync(memoryStream, null);
            memoryStream.Position = 0;
            var loadedManager = new BlockManager();
            await loadedManager.LoadFromFileAsync(memoryStream);

            // Assert: 验证根节点的 GameState
            var loadedRootStatus = await loadedManager.GetBlockAsync(_rootBlockId);
            loadedRootStatus.Should().NotBeNull();
            var loadedGameState = loadedRootStatus!.Block.GameState;
            var originalGameState = _originalManager.GetBlocks()[_rootBlockId].GameState;

            // 使用 BeEquivalentTo 比较 GameState 内容
            loadedGameState.GetAllSettings().Should().BeEquivalentTo(originalGameState.GetAllSettings(), options => options
                .ComparingByMembers<object>() // 比较对象内容
                .ComparingEnumsByName()      // 按名称比较枚举（如果GameState中有枚举）
                .Using<JsonElement>(ctx => ctx.Subject.Should().BeEquivalentTo(ctx.Expectation)).WhenTypeIs<JsonElement>() // 处理可能的 JsonElement
                 .Using<long>(ctx => ctx.Subject.Should().Be((long)ctx.Expectation)).When(info => info.Type == typeof(int)) // 处理 int->long 转换
            );
             // 手动检查包含 TypedID 的项
             loadedGameState["玩家位置参考"].Should().BeOfType<TypedID>().And.Be(_playerRef);


            // Assert: 验证子节点的 Metadata 和 TriggeredChildParams
            var loadedChildStatus = await loadedManager.GetBlockAsync(_childBlockId);
            loadedChildStatus.Should().NotBeNull();
            var loadedChildBlock = loadedChildStatus!.Block;
            var originalChildBlock = _originalManager.GetBlocks()[_childBlockId];

            loadedChildBlock.Metadata.Should().BeEquivalentTo(originalChildBlock.Metadata, options => options.ComparingByMembers<object>());
            loadedChildBlock.TriggeredChildParams.Should().BeEquivalentTo(originalChildBlock.TriggeredChildParams, options => options.ComparingByMembers<object>());
            // 手动检查 TriggeredChildParams 中的 TypedID
            loadedChildBlock.TriggeredChildParams["相关物品"].Should().BeOfType<TypedID>().And.Be(_itemRef);
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
            loadedBlocks.Should().ContainKey(_rootBlockId);
            var rootBlock = loadedBlocks[_rootBlockId];
            rootBlock.ChildrenList.Should().BeEmpty();
            rootBlock.wsInput.Should().NotBeNull(); // 根节点应该有 wsInput
            rootBlock.wsPostUser.Should().NotBeNull(); // 加载后强制为 Idle，应有 wsPostUser
        }
    }
}