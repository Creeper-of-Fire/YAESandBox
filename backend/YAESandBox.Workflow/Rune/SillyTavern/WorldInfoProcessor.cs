using System.Text;
using System.Text.RegularExpressions;
using YAESandBox.Workflow.AIService;

namespace YAESandBox.Workflow.Rune.SillyTavern;

/// <summary>
/// 代表一个需要深度插入到聊天记录中的原子化指令。
/// </summary>
/// <param name="Content">要插入的内容。</param>
/// <param name="Depth">插入深度 (0=最末尾, 1=倒数第二条消息前, etc.)。</param>
/// <param name="Role">插入时使用的角色 (System, User, Assistant)。</param>
/// <param name="InsertionOrder">排序优先级。值越小越靠前。</param>
public record DepthInjectionCommand(string Content, int Depth, PromptRoleType Role, int InsertionOrder);

/// <summary>
/// 存储世界书处理后的结果。
/// </summary>
public class WorldInfoProcessingResult
{
    /// <summary>
    /// 所有需要插入在 worldInfoBefore 标记位置的内容，已按顺序合并。
    /// </summary>
    public string WorldInfoBefore { get; internal set; } = string.Empty;

    /// <summary>
    /// 所有需要插入在 worldInfoAfter 标记位置的内容，已按顺序合并。
    /// </summary>
    public string WorldInfoAfter { get; internal set; } = string.Empty;

    /// <summary>
    /// 所有需要按深度插入到聊天记录中的指令列表。
    /// </summary>
    public List<DepthInjectionCommand> DepthInjections { get; internal set; } = [];
}

/// <summary>
/// 提供处理 SillyTavern 预设和世界书的静态方法。
/// </summary>
public static partial class SillyTavernProcessor
{
    /// <summary>
    /// 处理世界书列表，根据聊天记录激活条目，并生成插入内容和指令。
    /// </summary>
    /// <param name="worldBooks">要处理的世界书列表。</param>
    /// <param name="chatHistory">用于关键字匹配的聊天记录。</param>
    /// <param name="globalScanDepth">全局扫描深度。定义在历史记录中回溯多少条消息进行匹配。</param>
    /// <param name="maxRecursionDepth">最大递归深度。0表示无限（内部会设一个安全上限）。</param>
    /// <returns>一个包含 worldInfoBefore/After 内容和深度注入指令的结果对象。</returns>
    public static WorldInfoProcessingResult ProcessWorldInfo(
        List<SillyTavernWorldInfo> worldBooks,
        List<RoledPromptDto> chatHistory,
        int globalScanDepth,
        int maxRecursionDepth)
    {
        // 聚合所有未禁用的条目
        var allEntries = worldBooks
            .SelectMany(wb => wb.Entries.Values)
            .Where(entry => !entry.IsDisabled)
            .ToList();

        if (allEntries.Count == 0)
        {
            return new WorldInfoProcessingResult();
        }

        var activatedEntries = new HashSet<WorldInfoEntry>();
        var newActivationsFromLastPass = new List<WorldInfoEntry>();

        // --- 1. 初次激活 ---
        // 尊重每个条目自身的扫描深度覆盖
        foreach (var entry in allEntries)
        {
            int effectiveScanDepth = entry.ScanDepthOverride ?? globalScanDepth;

            // 如果扫描深度为0且不是常驻条目，则跳过
            if (effectiveScanDepth <= 0 && !entry.IsConstant)
            {
                continue;
            }

            string scanBuffer = string.Join("\n", chatHistory.TakeLast(effectiveScanDepth).Select(p => p.Content));

            if (entry.IsConstant || (!string.IsNullOrEmpty(scanBuffer) && IsEntryTriggered(entry, scanBuffer)))
            {
                if (activatedEntries.Add(entry))
                {
                    newActivationsFromLastPass.Add(entry);
                }
            }
        }

        // --- 2. 递归激活 ---
        // 设置递归次数上限，0表示无限，我们用总条目数作为安全上限
        int recursionLimit = (maxRecursionDepth == 0) ? allEntries.Count : maxRecursionDepth;

        for (int i = 0; i < recursionLimit; i++)
        {
            if (!newActivationsFromLastPass.Any())
            {
                break; // 没有新的激活，提前退出循环
            }

            string recursiveScanBuffer = string.Join("\n", newActivationsFromLastPass
                .Where(e => !e.PreventFurtherRecursion)
                .Select(e => e.Content));

            var newlyActivatedThisPass = new List<WorldInfoEntry>();

            if (string.IsNullOrWhiteSpace(recursiveScanBuffer)) break;

            // 检查所有尚未激活且允许被递归的条目
            foreach (var entry in allEntries.Where(e => !activatedEntries.Contains(e) && !e.ExcludeFromRecursion))
            {
                if (IsEntryTriggered(entry, recursiveScanBuffer))
                {
                    // 立即添加到主集合以防重复添加
                    if (activatedEntries.Add(entry))
                    {
                        newlyActivatedThisPass.Add(entry);
                    }
                }
            }

            // 为下一轮递归做准备
            newActivationsFromLastPass = newlyActivatedThisPass;
        }

        // --- 3. 过滤与选择 ---
        var finalEntries = activatedEntries.ToList();
        finalEntries = finalEntries.Where(e => !e.UseProbability || Random.Shared.Next(100) < e.Probability).ToList();
        finalEntries = HandleInclusionGroups(finalEntries);

        // --- 4. 排序 ---
        finalEntries = finalEntries.OrderBy(e => e.InsertionOrder).ToList();

        // --- 5. 分类与组装 ---
        return AssembleResults(finalEntries);
    }

    private static bool IsEntryTriggered(WorldInfoEntry entry, string scanText)
    {
        // 如果没有主关键字，则无法通过扫描触发
        if (entry.Keys.Count == 0)
            return false;

        // TODO 这里的匹配逻辑还不完善
        bool primaryMatch = entry.Keys.Any(key =>
            Regex.IsMatch(scanText, key, RegexOptions.IgnoreCase));

        if (!primaryMatch) return false;

        if (!entry.UseSecondaryKeys || entry.SecondaryKeys.Count == 0)
        {
            return true;
        }

        // 处理次要关键字逻辑
        return (SelectiveLogic)entry.SelectiveLogic switch
        {
            SelectiveLogic.AndAny => entry.SecondaryKeys.Any(key => Regex.IsMatch(scanText, key, RegexOptions.IgnoreCase)),
            SelectiveLogic.AndAll => entry.SecondaryKeys.All(key => Regex.IsMatch(scanText, key, RegexOptions.IgnoreCase)),
            SelectiveLogic.NotAny => !entry.SecondaryKeys.Any(key => Regex.IsMatch(scanText, key, RegexOptions.IgnoreCase)),
            SelectiveLogic.NotAll => !entry.SecondaryKeys.All(key => Regex.IsMatch(scanText, key, RegexOptions.IgnoreCase)),
            _ => true
        };
    }

    private static List<WorldInfoEntry> HandleInclusionGroups(List<WorldInfoEntry> entries)
    {
        var grouped = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Group))
            .GroupBy(e => e.Group);

        var winners = new HashSet<WorldInfoEntry>();
        var losers = new HashSet<WorldInfoEntry>();

        foreach (var group in grouped)
        {
            if (group.Count() <= 1) continue;

            var groupEntries = group.ToList();
            WorldInfoEntry winner;

            if (groupEntries.Any(e => e.PrioritizeInGroup))
            {
                // 优先级模式：选择 InsertionOrder 最高的
                winner = groupEntries.OrderByDescending(e => e.InsertionOrder).First();
            }
            else
            {
                // 权重随机模式
                int totalWeight = groupEntries.Sum(e => e.GroupWeight);
                int randomRoll = Random.Shared.Next(totalWeight);
                int cumulativeWeight = 0;
                winner = groupEntries.Last(); // Fallback
                foreach (var entry in groupEntries)
                {
                    cumulativeWeight += entry.GroupWeight;
                    if (randomRoll < cumulativeWeight)
                    {
                        winner = entry;
                        break;
                    }
                }
            }

            winners.Add(winner);
            foreach (var entry in groupEntries.Where(e => e != winner))
            {
                losers.Add(entry);
            }
        }

        return entries.Except(losers).ToList();
    }

    private static WorldInfoProcessingResult AssembleResults(List<WorldInfoEntry> sortedEntries)
    {
        var result = new WorldInfoProcessingResult();
        var beforeBuilder = new StringBuilder();
        var afterBuilder = new StringBuilder();

        foreach (var entry in sortedEntries)
        {
            string contentWithNewline = entry.Content + "\n";

            switch ((WorldInfoPosition)entry.Position)
            {
                case WorldInfoPosition.BeforeCharDefs:
                    beforeBuilder.Append(contentWithNewline);
                    break;

                case WorldInfoPosition.AfterCharDefs:
                    afterBuilder.Append(contentWithNewline);
                    break;

                // 按照指示，将AN和EM都视为@D⚙️，深度4
                case WorldInfoPosition.TopOfAN:
                case WorldInfoPosition.BottomOfAN:
                case WorldInfoPosition.BeforeExampleMessages:
                case WorldInfoPosition.AfterExampleMessages:
                    result.DepthInjections.Add(new DepthInjectionCommand(entry.Content, 4, PromptRoleType.System,entry.InsertionOrder));
                    break;

                case WorldInfoPosition.DepthBased:
                    var role = entry.MessageRole switch
                    {
                        1 => PromptRoleType.User,
                        2 => PromptRoleType.Assistant,
                        _ => PromptRoleType.System
                    };

                    int depth = entry.InsertionDepth ?? 0;
                    result.DepthInjections.Add(new DepthInjectionCommand(entry.Content, depth, role,entry.InsertionOrder));
                    break;
            }
        }

        result.WorldInfoBefore = beforeBuilder.ToString().TrimEnd();
        result.WorldInfoAfter = afterBuilder.ToString().TrimEnd();

        return result;
    }

    // 为了编译通过，临时添加这个枚举
    private enum SelectiveLogic
    {
        AndAny = 0,
        AndAll = 1,
        NotAny = 2,
        NotAll = 3
    }

    private enum WorldInfoPosition
    {
        BeforeCharDefs = 0,
        AfterCharDefs = 1,
        BeforeExampleMessages = 2,
        AfterExampleMessages = 3,
        TopOfAN = 4,
        BottomOfAN = 5,
        DepthBased = 6
    }
}