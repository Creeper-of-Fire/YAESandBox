using System.Diagnostics;
using System.Text;

namespace YAESandBox.Plugin.Nodejs.CompilerHost;

public class CompilerService
{
    private readonly string _compilerPath;

    public CompilerService()
    {
        // 确定编译器可执行文件的路径
        // AppContext.BaseDirectory 指向你的应用运行的根目录
        // 我们假设构建过程会把 compiler.exe 复制到 'tools' 文件夹下
        var executableName = OperatingSystem.IsWindows() ? "compiler.exe" : "compiler";
        _compilerPath = Path.Combine(AppContext.BaseDirectory, "tools", executableName);

        if (!File.Exists(_compilerPath))
        {
            // 在生产环境中，这里应该使用更健壮的日志记录
            throw new FileNotFoundException($"Compiler executable not found at: {_compilerPath}");
        }
    }

    public async Task<string> CompileAsync(string sourceCode, string fileExtension = ".vue")
    {
        var inputTempFile = Path.GetTempFileName() + fileExtension;
        var outputTempFile = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(inputTempFile, sourceCode);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = _compilerPath,
                Arguments = $"--input \"{inputTempFile}\" --output \"{outputTempFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_compilerPath) // 设置工作目录很重要！
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start compiler process.");
            }

            // 异步等待进程退出，并读取输出
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var errorOutput = await process.StandardError.ReadToEndAsync();
                throw new Exception($"Compilation failed with exit code {process.ExitCode}: {errorOutput}");
            }

            return await File.ReadAllTextAsync(outputTempFile);
        }
        finally
        {
            // 确保临时文件总是被删除
            if (File.Exists(inputTempFile)) File.Delete(inputTempFile);
            if (File.Exists(outputTempFile)) File.Delete(outputTempFile);
        }
    }
}