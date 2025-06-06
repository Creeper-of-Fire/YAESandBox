using YAESandBox.Core.Block;
using YAESandBox.Core.Block.BlockManager;

namespace YAESandBox.Core.Services.InterFaceAndBasic;

public class BasicBlockService(IBlockManager blockManager, INotifierService notifierService)
{
    /// <summary>
    /// 通知服务
    /// </summary>
    protected INotifierService NotifierService { get; } = notifierService;

    /// <summary>
    /// Block管理器
    /// </summary>
    protected IBlockManager BlockManager { get; } = blockManager;

    /// <summary>
    /// 获得原始Block对象——不暴露给外部类
    /// </summary>
    /// <param name="blockId"></param>
    /// <returns></returns>
    protected Task<BlockStatus?> GetBlockAsync(string blockId)
    {
        return this.BlockManager.GetBlockAsync(blockId);
    }
}