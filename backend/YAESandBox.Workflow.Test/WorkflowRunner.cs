using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.AIService.ConfigManagement;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow.Test;

/// <summary>
/// 工作流运行器，负责初始化所有必要的服务和依赖，并执行指定的工作流。
/// </summary>
public class WorkflowRunner
{
    private WorkflowConfigFileService WorkflowConfigFileService { get; }
    private IMasterAiService MasterAiService { get; }
    private IWorkflowDataAccess DataAccess { get; }

    /// <summary>
    /// 构造函数，手动设置所有依赖项。
    /// </summary>
    public WorkflowRunner()
    {
        Console.WriteLine("初始化工作流运行环境...");

        // 1. 设置存储后端
        // 使用你提供的 JsonFileCacheJsonStorage，数据将保存在程序的执行目录下的 "YAESandBoxData" 文件夹中
        string dataRootPath = Path.Combine(AppContext.BaseDirectory,
            "C:\\Users\\Creeper10\\Desktop\\ProjectForFun\\YAESandBox\\backend\\YAESandBox.AppWeb\\Data");
        IGeneralJsonStorage storage = new JsonFileCacheJsonStorage(dataRootPath);
        Console.WriteLine($"[环境] 数据存储路径: {dataRootPath}");

        // 2. 初始化工作流配置服务
        this.WorkflowConfigFileService = new WorkflowConfigFileService(storage);
        Console.WriteLine("[环境] 工作流配置服务已初始化。");

        // 3. 初始化AI配置管理器和AI主服务
        var aiConfigManager = new JsonFileAiConfigurationManager(storage);
        Console.WriteLine("[环境] AI配置管理器已初始化。");

        // 4. 初始化手动 HttpClient 工厂
        var httpClientFactory = new ManualHttpClientFactory();
        Console.WriteLine("[环境] 手动HttpClient工厂已初始化。");

        // 5. 初始化真正的 MasterAiService，并注入依赖
        this.MasterAiService = new MasterAiService(httpClientFactory, aiConfigManager);
        Console.WriteLine("[环境] 真正的MasterAiService已初始化。");

        // 6. 初始化模拟的数据访问层
        this.DataAccess = new MockWorkflowDataAccess();
        Console.WriteLine("[环境] 模拟数据访问层已初始化。");

        Console.WriteLine("工作流运行环境准备就绪。\n");
    }

    /// <summary>
    /// 执行指定ID的工作流。
    /// </summary>
    /// <param name="workflowId">要执行的工作流的ID。</param>
    /// <param name="triggerParams">触发工作流的参数。</param>
    /// <param name="rewTextCallback">生成rawText后的回调函数</param>
    /// <returns>工作流执行结果。</returns>
    public async Task<WorkflowExecutionResult?> RunWorkflowAsync(string workflowId, IReadOnlyDictionary<string, string> triggerParams,
        Action<string> rewTextCallback)
    {
        Console.WriteLine($"--- 开始执行工作流: {workflowId} ---");

        // 1. 加载工作流配置
        Console.WriteLine($"正在从文件加载工作流配置 '{workflowId}.json'...");
        var configResult = await this.WorkflowConfigFileService.FindWorkflowConfig(workflowId);
        if (!configResult.TryGetValue(out var workflowConfig))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"错误：无法加载工作流配置。原因: {configResult.Errors.FirstOrDefault()?.Message}");
            Console.ResetColor();
            return null;
        }

        Console.WriteLine("工作流配置加载成功。");

        // 2. 定义回调函数
        // 在控制台环境中，我们将显示更新请求直接打印到控制台
        // void DisplayUpdateCallback(DisplayUpdateRequestPayload payload)
        // {
        //     Console.ForegroundColor = ConsoleColor.Cyan;
        //     Console.WriteLine($"[显示更新请求] 模式: {payload.UpdateMode}");
        //     Console.WriteLine($"[显示更新请求] 内容: {payload.Content}");
        //     Console.ResetColor();
        // }

        Task DisplayUpdateCallback(DisplayUpdateRequestPayload payload)
        {
            return Task.Run(() =>
            {
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.Write(payload.Content);
                Console.ResetColor();
            });
        }

        var callback = new WorkflowRawTextCallbackTemp(DisplayUpdateCallback, s => Task.Run(() => rewTextCallback(s)));

        // 3. 使用 ToProcessor 扩展方法将配置转换为可执行的 WorkflowProcessor
        Console.WriteLine("正在构建工作流处理器...");
        var workflowProcessor = workflowConfig.ToWorkflowProcessor(
            triggerParams: triggerParams,
            masterAiService: this.MasterAiService,
            dataAccess: this.DataAccess,
            callback: callback
        );
        Console.WriteLine("工作流处理器构建完成。");

        // 4. 执行工作流
        Console.WriteLine(">>> 开始执行工作流步骤...");
        var result = await workflowProcessor.ExecuteWorkflowAsync();
        Console.WriteLine("<<< 工作流步骤执行完毕。");

        // 5. 返回结果
        Console.WriteLine($"--- 工作流执行结束: {workflowId} ---\n");
        return result;
    }
}

/// <summary>
/// IHttpClientFactory 的一个简单手动实现，用于在非依赖注入环境（如控制台应用）中
/// 满足 MasterAiService 的构造函数要求。
/// 它为每个请求都返回一个全新的 HttpClient 实例。
/// </summary>
public class ManualHttpClientFactory : IHttpClientFactory
{
    /// <summary>
    /// 创建一个新的 HttpClient 实例。
    /// 在这个简单的实现中，我们忽略了 'name' 参数。
    /// </summary>
    /// <param name="name">HTTP 客户端的逻辑名称（在此实现中被忽略）。</param>
    /// <returns>一个新的 HttpClient 实例。</returns>
    public HttpClient CreateClient(string name)
    {
        // 每次都返回一个新的实例。在一个真实的高性能应用中，这并不是最佳实践
        // (推荐使用 SocketHttpHandler 的池化)，但对于我们的控制台测试工具来说完全足够了。
        return new HttpClient();
    }
}