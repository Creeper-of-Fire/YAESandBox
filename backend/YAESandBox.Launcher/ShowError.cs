namespace YAESandBox.Launcher;

internal static class Logger
{
    internal static void ShowError(string message, string? logDir = null)
    {
        string finalMessage = message;
        if (logDir != null)
        {
            string logFilePath = Path.Combine(logDir, "launcher-error.log");
            try
            {
                File.WriteAllText(logFilePath, $"{DateTime.Now}: {message}");
                finalMessage += $"\n\n详细信息已记录到：{logFilePath}";
            }
            catch { /* 忽略写入日志的错误 */ }
        }
        
        // 在 Windows 上，我们可以尝试使用 MessageBox
        // 为此，需要在 .csproj 中设置 <OutputType>WinExe</OutputType>
        // MessageBox(IntPtr.Zero, finalMessage, "YAE SandBox 启动错误", 0x10 /* MB_ICONERROR */);
        
        // 对于控制台应用，这是备选方案
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("================ 错误 ================");
        Console.WriteLine(finalMessage);
        Console.WriteLine("======================================");
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }
    
    /// <summary>
    /// 一个独立的、可重用的方法，用于从流中读取所有行并写入日志。
    /// </summary>
    /// <param name="streamReader">要读取的流</param>
    /// <param name="logWriter">要写入的目标</param>
    /// <param name="prefix">每行日志的前缀</param>
    internal static async Task ReadStreamAsync(StreamReader streamReader, StreamWriter logWriter, string prefix)
    {
        // 循环读取，直到流的末尾 (ReadLineAsync 返回 null)
        while (await streamReader.ReadLineAsync() is { } line)
        {
            // 使用 lock 确保线程安全写入
            lock (logWriter)
            {
                logWriter.WriteLine($"{prefix} {line}");
            }
        }
    }
}