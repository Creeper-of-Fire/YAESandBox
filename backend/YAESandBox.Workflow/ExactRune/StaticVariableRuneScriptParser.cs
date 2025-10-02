using System.Text;

namespace YAESandBox.Workflow.ExactRune;

/// <summary>
/// 一个简单的手写状态机，用于解析“自用脚本”中的变量定义。
/// 它比复杂的正则表达式更健壮、更易于维护和调试。
/// </summary>
internal static class StaticVariableRuneScriptParser
{
    private enum State
    {
        SeekingName, // 寻找变量名开头
        ParsingName, // 正在解析变量名
        SeekingEquals, // 寻找等号
        SeekingValue, // 寻找值的开头
        InTripleDoubleQuote, // 在 """ ... """ 内部
        InTripleSingleQuote, // 在 ''' ... ''' 内部
        InDoubleQuote, // 在 " ... " 内部
        InSingleQuote, // 在 ' ... ' 内部
        InUnquotedValue, // 正在解析无引号的值
        IgnoringComment // 正在忽略注释行
    }

    public static List<KeyValuePair<string, string>> Parse(string scriptContent)
    {
        var variables = new List<KeyValuePair<string, string>>();
        var currentState = State.SeekingName;
        var nameBuilder = new StringBuilder();
        var valueBuilder = new StringBuilder();
        string? currentName = null;

        for (int i = 0; i < scriptContent.Length; i++)
        {
            char c = scriptContent[i];

            switch (currentState)
            {
                case State.SeekingName:
                    if (char.IsLetter(c) || c == '_')
                    {
                        nameBuilder.Append(c);
                        currentState = State.ParsingName;
                    }
                    else if (c == '#')
                    {
                        currentState = State.IgnoringComment;
                    }

                    // 忽略其他空白字符
                    break;

                case State.ParsingName:
                    if (char.IsLetterOrDigit(c) || c == '_')
                    {
                        nameBuilder.Append(c);
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        currentName = nameBuilder.ToString();
                        nameBuilder.Clear();
                        currentState = State.SeekingEquals;
                    }
                    else if (c == '=')
                    {
                        currentName = nameBuilder.ToString();
                        nameBuilder.Clear();
                        currentState = State.SeekingValue;
                    }
                    // 其他情况视为语法错误，重置状态
                    else
                    {
                        currentState = State.SeekingName;
                    }

                    break;

                case State.SeekingEquals:
                    if (c == '=')
                    {
                        currentState = State.SeekingValue;
                    }
                    else if (!char.IsWhiteSpace(c)) // 遇到非空白也非等号，语法错误
                    {
                        currentState = State.SeekingName;
                        currentName = null;
                    }

                    break;

                case State.SeekingValue:
                    if (c == '"')
                    {
                        if (i + 2 < scriptContent.Length && scriptContent[i + 1] == '"' && scriptContent[i + 2] == '"')
                        {
                            i += 2; // 跳过另外两个引号
                            currentState = State.InTripleDoubleQuote;
                        }
                        else
                        {
                            currentState = State.InDoubleQuote;
                        }
                    }
                    else if (c == '\'')
                    {
                        if (i + 2 < scriptContent.Length && scriptContent[i + 1] == '\'' && scriptContent[i + 2] == '\'')
                        {
                            i += 2; // 跳过另外两个引号
                            currentState = State.InTripleSingleQuote;
                        }
                        else
                        {
                            currentState = State.InSingleQuote;
                        }
                    }
                    else if (!char.IsWhiteSpace(c))
                    {
                        valueBuilder.Append(c);
                        currentState = State.InUnquotedValue;
                    }

                    break;

                case State.InTripleDoubleQuote:
                    if (c == '"' && i + 2 < scriptContent.Length && scriptContent[i + 1] == '"' && scriptContent[i + 2] == '"')
                    {
                        i += 2;
                        FinalizeVariable(variables, ref currentName, valueBuilder, out currentState);
                    }
                    else
                    {
                        valueBuilder.Append(c);
                    }

                    break;

                case State.InTripleSingleQuote:
                    if (c == '\'' && i + 2 < scriptContent.Length && scriptContent[i + 1] == '\'' && scriptContent[i + 2] == '\'')
                    {
                        i += 2;
                        FinalizeVariable(variables, ref currentName, valueBuilder, out currentState);
                    }
                    else
                    {
                        valueBuilder.Append(c);
                    }

                    break;

                case State.InDoubleQuote:
                    if (c == '\\' && i + 1 < scriptContent.Length)
                    {
                        valueBuilder.Append(scriptContent[++i]); // 添加被转义的字符
                    }
                    else if (c == '"')
                    {
                        FinalizeVariable(variables, ref currentName, valueBuilder, out currentState);
                    }
                    else
                    {
                        valueBuilder.Append(c);
                    }

                    break;

                case State.InSingleQuote:
                    if (c == '\\' && i + 1 < scriptContent.Length)
                    {
                        valueBuilder.Append(scriptContent[++i]); // 添加被转义的字符
                    }
                    else if (c == '\'')
                    {
                        FinalizeVariable(variables, ref currentName, valueBuilder, out currentState);
                    }
                    else
                    {
                        valueBuilder.Append(c);
                    }

                    break;

                case State.InUnquotedValue:
                    if (c == '\n')
                    {
                        FinalizeVariable(variables, ref currentName, valueBuilder, out currentState);
                    }
                    else
                    {
                        valueBuilder.Append(c);
                    }

                    break;

                case State.IgnoringComment:
                    if (c == '\n')
                    {
                        currentState = State.SeekingName;
                    }

                    break;
            }
        }

        // 处理文件末尾没有换行符的无引号值
        if (currentState == State.InUnquotedValue)
        {
            FinalizeVariable(variables, ref currentName, valueBuilder, out currentState);
        }

        return variables;
    }

    private static void FinalizeVariable(List<KeyValuePair<string, string>> variables, ref string? currentName, StringBuilder valueBuilder,
        out State currentState)
    {
        if (currentName != null)
        {
            variables.Add(new KeyValuePair<string, string>(currentName, valueBuilder.ToString().TrimEnd()));
        }

        currentName = null;
        valueBuilder.Clear();
        currentState = State.SeekingName;
    }
}