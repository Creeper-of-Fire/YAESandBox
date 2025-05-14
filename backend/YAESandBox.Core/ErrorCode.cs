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

public record NormalError(BlockResultCode Code, string Message) : LazyInitError(Message)
{
    public static NormalError NotFound(string message)
    {
        return new NormalError(BlockResultCode.NotFound, message);
    }

    public static NormalError Conflict(string message)
    {
        return new NormalError(BlockResultCode.Conflict, message);
    }

    public static NormalError InvalidInput(string message)
    {
        return new NormalError(BlockResultCode.InvalidInput, message);
    }

    public static NormalError Error(string message)
    {
        return new NormalError(BlockResultCode.Error, message);
    }

    public static NormalError InvalidState(string message)
    {
        return new NormalError(BlockResultCode.InvalidState, message);
    }
}