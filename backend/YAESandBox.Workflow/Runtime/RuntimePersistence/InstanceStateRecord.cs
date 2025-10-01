using System.Text.Json;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;

namespace YAESandBox.Workflow.Runtime.RuntimePersistence;

/// <summary>
/// 存储单个处理器实例的持久化状态的抽象基类。
/// 这是一个密封的辨别联合体系，确保每种状态的数据完整性。
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$stateType")]
[JsonDerivedType(typeof(RunningState), typeDiscriminator: "running")]
[JsonDerivedType(typeof(CompletedState), typeDiscriminator: "completed")]
[JsonDerivedType(typeof(FailedState), typeDiscriminator: "failed")]
public abstract record InstanceStateRecord(Guid InstanceId);

/// <summary>
/// 表示一个正在执行中的实例状态。
/// </summary>
/// <param name="InstanceId">实例的唯一ID。</param>
/// <param name="SerializedInputs">实例执行时的输入快照（非空）。</param>
public sealed record RunningState(Guid InstanceId, string SerializedInputs) : InstanceStateRecord(InstanceId)
{
    /// <summary>
    /// 创建一个正在执行中的实例状态。
    /// </summary>
    /// <param name="instanceId"></param>
    /// <param name="inputs"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static RunningState Create<TInput>(Guid instanceId, TInput inputs)
    {
        return new RunningState(
            instanceId,
            ValueWrapper.SerializeAsWrapper(inputs)
        );
    }

    /// <summary>
    /// 获得反序列化后的输入数据。
    /// </summary>
    /// <exception cref="JsonException"></exception>
    public TInput GetInput<TInput>()
    {
        return ValueWrapper.ParseValue<TInput>(this.SerializedInputs);
    }
}

/// <summary>
/// 表示一个已成功完成的实例状态。
/// </summary>
/// <param name="InstanceId">实例的唯一ID。</param>
/// <param name="SerializedInputs">实例执行时的输入快照（非空）。</param>
/// <param name="SerializedPayload">实例成功完成时需要持久化的数据载荷（非空）。</param>
public sealed record CompletedState(Guid InstanceId, string SerializedInputs, string SerializedPayload) : InstanceStateRecord(InstanceId)
{
    /// <summary>
    /// 创建一个已成功完成的实例状态。
    /// </summary>
    /// <param name="instanceId"></param>
    /// <param name="inputs"></param>
    /// <param name="payload"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TPayload"></typeparam>
    /// <returns></returns>
    public static CompletedState Create<TInput, TPayload>(Guid instanceId, TInput inputs, TPayload payload)
    {
        return new CompletedState(
            instanceId,
            ValueWrapper.SerializeAsWrapper(inputs),
            ValueWrapper.SerializeAsWrapper(payload)
        );
    }

    /// <summary>
    /// 获得反序列化后的输入数据。
    /// </summary>
    /// <exception cref="JsonException"></exception>
    public TInput GetInput<TInput>()
    {
        return ValueWrapper.ParseValue<TInput>(this.SerializedInputs);
    }

    /// <summary>
    /// 获得反序列化后的持久化数据载荷。
    /// </summary>
    /// <exception cref="JsonException"></exception>
    public TPayload GetPayload<TPayload>()
    {
        return ValueWrapper.ParseValue<TPayload>(this.SerializedPayload);
    }
}

/// <summary>
/// 表示一个执行失败的实例状态。
/// </summary>
/// <param name="InstanceId">实例的唯一ID。</param>
/// <param name="SerializedInputs">实例执行时的输入快照（非空）。</param>
/// <param name="ErrorDetails">实例失败时的错误信息（非空）。</param>
public sealed record FailedState(Guid InstanceId, string SerializedInputs, Error ErrorDetails) : InstanceStateRecord(InstanceId)
{
    /// <summary>
    /// 创建一个执行失败的实例状态。
    /// </summary>
    /// <param name="instanceId"></param>
    /// <param name="inputs"></param>
    /// <param name="error"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static FailedState Create<TInput>(Guid instanceId, TInput inputs, Error error)
    {
        return new FailedState(
            instanceId,
            ValueWrapper.SerializeAsWrapper(inputs),
            error
        );
    }

    /// <summary>
    /// 获得反序列化后的输入数据。
    /// </summary>
    /// <exception cref="JsonException"></exception>
    public TInput GetInput<TInput>()
    {
        return ValueWrapper.ParseValue<TInput>(this.SerializedInputs);
    }
}