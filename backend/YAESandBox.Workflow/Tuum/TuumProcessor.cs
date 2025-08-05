using System.Text.Json;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Rune;
using static YAESandBox.Workflow.Core.WorkflowProcessor;

namespace YAESandBox.Workflow.Tuum;

//tuum的信息：
// 使用的脚本符文们的UUID（注意，脚本符文本身就是绑定在祝祷上的，如果需要把符文复制到更广的地方，可以考虑直接复制祝祷之类的）
/// <summary>
/// 祝祷配置的运行时
/// </summary>
public class TuumProcessor(
    WorkflowRuntimeService workflowRuntimeService,
    TuumConfig config)
    : IProcessorWithDebugDto<ITuumProcessorDebugDto>
{
    private TuumConfig Config { get; } = config;
    internal TuumProcessorContent TuumContent { get; } = new(config, workflowRuntimeService);

    /// <summary>
    /// 祝祷运行时的上下文
    /// </summary>
    public class TuumProcessorContent(TuumConfig tuumConfig, WorkflowRuntimeService workflowRuntimeService)
    {
        /// <summary>
        /// 祝祷的内部变量池
        /// </summary>
        public Dictionary<string, object?> TuumVariable { get; } = [];

        /// <summary>
        /// 得到祝祷的变量
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public object? GetTuumVar(string name)
        {
            return this.TuumVariable.GetValueOrDefault(name);
        }

        /// <summary>
        /// 设置祝祷的变量
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetTuumVar(string name, object? value)
        {
            this.TuumVariable[name] = value;
        }

        /// <summary>
        /// 祝祷的配置
        /// </summary>
        public TuumConfig TuumConfig { get; } = tuumConfig;

        /// <summary>
        /// 工作流的运行时服务
        /// </summary>
        public WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;
        
        /// <summary>
        /// 获得祝祷的变量，带有类型转换，并且有序列化尝试
        /// </summary>
        /// <param name="valueName"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidCastException"></exception>
        public T? GetTuumVar<T>(string valueName)
        {
            object? tryGetValue = this.GetTuumVar(valueName);
            if (tryGetValue is null || tryGetValue.Equals(default(T)))
                return default;

            if (tryGetValue is T promptsValue)
                return promptsValue;

            try
            {
                // 将 C# 对象序列化成 JSON 字符串
                string json = JsonSerializer.Serialize(tryGetValue);

                var result = JsonSerializer.Deserialize<T>(json, YaeSandBoxJsonHelper.JsonSerializerOptions);

                if (result is null || result.Equals(default(T)))
                    return default;

                // 为了后续访问效率，可以将转换后的结果写回 TuumVariable
                this.SetTuumVar(valueName, result);

                return result;
            }
            catch (Exception ex)
            {
                return default;
                // 提供详细的错误信息
                // TODO 应该记录在Tuum的Debug里面
                
                // throw new InvalidCastException(
                    // $"无法将值'{valueName}'(类型: {tryGetValue.GetType().FullName})转换为 {typeof(T).FullName}。JSON 转换失败: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// 消费者（Consumes）：此祝祷需要从全局变量池中获取的所有变量的【全局名称】。
    /// 在严格模式下，这个集合就是 InputMappings 的所有 Value。
    /// </summary>
    internal IEnumerable<string> GlobalConsumers { get; } = config.InputMappings.Values;

    /// <summary>
    /// 生产者（Produces）：此祝祷通过 OutputMappings 向全局变量池声明输出的变量。
    /// </summary>
    internal IEnumerable<string> GlobalProducers { get; } = config.OutputMappings.Keys;

    private List<IProcessorWithDebugDto<IRuneProcessorDebugDto>> Runes { get; } =
        config.Runes.Select(rune => rune.ToRuneProcessor(workflowRuntimeService)).ToList();
    
    internal WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;

    /// <summary>
    /// 启动祝祷流程
    /// </summary>
    /// <param name="workflowRuntimeContext"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Result<Dictionary<string, object?>>> ExecuteTuumsAsync(
        WorkflowRuntimeContext workflowRuntimeContext, CancellationToken cancellationToken = default)
    {
        // 严格根据 InputMappings 从全局变量池填充祝祷的内部变量池
        foreach ((string localName, string globalName) in this.Config.InputMappings)
        {
            if (!workflowRuntimeContext.GlobalVariables.TryGetValue(globalName, out object? value))
            {
                // 这一层校验理论上在静态分析时已完成，但在这里，“直接获取然后失败时抛错误”和“失败时返回Result”是一样的。
                return NormalError.Conflict($"执行祝祷 '{this.Config.ConfigId}' 失败：找不到必需的全局输入变量 '{globalName}'。");
            }

            this.TuumContent.TuumVariable[localName] = value;
        }

        // this.TuumContent.TuumVariable[nameof(WorkflowRuntimeContext.FinalRawText)] = workflowRuntimeContext.FinalRawText;
        // this.TuumContent.TuumVariable[nameof(WorkflowRuntimeContext.GeneratedOperations)] = workflowRuntimeContext.GeneratedOperations;

        foreach (var rune in this.Runes)
        {
            switch (rune)
            {
                case INormalRune normalRune:
                    var result = await normalRune.ExecuteAsync(this.TuumContent, cancellationToken);
                    if (result.TryGetError(out var error))
                        return error;
                    break;
            }
        }

        var tuumOutput = new Dictionary<string, object?>();

        // if (this.TuumContent.TuumVariable.TryGetValue(nameof(WorkflowRuntimeContext.FinalRawText), out object? finalRawText))
        // {
        //     workflowRuntimeContext.FinalRawText = (string)finalRawText;
        // }
        //
        // if (this.TuumContent.TuumVariable.TryGetValue(nameof(WorkflowRuntimeContext.GeneratedOperations), out object? generatedOperations))
        // {
        //     workflowRuntimeContext.GeneratedOperations = (List<AtomicOperation>)generatedOperations;
        // }

        foreach ((string globalName, string localName) in this.Config.OutputMappings)
        {
            // 从本祝祷的内部变量池中查找由符文产生的局部变量
            if (this.TuumContent.TuumVariable.TryGetValue(localName, out object? localValue))
            {
                tuumOutput[globalName] = localValue;
            }
            // else 
            // {
            //   可选：在这里可以处理映射声明了，但符文实际并未产生输出的情况
            //   例如：记录一个警告日志，或者根据严格模式抛出异常
            //   根据“后端不验证”的原则，我们暂时忽略这种情况
            // }
        }

        return tuumOutput;
    }


    /// <inheritdoc />
    public ITuumProcessorDebugDto DebugDto => new TuumProcessorDebugDto
        { RuneProcessorDebugDtos = this.Runes.ConvertAll(it => it.DebugDto) };

    /// <inheritdoc />
    internal record TuumProcessorDebugDto : ITuumProcessorDebugDto
    {
        /// <inheritdoc />
        public required IList<IRuneProcessorDebugDto> RuneProcessorDebugDtos { get; init; }
    }
}