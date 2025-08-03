using System.Collections.Concurrent;
using System.Text.Json;
using Nito.AsyncEx;
using YAESandBox.Seed.State;
using YAESandBox.Seed.State.Entity;
using YAESandBox.Depend;
using YAESandBox.Depend.Storage;

namespace YAESandBox.Seed.Block.BlockManager;

public partial class BlockManager
{
    /// <summary>
    /// 将当前 BlockManager 的状态保存到流中。
    /// </summary>
    /// <param name="saveAction">要写入的回调。</param>
    /// <param name="frontEndBlindData">前端提供的盲存数据。</param>
    public async Task SaveToFileAsync(Func<ArchiveDto, JsonSerializerOptions, Task> saveAction, object? frontEndBlindData)
    {
        var archive = new ArchiveDto
        {
            BlindStorage = frontEndBlindData,
            ArchiveVersion = "1.0" // Or read from config/constant
        };

        // 遍历内存中的 Blocks
        foreach ((string blockId, var blockStatus) in this.Blocks)
        {
            var coreBlock = blockStatus.Block; // The actual Block instance

            var blockDto = new BlockDto
            {
                BlockId = coreBlock.BlockId,
                ParentBlockId = coreBlock.ParentBlockId,
                WorkFlowName = coreBlock.WorkflowName,
                ChildrenIds = [..coreBlock.ChildrenList], // Copy list
                BlockContent = coreBlock.BlockContent,
                // Shallow copy Metadata
                Metadata = coreBlock.Metadata.ToDictionary(entry => entry.Key, entry => entry.Value),
                // Shallow copy Params
                TriggeredChildParams = coreBlock.TriggeredChildParams.ToDictionary(entry => entry.Key, entry => entry.Value),
                TriggeredParams = coreBlock.TriggeredParams.ToDictionary(entry => entry.Key, entry => entry.Value),
                GameState = coreBlock.GameState.GetAllSettings()
                    .ToDictionary(entry => entry.Key, entry => entry.Value)
            };

            blockDto.WorldStates["wsInput"] = this.MapWorldStateDto(coreBlock.WsInput);
            if (coreBlock.WsPostAi != null)
                blockDto.WorldStates["wsPostAI"] = this.MapWorldStateDto(coreBlock.WsPostAi);
            if (coreBlock.WsPostUser != null)
                blockDto.WorldStates["wsPostUser"] = this.MapWorldStateDto(coreBlock.WsPostUser);
            // wsTemp 不保存

            archive.Blocks.Add(blockId, blockDto);
        }

        await saveAction(archive, YaeSandBoxJsonHelper.JsonSerializerOptions);
        Log.Info($"BlockManager state saved. Blocks count: {archive.Blocks.Count}");
    }

    /// <summary>
    /// 从流中加载 BlockManager 的状态。
    /// </summary>
    /// <param name="loadAction">要读取的回调。</param>
    /// <returns>恢复的前端盲存数据。</returns>
    public async Task<object?> LoadFromFileAsync(Func<JsonSerializerOptions, Task<ArchiveDto?>> loadAction)
    {
        var archive = await loadAction(YaeSandBoxJsonHelper.JsonSerializerOptions);

        if (archive == null)
        {
            Log.Error("Failed to deserialize archive or archive is empty.");
            // Handle error as needed (e.g., throw, return default)
            // Since we ignore most errors, just log and potentially start fresh.
            this.Blocks.Clear();
            this.BlockLocks.Clear();
            // Ensure root block exists if starting fresh
            if (!this.Blocks.ContainsKey(WorldRootId))
                this.Blocks.TryAdd(WorldRootId,
                    Block.CreateBlock(WorldRootId, null, DebugWorkFlowName, new WorldState(), new GameState()));

            return null;
        }

        Log.Info(
            $"Loading BlockManager state from archive version {archive.ArchiveVersion}. Blocks count: {archive.Blocks.Count}");

        // --- 清空当前状态并重建 ---
        var newBlocks = new ConcurrentDictionary<string, BlockStatus>();
        var newLocks = new ConcurrentDictionary<string, AsyncLock>();

        foreach ((string blockId, var blockDto) in archive.Blocks)
        {
            // --- 恢复状态 ---
            var gameState = PersistenceMapper.MapGameState(blockDto.GameState);
            // Metadata and TriggeredChildParams might need deep copy?
            var metadata = blockDto.Metadata.ToDictionary(entry => entry.Key, entry => entry.Value);
            var triggeredChildParams = blockDto.TriggeredChildParams.ToDictionary(entry => entry.Key, entry => entry.Value);
            var triggerParams = blockDto.TriggeredParams.ToDictionary(entry => entry.Key, entry => entry.Value);

            // --- 恢复 WorldState 快照 ---
            var wsInput = PersistenceMapper.MapWorldState(blockDto.WorldStates.GetValueOrDefault("wsInput"));
            var wsPostAi = PersistenceMapper.MapWorldState(blockDto.WorldStates.GetValueOrDefault("wsPostAI"));
            var wsPostUser = PersistenceMapper.MapWorldState(blockDto.WorldStates.GetValueOrDefault("wsPostUser"));

            if (wsInput == null && blockId != WorldRootId) // Root might start empty, others must have wsInput
            {
                Log.Error($"Block '{blockId}' loaded without wsInput data. Skipping block.");
                continue; // Skip this block if essential wsInput is missing
            }

            wsInput ??= new WorldState(); // Ensure root has a non-null wsInput


            // --- 创建 Block 实例 (使用新的构造函数) ---
            IdleBlockStatus coreBlock;
            try
            {
                coreBlock = Block.CreateBlockFromSave(
                    blockId,
                    blockDto.ParentBlockId,
                    blockDto.WorkFlowName,
                    blockDto.ChildrenIds,
                    blockDto.BlockContent,
                    metadata,
                    triggeredChildParams,
                    triggerParams,
                    gameState,
                    wsInput, // wsInput is mandatory now (except maybe root)
                    wsPostAi,
                    wsPostUser
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error creating Block instance for ID '{blockId}' during load. Skipping block.");
                continue;
            }


            // --- 创建 BlockStatus (强制为 Idle) ---
            // 确保 Idle 状态所需的状态是存在的 (wsPostUser)
            if (coreBlock.Block.WsPostUser == null)
            {
                if (coreBlock.Block.WsPostAi != null)
                {
                    coreBlock.Block.WsPostUser = coreBlock.Block.WsPostAi.Clone(); // Prefer wsPostAI if available
                    Log.Warning($"Block '{blockId}' loaded as Idle but wsPostUser was null. Recovered from wsPostAI.");
                }
                else
                {
                    coreBlock.Block.WsPostUser = coreBlock.Block.WsInput.Clone(); // Fallback to wsInput
                    Log.Warning($"Block '{blockId}' loaded as Idle but wsPostUser was null. Recovered from wsInput.");
                }
            }

            if (!newBlocks.TryAdd(blockId, coreBlock))
            {
                Log.Error($"Failed to add loaded block '{blockId}' to dictionary (duplicate?). Skipping.");
                continue;
            }

            newLocks.GetOrAdd(blockId, _ => new AsyncLock()); // Ensure lock exists
        }

        // --- 原子地替换 BlockManager 的状态 ---
        // (简单替换，如果需要更复杂的事务性替换，逻辑会更复杂)
        this.Blocks.Clear();
        this.BlockLocks.Clear();
        foreach (var kvp in newBlocks) this.Blocks.TryAdd(kvp.Key, kvp.Value);
        foreach (var kvp in newLocks) this.BlockLocks.TryAdd(kvp.Key, kvp.Value);


        Log.Info($"BlockManager state loaded successfully. Final block count: {this.Blocks.Count}");
        return archive.BlindStorage; // 返回盲存数据
    }

    private WorldStateDto MapWorldStateDto(WorldState ws)
    {
        var dto = new WorldStateDto();
        this.MapEntitiesDto(dto.Items, ws.Items);
        this.MapEntitiesDto(dto.Characters, ws.Characters);
        this.MapEntitiesDto(dto.Places, ws.Places);
        return dto;
    }

    private void MapEntitiesDto<TEntity>(Dictionary<string, EntityDto> targetDict,
        Dictionary<string, TEntity> sourceDict)
        where TEntity : BaseEntity
    {
        foreach (var kvp in sourceDict)
            // 在序列化时，不需要担心 object? 问题，System.Text.Json 会处理
            targetDict.Add(kvp.Key, new EntityDto { Attributes = kvp.Value.GetAllAttributes() });
    }
}