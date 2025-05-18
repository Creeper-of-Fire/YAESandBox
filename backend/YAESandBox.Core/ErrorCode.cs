using YAESandBox.Depend;
using YAESandBox.Depend.Results;

namespace YAESandBox.Core;

public record NormalHandledIssue(BlockResultCode Code, string Message) : LazyInitHandledIssue(Message)
{
    public static NormalHandledIssue NotFound(string message)
    {
        return new NormalHandledIssue(BlockResultCode.NotFound, message);
    }

    public static NormalHandledIssue Conflict(string message)
    {
        return new NormalHandledIssue(BlockResultCode.Conflict, message);
    }

    public static NormalHandledIssue InvalidInput(string message)
    {
        return new NormalHandledIssue(BlockResultCode.InvalidInput, message);
    }

    public static NormalHandledIssue Error(string message)
    {
        return new NormalHandledIssue(BlockResultCode.Error, message);
    }

    public static NormalHandledIssue InvalidState(string message)
    {
        return new NormalHandledIssue(BlockResultCode.InvalidState, message);
    }
}

public record NormalBlockError(BlockResultCode Code, string Message) : LazyInitError(Message)
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