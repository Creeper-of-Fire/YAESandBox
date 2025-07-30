using YAESandBox.Depend;
using YAESandBox.Depend.Results;

namespace YAESandBox.Core;

public record NormalBlockError(BlockResultCode Code, string Message) : Error(Message)
{
    public static NormalBlockError NotFound(string message)
    {
        return new NormalBlockError(BlockResultCode.NotFound, message);
    }

    public static NormalBlockError Conflict(string message)
    {
        return new NormalBlockError(BlockResultCode.Conflict, message);
    }

    public static NormalBlockError InvalidInput(string message)
    {
        return new NormalBlockError(BlockResultCode.InvalidInput, message);
    }

    public static NormalBlockError Error(string message)
    {
        return new NormalBlockError(BlockResultCode.Error, message);
    }

    public static NormalBlockError InvalidState(string message)
    {
        return new NormalBlockError(BlockResultCode.InvalidState, message);
    }
}