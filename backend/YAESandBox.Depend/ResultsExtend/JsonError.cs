using System.Text.Json.Nodes;
using JetBrains.Annotations;
using YAESandBox.Depend.Results;

namespace YAESandBox.Depend.ResultsExtend;

internal record JsonError(string? OriginJsonString, string Message, Exception? Exception = null) : Error(Message, Exception), IErrorCanBeDto
{
    internal static JsonError Error(string? originJsonString, string message, Exception? exception = null)
    {
        return new JsonError(originJsonString, message, exception);
    }

    internal static JsonError Error(JsonNode? originJsonNode, string message, Exception? exception = null)
    {
        return new JsonError(originJsonNode?.ToJsonString(), message, exception);
    }

    /// <inheritdoc />
    public ResultDto<TValue> ToDto<TValue>() => new JsonResultDto<TValue>
        { ErrorDetails = this.ToDetailString(), Data = default, IsSuccess = false, OriginJsonString = this.OriginJsonString };
}

/// <inheritdoc />
/// <remarks>这是存在Json序列化/反序列化时可以对外使用的更全面的DTO</remarks>
public record JsonResultDto<TData> : ResultDto<TData>
{
    /// <summary>
    /// 失败时，有可能返回序列化错误时的原始文本
    /// </summary>
    public required string? OriginJsonString { get; set; }

    /// <summary>
    /// 把<see cref="ResultDto{TValue}"/>转换成<see cref="JsonResultDto{TData}"/>，如果不需要转换，则返回原始值而非拷贝。
    /// </summary>
    /// <param name="resultDto"></param>
    /// <returns></returns>
    [MustUseReturnValue]
    public static JsonResultDto<TData> ToJsonResultDto(ResultDto<TData> resultDto)
    {
        if (resultDto is JsonResultDto<TData> jsonResultDto)
            return jsonResultDto;
        return new JsonResultDto<TData>
        {
            IsSuccess = resultDto.IsSuccess,
            Data = resultDto.Data,
            ErrorDetails = resultDto.ErrorDetails,
            OriginJsonString = null
        };
    }
}