using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using YAESandBox.Core.Action; // For Operator enum reference if needed, or use strings
using YAESandBox.Core.State.Entity; // For EntityType

namespace YAESandBox.API.DTOs;

/// <summary>
/// 用于 API 请求的单个原子操作的表示。
/// 定义了对 WorldState 中实体的创建、修改或删除操作。
/// </summary>
public record AtomicOperationRequestDto
{
    /// <summary>
    /// 操作类型。必须是 "CreateEntity", "ModifyEntity", 或 "DeleteEntity" (不区分大小写)。
    /// </summary>
    [Required(ErrorMessage = "必须指定操作类型")]
    public string OperationType { get; set; } = null!;

    /// <summary>
    /// 操作目标实体的类型 (Item, Character, Place)。
    /// </summary>
    [Required(ErrorMessage = "必须指定实体类型")]
    public EntityType EntityType { get; set; }

    /// <summary>
    /// 操作目标实体的唯一 ID。不能为空或仅包含空白字符。
    /// </summary>
    [Required(ErrorMessage = "必须指定实体 ID")]
    [MinLength(1, ErrorMessage = "实体 ID 不能为空")]
    public string EntityId { get; set; } = null!;

    // --- CreateEntity 参数 ---
    /// <summary>
    /// (仅用于 CreateEntity 操作)
    /// 要创建的实体的初始属性字典。键是属性名，值是属性值。
    /// 如果不提供，则实体以默认属性创建。
    /// </summary>
    public Dictionary<string, object?>? InitialAttributes { get; set; }

    // --- ModifyEntity 参数 ---
    /// <summary>
    /// (仅用于 ModifyEntity 操作)
    /// 要修改的属性的键（名称）。
    /// </summary>
    public string? AttributeKey { get; set; }

    /// <summary>
    /// (仅用于 ModifyEntity 操作)
    /// 修改操作符。预期值为 "=", "+=", "-=" 等表示赋值、增加、减少等操作的字符串。
    /// 具体支持的操作符取决于后端实现 (<see cref="OperatorHelper"/>)。
    /// </summary>
    public string? ModifyOperator { get; set; }

    /// <summary>
    /// (仅用于 ModifyEntity 操作)
    /// 修改操作的值。类型应与目标属性和操作符兼容。
    /// </summary>
    public object? ModifyValue { get; set; }

    // --- DeleteEntity 参数 ---
    // DeleteEntity 操作不需要额外的特定参数。
}

/// <summary>
/// 提供 AtomicOperation 和 AtomicOperationRequestDto 之间转换的辅助方法。
/// </summary>
public static class AtomicOperationRequestDtoHelper
{
    /// <summary>
    /// 将 AtomicOperation 转换为 AtomicOperationRequestDto。
    /// </summary>
    /// <param name="operation"></param>
    /// <returns></returns>
    public static AtomicOperationRequestDto ToAtomicOperationRequestDto(this AtomicOperation operation)
    {
        return new AtomicOperationRequestDto
        {
            OperationType = operation.OperationType.ToString(),
            EntityType = operation.EntityType,
            EntityId = operation.EntityId,
            AttributeKey = operation.AttributeKey,
            ModifyOperator = OperatorHelper.OperatorToString(operation.ModifyOperator),
            ModifyValue = operation.ModifyValue,
            InitialAttributes = operation.InitialAttributes
        };
    }

    
    // public static AtomicOperation ToAtomicOperation(this AtomicOperationRequestDto dto)
    // {
    //     return new AtomicOperation
    //     {
    //         OperationType = dto.OperationType switch
    //         {
    //             "CreateEntity" => AtomicOperationType.CreateEntity,
    //             "ModifyEntity" => AtomicOperationType.ModifyEntity,
    //             "DeleteEntity" => AtomicOperationType.DeleteEntity,
    //             _ => throw new ArgumentException("Invalid operation type")
    //         },
    //         EntityType = dto.EntityType,
    //         EntityId = dto.EntityId,
    //         AttributeKey = dto.AttributeKey,
    //         ModifyOperator = OperatorHelper.StringToOperatorCanBeNull(dto.ModifyOperator),
    //         ModifyValue = dto.ModifyValue,
    //         InitialAttributes = dto.InitialAttributes
    //     };
    // }
    /// <summary>
    /// 将 AtomicOperationRequestDto 转换为 AtomicOperation。
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">无效的操作类型、实体 ID 为空、操作缺乏键、操作符无效</exception>
    public static AtomicOperation ToAtomicOperation(this AtomicOperationRequestDto dto)
    {
        // 1. 验证并解析 OperationType
        if (!Enum.TryParse<AtomicOperationType>(dto.OperationType, true, out var opType))
            throw new ArgumentException($"无效的操作类型: '{dto.OperationType}'。有效值为 CreateEntity, ModifyEntity, DeleteEntity。");

        // 2. 验证 EntityId (所有操作都需要)
        if (string.IsNullOrWhiteSpace(dto.EntityId))
            throw new ArgumentException("实体 ID (EntityId) 不能为空。");

        // 3. 根据操作类型进行特定验证和创建
        switch (opType)
        {
            case AtomicOperationType.CreateEntity:
                // Create 操作的 InitialAttributes 是可选的，工厂方法会处理 null
                // 工厂方法内部会再次验证 EntityId，这里主要是为了提前失败
                return AtomicOperation.Create(dto.EntityType, dto.EntityId, dto.InitialAttributes);

            case AtomicOperationType.ModifyEntity:
                // 验证 Modify 操作必需的字段
                if (string.IsNullOrWhiteSpace(dto.AttributeKey))
                {
                    throw new ArgumentException("ModifyEntity 操作必须提供属性键 (AttributeKey)。");
                }
                if (string.IsNullOrWhiteSpace(dto.ModifyOperator))
                {
                    throw new ArgumentException("ModifyEntity 操作必须提供修改操作符 (ModifyOperator)。");
                }
                // ModifyValue 可以为 null，取决于业务逻辑，工厂方法接受 object?

                Operator op;
                try
                {
                    // 使用会抛异常的转换器，确保操作符有效
                    op = OperatorHelper.StringToOperator(dto.ModifyOperator);
                }
                catch (ArgumentException ex)
                {
                    // 包装一下异常，提供更多上下文
                    throw new ArgumentException($"无效的修改操作符 (ModifyOperator): '{dto.ModifyOperator}'. {ex.Message}", ex);
                }

                // 调用工厂方法，它内部会再次验证 EntityId 和 AttributeKey
                return AtomicOperation.Modify(dto.EntityType, dto.EntityId, dto.AttributeKey, op, dto.ModifyValue);

            case AtomicOperationType.DeleteEntity:
                // Delete 操作只需要 EntityType 和 EntityId，已验证
                // 工厂方法内部会再次验证 EntityId
                return AtomicOperation.Delete(dto.EntityType, dto.EntityId);

            default:
                // 理论上 Enum.TryParse 成功后不会到这里，但作为保险
                throw new UnreachableException($"未处理的操作类型: {opType}");
        }
    }


    /// <summary>
    /// 将 AtomicOperationRequestDto 列表转换为 AtomicOperation 列表。
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">无效的操作类型、实体 ID 为空、操作缺乏键、操作符无效</exception>
    public static List<AtomicOperation> ToAtomicOperations(this List<AtomicOperationRequestDto> dto)
    {
        return dto.Select(ToAtomicOperation).ToList();
    }

    /// <summary>
    /// 将 AtomicOperation 列表转换为 AtomicOperationRequestDto 列表。
    /// </summary>
    /// <param name="operations"></param>
    /// <returns></returns>
    public static List<AtomicOperationRequestDto> ToAtomicOperationRequests(this List<AtomicOperation> operations)
    {
        return operations.Select(ToAtomicOperationRequestDto).ToList();
    }
}

/// <summary>
/// 包含用于批量执行的原子操作请求列表。
/// </summary>
public record BatchAtomicRequestDto
{
    /// <summary>
    /// 要执行的原子操作请求列表。该列表不能为空。
    /// </summary>
    [Required(ErrorMessage = "操作列表不能为空")]
    [MinLength(1, ErrorMessage = "操作列表至少需要包含一个操作")]
    public List<AtomicOperationRequestDto> Operations { get; set; } = new();
}