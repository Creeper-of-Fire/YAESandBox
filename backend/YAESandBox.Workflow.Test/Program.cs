// See https://aka.ms/new-console-template for more information

using System.Text;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.AIService.AiConfig;
using YAESandBox.Workflow.AIService.AiConfig.Doubao;

namespace YAESandBox.Workflow.Test;

internal class Program
{
    // 使用你的实际 API Key
    private const string DOUBAO_API_KEY = "fe18c009-4586-4f73-b0f0-a905f0f48286"; // 请替换为你的有效API Key
    private const string DOUBAO_MODEL_NAME = "doubao-1-5-lite-32k-250115"; // 或者其他你测试的模型
    private const double DEFAULT_TEMPERATURE = 0.7;

    internal static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8; //确保控制台能正确显示中文等字符

        // 1. 创建 HttpClient 实例
        using var httpClient = new HttpClient();

        // 2. 准备 AiProcessorDependencies
        var dependencies = new AiProcessorDependencies(httpClient);

        // 3. 准备 AiRequestParameters
        var requestParameters = new DoubaoAiProcessorConfig(DOUBAO_API_KEY, DOUBAO_MODEL_NAME)
        {
            Temperature = DEFAULT_TEMPERATURE
        };

        // 4. 手动创建 DoubaoAiProcessor 实例
        IAiProcessor doubaoProcessor = new DoubaoAiProcessor(dependencies, requestParameters);

        Console.WriteLine($"正在使用模型: {DOUBAO_MODEL_NAME}");
        Console.WriteLine("请输入你的问题 (输入 'quit' 退出, 输入 'clear' 清空历史记录):"); // 新增 'clear' 命令

        // 初始化聊天历史记录列表
        // 我们将存储 (角色, 内容) 的元组
        var chatHistory = new List<RoledPromptDto>();

        while (true)
        {
            Console.Write("> ");
            string? userInput = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(userInput))
            {
                continue;
            }

            if (userInput.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (userInput.Equals("clear", StringComparison.OrdinalIgnoreCase)) // 新增清空历史记录的命令
            {
                chatHistory.Clear();
                Console.WriteLine("--- 聊天历史已清空 ---");
                Console.WriteLine("\n------------------------------------");
                continue;
            }

            // 构建本次请求的 prompts
            // 使用 C# 12+ 集合表达式可以使这里更简洁
            var prompts = new List<RoledPromptDto>
            {
                RoledPromptDto.System("你是一个乐于助人的AI助手。") // 系统提示始终放在最前面
            };

            // 添加历史记录到 prompts
            // 使用 LINQ 的 Select 转换历史记录项
            prompts.AddRange(chatHistory.ToList());

            // 添加当前用户输入
            prompts.Add(RoledPromptDto.User(userInput,"小明"));


            Console.WriteLine("\nAI 正在回复 (流式):");
            var accumulatedResponse = new StringBuilder();
            using var cts = new CancellationTokenSource();

            // 可选：设置超时
            // cts.CancelAfter(TimeSpan.FromSeconds(60)); // 例如60秒超时

            try
            {
                var result = await doubaoProcessor.StreamRequestAsync(
                    prompts,
                    chunk =>
                    {
                        Console.Write(chunk); // 直接打印流式块
                        accumulatedResponse.Append(chunk);
                    },
                    cts.Token
                );

                Console.WriteLine(); // 换行

                if (result.IsSuccess)
                {
                    Console.WriteLine("--- 流式传输成功完成 ---");

                    // 将当前的用户输入和AI的完整回复添加到历史记录中
                    chatHistory.Add(RoledPromptDto.User(userInput,"小明"));
                    chatHistory.Add(RoledPromptDto.Assistant(accumulatedResponse.ToString()));

                    // 可选: 限制历史记录的长度，例如只保留最近 N 轮对话
                    // int maxHistoryItems = 10; // 保留最近5轮对话 (用户+AI = 2项/轮)
                    // if (chatHistory.Count > maxHistoryItems)
                    // {
                    //     chatHistory.RemoveRange(0, chatHistory.Count - maxHistoryItems);
                    // }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"--- AI 请求失败 ---");
                    if (result.IsFailed)
                    {
                        // 假设 result.Errors 至少有一个错误
                        Console.WriteLine($"错误: {result.Errors[0].Message}");
                    }

                    Console.ResetColor();
                    // 请求失败时，不应将用户输入或部分回复计入历史
                }
            }
            catch (OperationCanceledException)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n--- 请求被取消 (可能超时) ---");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"\n--- 发生意外错误 --- \n{ex}");
                Console.ResetColor();
            }
            finally
            {
                // accumulatedResponse.Clear(); // StringBuilder 在每次循环开始时重新创建，这里不需要显式清空
                Console.WriteLine("\n------------------------------------");
            }
        }

        Console.WriteLine("测试程序结束。");
    }
}