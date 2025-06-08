// --- File: Program.cs ---

using YAESandBox.Workflow.Test;

// 欢迎信息
Console.WriteLine("======================================");
Console.WriteLine("  YAESandBox 工作流控制台运行器");
Console.WriteLine("======================================");
Console.WriteLine("本程序用于在没有Web环境的情况下测试和执行工作流。");
Console.WriteLine("请确保你已经通过API端点创建了所需的工作流、步骤和AI配置。");
Console.WriteLine("配置文件将从执行目录下的 'YAESandBoxData' 文件夹中读取。\n");

// 创建运行器实例
var runner = new WorkflowRunner();

// 主循环，允许用户反复测试
while (true)
{
    // 获取用户输入
    Console.Write("请输入要执行的全局工作流ID (或输入 'exit' 退出): ");
    string? workflowId = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(workflowId) || workflowId.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("程序退出。");
        break;
    }

    // 在这里可以定义一些触发参数，现在为空
    var triggerParameters = new Dictionary<string, string>
    {
        { "playerName", "阿尔冯斯" },
        { "location", "迷雾森林" }
    };

    Console.WriteLine($"已准备触发参数: playerName='{triggerParameters["playerName"]}', location='{triggerParameters["location"]}'");

    try
    {
        // 调用运行器执行工作流
        var result = await runner.RunWorkflowAsync(workflowId, triggerParameters, Console.WriteLine);

        // 打印结果
        if (result != null)
        {
            Console.WriteLine("---------- 执行结果摘要 ----------");
            Console.ForegroundColor = result.IsSuccess ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"执行状态: {(result.IsSuccess ? "成功" : "失败")}");
            Console.ResetColor();

            if (!result.IsSuccess)
            {
                Console.WriteLine($"错误信息: {result.ErrorMessage}");
            }

            Console.WriteLine("\n[最终生成文本 (RawText)]:");
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.ResetColor();

            // Console.WriteLine($"\n[生成的原子操作数]: {result.Operations.Count}");
            // 你可以在这里遍历并打印 result.Operations 的详细信息

            Console.WriteLine("---------------------------------\n");
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine($"\n!!!!!! 发生未处理的异常 !!!!!!");
        Console.WriteLine(ex.ToString());
        Console.ResetColor();
        Console.WriteLine("\n");
    }
}