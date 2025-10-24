using System.Text.RegularExpressions;

namespace YAESandBox.Workflow.Utility;

/// <summary>
/// TOML 预处理工具类
/// </summary>
public static partial class TOMLHelper
{
    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex ValidBareKeyRegex();

    /// <summary>
    /// 预处理 TOML 字符串，自动为不符合规范的裸键（如包含中文字符的键）添加引号。
    /// 这个版本更健壮，能处理行内注释等情况。
    /// </summary>
    /// <param name="tomlString">原始的 TOML 字符串。</param>
    /// <returns>修复后、符合 TOML 规范的字符串。</returns>
    public static string PreprocessUnquotedKeys(string tomlString)
    {
        // 用于匹配有效 TOML 裸键的正则表达式。裸键只能包含 ASCII 字母、数字、下划线和破折号。
        var validBareKeyRegex = ValidBareKeyRegex();

        var processedLines = new List<string>();
        string[] lines = tomlString.Split(["\r\n", "\n"], StringSplitOptions.None);

        foreach (string line in lines)
        {
            // 1. 分离内容和行内注释
            string content = line;
            string comment = "";
            int commentIndex = line.IndexOf('#');
            // 确保 '#' 不在字符串内部（这是一个简化处理，对于复杂情况可能不完美，但对大多数AI输出有效）
            if (commentIndex != -1 && !IsInsideQuotes(line, commentIndex))
            {
                content = line.Substring(0, commentIndex);
                comment = line.Substring(commentIndex);
            }

            string trimmedContent = content.Trim();

            // 如果内容部分为空，则该行要么是空行要么是纯注释行，直接保留
            if (string.IsNullOrWhiteSpace(trimmedContent))
            {
                processedLines.Add(line);
                continue;
            }

            // 2. 处理表头, e.g., [character.莉莉]
            if (trimmedContent.StartsWith('[') && trimmedContent.EndsWith(']'))
            {
                bool isArrayOfTables = trimmedContent.StartsWith("[[", StringComparison.Ordinal) &&
                                       trimmedContent.EndsWith("]]", StringComparison.Ordinal);
                int startIndex = isArrayOfTables ? 2 : 1;
                int length = trimmedContent.Length - (isArrayOfTables ? 4 : 2);
                string keyPath = trimmedContent.Substring(startIndex, length).Trim();

                string fixedKeyPath = FixDottedKey(keyPath, validBareKeyRegex);

                if (keyPath != fixedKeyPath)
                {
                    string brackets = isArrayOfTables ? "[[" : "[";
                    string endBrackets = isArrayOfTables ? "]]" : "]";
                    // 重组行：修复后的内容 + 前导/后导空格 + 注释
                    processedLines.Add(ReconstructLine(content, $"{brackets}{fixedKeyPath}{endBrackets}", comment));
                }
                else
                {
                    processedLines.Add(line); // 无需修改
                }

                continue;
            }

            // 3. 处理键值对, e.g., 中文键 = "value"
            int equalsIndex = trimmedContent.IndexOf('=');
            if (equalsIndex > 0)
            {
                string key = trimmedContent.Substring(0, equalsIndex).Trim();
                if (!string.IsNullOrEmpty(key))
                {
                    // 使用 FixDottedKey 来处理可能存在的点分隔键
                    string fixedKey = FixDottedKey(key, validBareKeyRegex);

                    if (key != fixedKey)
                    {
                        string valuePart = trimmedContent.Substring(equalsIndex); // 包含 '='
                        processedLines.Add(ReconstructLine(content, $"{fixedKey} {valuePart.TrimStart()}", comment));
                    }
                    else
                    {
                        processedLines.Add(line); // 无需修改
                    }

                    continue;
                }
            }

            processedLines.Add(line); // 如果不匹配任何规则，原样保留
        }

        return string.Join("\n", processedLines);
    }

    // 辅助函数：修复点分隔键
    private static string FixDottedKey(string dottedKey, Regex validBareKeyRegex)
    {
        string[] parts = dottedKey.Split('.');
        var processedParts = new List<string>();
        foreach (string part in parts)
        {
            string trimmedPart = part.Trim();
            if (string.IsNullOrEmpty(trimmedPart)) continue;

            if (!validBareKeyRegex.IsMatch(trimmedPart) && !IsQuoted(trimmedPart))
            {
                processedParts.Add($"\"{trimmedPart}\"");
            }
            else
            {
                processedParts.Add(part);
            }
        }

        return string.Join(".", processedParts);
    }

    // 辅助函数：检查字符是否在引号内（简化版）
    private static bool IsInsideQuotes(string line, int index)
    {
        int quoteCount = 0;
        for (int i = 0; i < index; i++)
        {
            if (line[i] == '"') // 简化：不处理转义的引号
            {
                quoteCount++;
            }
        }

        return quoteCount % 2 != 0;
    }

    // 辅助函数：检查键是否已被引号包裹
    private static bool IsQuoted(string keyPart)
    {
        keyPart = keyPart.Trim();
        return (keyPart.StartsWith('"') && keyPart.EndsWith('"')) ||
               (keyPart.StartsWith('\'') && keyPart.EndsWith('\''));
    }

    // 辅助函数：重新组合行，保留原始的缩进和注释
    private static string ReconstructLine(string originalContent, string newContent, string comment)
    {
        int leadingWhitespace = originalContent.TakeWhile(char.IsWhiteSpace).Count();
        return new string(' ', leadingWhitespace) + newContent + comment;
    }
}