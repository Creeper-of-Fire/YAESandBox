using YAESandBox.Core.Block;

namespace YAESandBox.API.Services;

public class BasicBlockService(INotifierService notifierService, BlockManager blockManager)
{
    protected INotifierService notifierService { get; } = notifierService;
    protected BlockManager blockManager { get; } = blockManager;

    /// <summary>
    /// 获得原始Block对象——不暴露给外部类
    /// </summary>
    /// <param name="blockId"></param>
    /// <returns></returns>
    protected Task<BlockStatus?> GetBlockAsync(string blockId) =>
        this.blockManager.GetBlockAsync(blockId);
}