using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Core.VarSpec;

namespace YAESandBox.Workflow.ExactRune.SillyTavern;

/// <summary>
/// 代表最终提示词列表模板中的一个条目。
/// 它是一个经过处理的、简化的 PromptItem 版本，
/// 明确了其作为标记（Mark）还是具体提示词（Prompt）的性质，并携带了最终生效的角色。
/// </summary>
public record PromptTemplateItem
{
    /// <summary>
    /// 来自原始 PromptItem 的唯一标识符。
    /// 对于标记，这就是标记的名称（如 "chatHistory"）。
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// 指示此条目是否为位置标记。
    /// </summary>
    public required bool IsMark { get; init; }

    /// <summary>
    /// 如果这不是一个标记，此属性包含提示词的具体内容。
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// 此条目最终生效的角色，已处理过 "system_prompt" 覆盖逻辑。
    /// </summary>
    public required PromptRoleType Role { get; init; }
}

/// <summary>
/// 存储从预设中提取和分类后的处理结果。
/// </summary>
public record PresetProcessingResult
{
    /// <summary>
    /// 一个有序的模板列表，用于构建最终的提示词。
    /// </summary>
    public List<PromptTemplateItem> Template { get; init; } = [];

    /// <summary>
    /// 所有需要深度注入到聊天记录中的指令列表。
    /// </summary>
    public List<DepthInjectionCommand> DepthInjections { get; init; } = [];
}

public static partial class SillyTavernProcessor
{
    /// <summary>
    /// 最终组装函数。将所有处理过的部分（预设模板、世界书结果、聊天记录）合成为最终的提示词列表。
    /// </summary>
    /// <param name="presetResult">从预设中提取的模板和注入指令。</param>
    /// <param name="worldInfoResult">从世界书中处理得到的内容和注入指令。</param>
    /// <param name="originalHistory">原始的、未经修改的聊天记录。</param>
    /// <param name="playerCharacter">用于填充 personaDescription 的玩家角色信息。</param>
    /// <param name="targetCharacter">用于填充 charDescription 的目标角色信息。</param>
    /// <returns>一个完整的、有序的、可直接发送给AI的提示词列表。</returns>
    public static List<RoledPromptDto> AssembleFinalPromptList(
        PresetProcessingResult presetResult,
        WorldInfoProcessingResult worldInfoResult,
        List<RoledPromptDto> originalHistory,
        ThingInfo playerCharacter,
        ThingInfo targetCharacter)
    {
        // --- 步骤 1: 合并所有深度注入指令 ---
        var allInjections = new List<DepthInjectionCommand>();
        allInjections.AddRange(presetResult.DepthInjections);
        allInjections.AddRange(worldInfoResult.DepthInjections);

        // --- 步骤 2: 将合并后的指令注入到历史记录中 ---
        var modifiedHistory = InjectCommandsIntoHistory(originalHistory, allInjections);

        // --- 步骤 3: 遍历模板，进行最终组装 ---
        var finalPromptList = new List<RoledPromptDto>();

        foreach (var templateItem in presetResult.Template)
        {
            if (!templateItem.IsMark)
            {
                // 如果是具体提示词，直接使用其内容和已计算好的角色
                if (templateItem.Content != null)
                {
                    finalPromptList.Add(new RoledPromptDto
                    {
                        Role = templateItem.Role,
                        Content = templateItem.Content,
                        Name = string.Empty
                    });
                }
                continue;
            }

            // 处理标记替换
            switch (templateItem.Identifier)
            {
                case "chatHistory":
                    finalPromptList.AddRange(modifiedHistory);
                    break;

                case "worldInfoBefore":
                    if (!string.IsNullOrWhiteSpace(worldInfoResult.WorldInfoBefore))
                    {
                        finalPromptList.Add(new RoledPromptDto { Role = templateItem.Role, Content = worldInfoResult.WorldInfoBefore });
                    }

                    break;

                case "worldInfoAfter":
                    if (!string.IsNullOrWhiteSpace(worldInfoResult.WorldInfoAfter))
                    {
                        finalPromptList.Add(new RoledPromptDto { Role = templateItem.Role, Content = worldInfoResult.WorldInfoAfter });
                    }

                    break;

                case "charDescription":
                    if (!string.IsNullOrWhiteSpace(targetCharacter.Description))
                    {
                        finalPromptList.Add(new RoledPromptDto { Role = templateItem.Role, Content = targetCharacter.Description });
                    }

                    break;

                case "personaDescription":
                    if (!string.IsNullOrWhiteSpace(playerCharacter.Description))
                    {
                        finalPromptList.Add(new RoledPromptDto { Role = templateItem.Role, Content = playerCharacter.Description });
                    }

                    break;

                // 其他标记（如 scenario 等）在此阶段被忽略，
                // 因为我们没有对应的数据源来填充它们。
            }
        }

        return finalPromptList;
    }

    /// <summary>
    /// 将深度注入指令应用到聊天记录列表中。
    /// </summary>
    /// <param name="originalHistory">原始的聊天记录。</param>
    /// <param name="commands">从世界书和预设中收集的所有深度注入指令。</param>
    /// <returns>一个新的、包含了注入内容的聊天记录列表。</returns>
    public static List<RoledPromptDto> InjectCommandsIntoHistory(
        List<RoledPromptDto> originalHistory,
        List<DepthInjectionCommand> commands)
    {
        if (commands.Count == 0)
        {
            return [..originalHistory];
        }

        // 关键步骤：对指令进行排序，以确保插入顺序正确。
        // 1. 按深度(Depth)降序排序：深度值大的（更靠前的历史）先处理。
        // 2. 在相同深度下，按插入顺序(InsertionOrder)升序排序：Order小的先插入。
        var sortedCommands = commands
            .OrderByDescending(c => c.Depth)
            .ThenBy(c => c.InsertionOrder);

        var newHistory = new List<RoledPromptDto>(originalHistory);

        foreach (var command in sortedCommands)
        {
            var newPrompt = new RoledPromptDto
            {
                Role = command.Role,
                Content = command.Content,
                Name = "" // 预设和世界书的注入通常不带角色名
            };

            // 计算插入索引
            // 0 = 列表末尾, 1 = 倒数第二个位置, etc.
            int targetIndex = newHistory.Count - command.Depth;

            // 边界检查：如果深度超出范围，则插入到列表的最开头
            if (targetIndex < 0)
            {
                targetIndex = 0;
            }

            newHistory.Insert(targetIndex, newPrompt);
        }

        return newHistory;
    }
}