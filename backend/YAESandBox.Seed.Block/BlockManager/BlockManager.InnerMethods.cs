using YAESandBox.Depend;

namespace YAESandBox.Seed.Block.BlockManager;

public partial class BlockManager
{
    /// <summary>
    /// (内部实现) 手动创建新的 Idle Block。
    /// </summary>
    public async Task<(ManagementResult result, BlockStatus? newBlockStatus)> InternalCreateBlockManuallyAsync(
        string parentBlockId, IReadOnlyDictionary<string, string>? initialMetadata)
    {
        // 1. 验证父 Block
        if (!this.Blocks.TryGetValue(parentBlockId, out var parentBlockStatus))
        {
            Log.Warning($"手动创建 Block 失败: 父 Block '{parentBlockId}' 未找到。");
            return (ManagementResult.NotFound, null);
        }

        if (parentBlockStatus is not IdleBlockStatus)
        {
            Log.Warning($"手动创建 Block 失败: 父 Block '{parentBlockId}' 状态为 {parentBlockStatus.StatusCode}，非 Idle。");
            return (ManagementResult.InvalidState, null);
        }

        // 2. 生成新 Block ID
        string newBlockId = $"manual_blk_{Guid.NewGuid().ToString("N")[..8]}";
        Log.Info($"准备手动创建新 Block '{newBlockId}'，父节点: '{parentBlockId}'。");

        // 3. 获取父节点锁并创建 Block (初始可能为 Loading)
        using (await this.GetLockForBlock(parentBlockId).LockAsync())
        {
            // 重新检查父节点状态，以防在等待锁期间发生变化
            if (!this.Blocks.TryGetValue(parentBlockId, out parentBlockStatus) ||
                parentBlockStatus is not IdleBlockStatus updatedIdleParentBlock)
            {
                Log.Warning($"手动创建 Block 失败: 等待锁后，父 Block '{parentBlockId}' 状态不再是 Idle。");
                return (ManagementResult.InvalidState, null); // 或者重试？取决于策略
            }

            // 克隆状态
            var wsInputClone = updatedIdleParentBlock.Block.WsPostUser?.Clone() ?? updatedIdleParentBlock.Block.WsInput.Clone();
            var gameStateClone = updatedIdleParentBlock.Block.GameState.Clone();

            // 使用标准方式创建 Block (返回 Loading)
            var tempLoadingBlock = Block.CreateBlock(newBlockId, parentBlockId, DebugWorkFlowName, wsInputClone, gameStateClone, []);

            // 添加到字典 (仍然需要锁新 Block ID，虽然概率极低，但保险起见)
            using (await this.GetLockForBlock(newBlockId).LockAsync())
            {
                if (!this.Blocks.TryAdd(newBlockId, tempLoadingBlock))
                {
                    Log.Error($"手动创建 Block 时添加 '{newBlockId}' 失败，已存在同名 Block。");
                    // 理论上极不可能，但如果发生，是严重错误
                    return (ManagementResult.Error, null);
                }

                // 更新父节点的孩子列表
                updatedIdleParentBlock.Block.AddChildren(tempLoadingBlock.Block);
                Log.Debug($"父 Block '{parentBlockId}' 已添加子节点 '{newBlockId}'。");
                // 注意：父节点的变更可能需要持久化（如果使用了数据库）
            }
        } // 释放父节点锁

        // 4. 获取新节点锁并强制转换为 Idle 状态
        IdleBlockStatus finalIdleBlock;
        using (await this.GetLockForBlock(newBlockId).LockAsync())
        {
            // 再次检查状态是否仍是 Loading
            if (!this.Blocks.TryGetValue(newBlockId, out var currentStatus) ||
                currentStatus is not LoadingBlockStatus loadingBlock)
            {
                // 可能在释放父锁和获取新锁之间状态被改变了？可能性低但存在
                Log.Error($"尝试将手动创建的 Block '{newBlockId}' 设为 Idle 时发现状态不是 Loading ({currentStatus?.StatusCode})。");
                // 可能需要回滚之前的添加操作？复杂性增加
                return (ManagementResult.Error, null);
            }

            // 执行强制状态转换
            finalIdleBlock = loadingBlock.ForceIdleState();
            this.Blocks[newBlockId] = finalIdleBlock; // 更新字典中的状态

            // 应用初始元数据
            if (initialMetadata != null)
                foreach (var kvp in initialMetadata)
                    // 假设 AddOrSetMetaData 是线程安全的或在此锁内安全
                    finalIdleBlock.Block.AddOrSetMetaData(kvp.Key, kvp.Value);

            Log.Info($"手动创建的 Block '{newBlockId}' 已成功设置为 Idle 状态。");
        } // 释放新节点锁

        return (ManagementResult.Success, finalIdleBlock);
    }

    /// <summary>
    /// (内部实现) 手动删除指定的 Block。
    /// </summary>
    public async Task<ManagementResult> InternalDeleteBlockManuallyAsync(string blockId, bool recursive, bool force)
    {
        if (blockId == WorldRootId)
        {
            Log.Warning("尝试手动删除根节点 '__WORLD__'，操作被拒绝。");
            return ManagementResult.CannotPerformOnRoot;
        }

        List<string> blocksToDelete = [blockId];
        List<string> locksToAcquire = []; // 需要获取锁的 Block ID 列表
        string? parentIdToUpdate = null; // 需要更新其 ChildrenList 的父节点

        // 1. 查找需要删除的 Block 及其父节点，并确定是否可以删除
        using (await this.GetLockForBlock(blockId).LockAsync()) // 先锁住目标节点进行检查
        {
            if (!this.Blocks.TryGetValue(blockId, out var blockToDeleteStatus))
            {
                Log.Warning($"手动删除 Block 失败: Block '{blockId}' 未找到。");
                return ManagementResult.NotFound;
            }

            parentIdToUpdate = blockToDeleteStatus.Block.ParentBlockId; // 记录父节点ID

            // 检查状态是否允许删除 (除非强制)
            if (!force && blockToDeleteStatus is not (IdleBlockStatus or ErrorBlockStatus))
            {
                Log.Warning(
                    $"手动删除 Block 失败: Block '{blockId}' 状态为 {blockToDeleteStatus.StatusCode}，不允许删除（除非 force=true）。");
                return ManagementResult.InvalidState;
            }

            locksToAcquire.Add(blockId); // 确认需要锁定此 Block

            // 如果需要递归删除
            if (recursive)
            {
                var childrenToDelete = this.FindAllDescendants(blockId); // 需要实现 FindAllDescendants
                if (childrenToDelete == null)
                {
                    Log.Error($"手动递归删除 Block '{blockId}' 时查找子孙节点失败。");
                    return ManagementResult.Error; // 查找失败
                }

                blocksToDelete.AddRange(childrenToDelete);
                locksToAcquire.AddRange(childrenToDelete); // 所有子孙都需要锁定
            }
            else if (blockToDeleteStatus.Block.ChildrenList.Count > 0)
            {
                Log.Warning($"手动删除 Block '{blockId}' 失败: Block 有子节点但未指定 recursive=true。");
                return ManagementResult.BadRequest; // 或者定义一个更明确的 Result
            }
        } // 释放目标节点锁

        // 2. 锁定所有相关 Block (父节点和所有要删除的节点)
        // 注意：获取多个锁需要小心死锁。按 ID 排序获取可以避免简单死锁。
        var sortedLockIds = locksToAcquire
            .Union(parentIdToUpdate != null ? [parentIdToUpdate] : [])
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var acquiredLocks = new List<IDisposable>();
        try
        {
            Log.Debug($"准备获取删除操作所需的锁: {string.Join(", ", sortedLockIds)}");
            foreach (string lockId in sortedLockIds) acquiredLocks.Add(await this.GetLockForBlock(lockId).LockAsync());

            Log.Debug("删除操作所需锁已全部获取。");

            // 3. 在锁保护下执行删除
            // 3.1 再次检查父节点是否存在 (以防在等待锁时被删除)
            BlockStatus? parentStatus = null;
            if (parentIdToUpdate != null && !this.Blocks.TryGetValue(parentIdToUpdate, out parentStatus))
            {
                Log.Warning($"手动删除 Block '{blockId}' 失败: 其父节点 '{parentIdToUpdate}' 在获取锁后消失。");
                return ManagementResult.NotFound; // 或 Error?
            }

            // 3.2 移除 Block 字典中的条目
            int removedCount = 0;
            foreach (string idToRemove in blocksToDelete)
                if (this.Blocks.TryRemove(idToRemove, out _))
                {
                    this.BlockLocks.TryRemove(idToRemove, out _); // 同时移除锁对象，避免内存泄漏
                    removedCount++;
                    Log.Debug($"Block '{idToRemove}' 已从字典中移除。");
                }
                else
                {
                    Log.Warning($"尝试删除 Block '{idToRemove}' 时发现它已不在字典中。");
                }

            // 3.3 从父节点的 ChildrenList 中移除
            if (parentIdToUpdate != null && parentStatus != null)
            {
                if (parentStatus.Block.ChildrenList.Remove(blockId))
                    Log.Debug($"Block '{blockId}' 已从父节点 '{parentIdToUpdate}' 的子列表中移除。");
                // 可能需要持久化父节点变更
                else
                    Log.Warning($"尝试从父节点 '{parentIdToUpdate}' 移除子节点 '{blockId}' 时发现它不存在于列表中。");
            }

            Log.Info($"手动删除操作完成。共移除 {removedCount} 个 Block (请求删除 {blocksToDelete.Count} 个)。");
            return ManagementResult.Success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "手动删除 Block 时发生意外错误。");
            return ManagementResult.Error;
        }
        finally
        {
            // 确保所有锁都被释放
            foreach (var lck in acquiredLocks) lck.Dispose();

            Log.Debug("删除操作的锁已释放。");
        }
    }

    /// <summary>
    /// (内部实现) 手动移动指定的 Block 到新的父节点下。
    /// </summary>
    public async Task<ManagementResult> InternalMoveBlockManuallyAsync(string blockId, string newParentBlockId)
    {
        if (blockId == WorldRootId) return ManagementResult.CannotPerformOnRoot;
        if (blockId == newParentBlockId) return ManagementResult.CyclicOperation;

        string? oldParentId;

        // 1. 预检查和信息收集 (只读，获取少量锁)
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.Blocks.TryGetValue(blockId, out var blockToMoveStatus)) return ManagementResult.NotFound;
            oldParentId = blockToMoveStatus.Block.ParentBlockId;

            // 检查状态是否允许移动 (例如，不能移动 Loading/Resolving?) - 根据需要添加
            // if (!force && blockToMoveStatus is LoadingBlockStatus or ConflictBlockStatus) return ManagementResult.InvalidState;

            // 查找所有子孙，检查循环引用
            var descendantIds = this.FindAllDescendants(blockId);
            if (descendantIds == null) return ManagementResult.Error; // 查找失败
            if (descendantIds.Contains(newParentBlockId)) return ManagementResult.CyclicOperation;
        }

        if (oldParentId == newParentBlockId) return ManagementResult.Success; // 没必要移动

        using (await this.GetLockForBlock(newParentBlockId).LockAsync())
        {
            if (!this.Blocks.TryGetValue(newParentBlockId, out var newParentStatus)) return ManagementResult.NotFound;
            // 检查新父节点状态是否允许接收子节点 (e.g., Idle?)
            if (newParentStatus is not IdleBlockStatus) return ManagementResult.InvalidState;
        }

        if (oldParentId == null)
            // 尝试移动一个直接在根下的 Block (旧父节点是 __WORLD__?)
            // 这种情况通常是允许的，但需要确认 oldParentId == WorldRootId
            // 如果 oldParentId 真的是 null (数据错误?)，则可能需要特殊处理或报错
            Log.Warning($"Block '{blockId}' 的 ParentId 为 null，但它不是根节点。数据可能存在问题。");
        // 根据策略决定是否继续或返回错误

        // 2. 锁定所有相关 Block (要移动的, 旧父, 新父)
        var lockIds = new List<string> { blockId, newParentBlockId };
        if (oldParentId != null) lockIds.Add(oldParentId);
        var sortedLockIds = lockIds.Distinct().OrderBy(id => id).ToList();

        var acquiredLocks = new List<IDisposable>();
        try
        {
            Log.Debug($"准备获取移动操作所需的锁: {string.Join(", ", sortedLockIds)}");
            foreach (string lockId in sortedLockIds) acquiredLocks.Add(await this.GetLockForBlock(lockId).LockAsync());

            Log.Debug("移动操作所需锁已全部获取。");

            // 3. 在锁保护下执行移动
            // 3.1 重新获取并验证所有相关 Block 的状态
            if (!this.Blocks.TryGetValue(blockId, out var blockToMove) ||
                !this.Blocks.TryGetValue(newParentBlockId, out var newParent) ||
                (oldParentId != null && !this.Blocks.TryGetValue(oldParentId, out var oldParent)))
            {
                Log.Warning("移动操作失败：一个或多个相关 Block 在获取锁后消失。");
                return ManagementResult.NotFound; // 或者 Error
            }

            // 再次检查状态 (可选，但更安全)
            if (newParent is not IdleBlockStatus) return ManagementResult.InvalidState;
            // if (!force && blockToMove is LoadingBlockStatus or ConflictBlockStatus) return ManagementResult.InvalidState;

            // 3.2 从旧父节点移除 (如果旧父存在且不是根)
            if (oldParentId != null &&
                this.Blocks.TryGetValue(oldParentId, out var oldParentStatus)) // 确保 oldParent 变量已更新
            {
                if (!oldParentStatus.Block.ChildrenList.Remove(blockId))
                    Log.Warning($"尝试从旧父节点 '{oldParentId}' 移除子节点 '{blockId}' 时未找到。数据可能不一致。");
                // 可能需要决定是否继续
                else
                    Log.Debug($"Block '{blockId}' 已从旧父节点 '{oldParentId}' 的子列表中移除。");
                // 持久化旧父节点变更
            }

            // 3.3 更新 Block 的 ParentBlockId
            // Block 是 class，可以直接修改。如果是 record struct，需要创建新实例替换。
            // 假设 Block 是 class:
            blockToMove.Block.GetType().GetProperty("ParentBlockId")
                ?.SetValue(blockToMove.Block, newParentBlockId); // 使用反射或提供 internal setter
            Log.Debug($"Block '{blockId}' 的 ParentBlockId 已更新为 '{newParentBlockId}'。");
            // 持久化 blockToMove 的变更

            // 3.4 添加到新父节点的 ChildrenList
            newParent.Block.AddChildren(blockToMove.Block); // AddChildren 应该处理重复添加 (虽然理论上不应发生)
            Log.Debug($"Block '{blockId}' 已添加到新父节点 '{newParentBlockId}' 的子列表中。");
            // 持久化新父节点变更

            Log.Info($"Block '{blockId}' 已成功移动到父节点 '{newParentBlockId}' 下。");
            return ManagementResult.Success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"手动移动 Block '{blockId}' 时发生意外错误。");
            return ManagementResult.Error;
        }
        finally
        {
            foreach (var lck in acquiredLocks) lck.Dispose();
            Log.Debug("移动操作的锁已释放。");
        }
    }

    /// <summary>
    /// 辅助方法：查找指定 Block ID 的所有后代 Block ID。
    /// 返回 null 表示查找过程中出错。
    /// </summary>
    private IReadOnlyList<string>? FindAllDescendants(string startBlockId)
    {
        var descendants = new List<string>();
        var queue = new Queue<string>();

        // 先获取起始节点的子节点
        if (!this.Blocks.TryGetValue(startBlockId, out var startBlockStatus))
        {
            Log.Error($"FindAllDescendants 错误：起始节点 '{startBlockId}' 未找到。");
            return null; // 起始节点不存在
        }

        foreach (string childId in startBlockStatus.Block.ChildrenList) queue.Enqueue(childId);

        while (queue.Count > 0)
        {
            string currentId = queue.Dequeue();
            if (descendants.Contains(currentId)) continue; // 避免循环引用导致的无限循环

            if (!this.Blocks.TryGetValue(currentId, out var currentBlockStatus))
            {
                Log.Warning($"FindAllDescendants 警告：子孙节点 '{currentId}' 在字典中未找到，可能数据不一致。");
                continue; // 跳过不存在的节点
            }

            descendants.Add(currentId);

            foreach (string childId in currentBlockStatus.Block.ChildrenList) queue.Enqueue(childId);
        }

        return descendants;
    }
}