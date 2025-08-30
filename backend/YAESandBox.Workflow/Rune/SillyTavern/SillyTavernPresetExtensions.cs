using YAESandBox.Workflow.AIService;
using static YAESandBox.Workflow.AIService.PromptRoleTypeExtension;

namespace YAESandBox.Workflow.Rune.SillyTavern;

/// <summary>
/// 为 SillyTavern 数据模型提供辅助扩展方法。
/// </summary>
public static class SillyTavernModelExtensions
{
    /// <summary>
    /// 根据 SillyTavern 的 "system_prompt" 覆盖规则，获取一个 PromptItem 最终生效的角色类型。
    /// 如果 "system_prompt" 为 true，则角色总是 System。否则，使用 "role" 字段。
    /// </summary>
    /// <param name="item">要分析的提示词项。</param>
    /// <returns>最终生效的 PromptRoleType。</returns>
    public static PromptRoleType GetEffectiveRoleType(this PromptItem item)
    {
        if (item.SystemPrompt)
        {
            return PromptRoleType.System;
        }

        // 如果不是系统提示，则根据 role 字段转换，默认为 system
        return ToPromptRoleType(item.Role ?? "system");
    }
}

/// <summary>
/// 为 SillyTavern 预设模型提供辅助功能的扩展方法。
/// </summary>
public static class SillyTavernPresetExtensions
{
    /// <summary>
    /// 将 SillyTavern 预设转换为一个根据指定角色排序并过滤掉禁用项的 PromptItem 列表。
    /// </summary>
    /// <param name="preset">要处理的 SillyTavernPreset 实例。</param>
    /// <param name="characterId">
    /// 可选的角色ID。如果提供，将精确查找该角色的顺序配置。
    /// 如果为 null，将按以下顺序尝试回退查找：100001 (常见默认), 100000 (次常见), 预设中的第一个。
    /// </param>
    /// <returns>一个有序的、只包含启用项的 PromptItem 列表。如果找不到有效的顺序配置，则返回空列表。</returns>
    public static List<PromptItem> GetOrderedPrompts(this SillyTavernPreset preset, long? characterId = null)
    {
        if (!preset.PromptOrder.Any())
        {
            return [];
        }

        // 1. 根据 character_id 找到正确的 `prompt_order` 配置
        PromptOrderSetting? orderSetting = null;

        if (characterId.HasValue)
        {
            orderSetting = preset.PromptOrder.FirstOrDefault(o => o.CharacterId == characterId.Value);
        }

        // 如果未提供ID或找不到精确匹配，则执行智能回退
        orderSetting ??= preset.PromptOrder.FirstOrDefault(o => o.CharacterId == 100001)
                         ?? preset.PromptOrder.FirstOrDefault(o => o.CharacterId == 100000)
                         ?? preset.PromptOrder.FirstOrDefault();

        if (orderSetting is null)
        {
            // 没有找到任何可用的顺序配置
            return [];
        }

        // 为了高效查找，将 prompts 列表转换为字典
        var promptLookup = preset.Prompts.ToDictionary(p => p.Identifier);

        var orderedPrompts = new List<PromptItem>();

        // 2 & 3. 遍历 order 列表并过滤 enabled=false 的项
        foreach (var orderItem in orderSetting.Order)
        {
            if (!orderItem.Enabled)
            {
                continue;
            }

            // 4. 根据 identifier 查找完整的 PromptItem
            if (promptLookup.TryGetValue(orderItem.Identifier, out var promptItem))
            {
                // 5. 按顺序添加到最终列表
                orderedPrompts.Add(promptItem);
            }
            // 如果在 promptLookup 中找不到，说明预设文件数据可能不一致，此处选择静默忽略。
        }

        return orderedPrompts;
    }

    /// <summary>
    /// 从有序的 PromptItem 列表中提取深度注入指令，并将其余部分转换为一个模板。
    /// </summary>
    /// <param name="orderedPrompts">已排序和过滤的 PromptItem 列表。</param>
    /// <returns>一个包含模板和注入指令的结果对象。</returns>
    public static PresetProcessingResult ExtractAndTemplate(this List<PromptItem> orderedPrompts)
    {
        var template = new List<PromptTemplateItem>();
        var injections = new List<DepthInjectionCommand>();

        foreach (var item in orderedPrompts)
        {
            // injection_position == 1 表示启用深度注入模式
            if (item.InjectionPosition == 1)
            {
                var command = new DepthInjectionCommand(
                    Content: item.Content ?? string.Empty,
                    Depth: item.InjectionDepth ?? 0,
                    Role: item.GetEffectiveRoleType(),
                    InsertionOrder: item.InjectionOrder ?? 100 // 提供一个默认值
                );
                injections.Add(command);
            }
            else // 否则，按顺序处理
            {
                // 创建新的、信息更完整的 PromptTemplateItem
                template.Add(new PromptTemplateItem
                {
                    Identifier = item.Identifier,
                    IsMark = item.Marker,
                    Content = item.Content,
                    // 使用辅助函数计算并存储最终生效的角色
                    Role = item.GetEffectiveRoleType() 
                });
            }
        }

        return new PresetProcessingResult { Template = template, DepthInjections = injections };
    }
}