using YAESandBox.Depend.Interfaces;
using YAESandBox.Workflow.Module;
using static YAESandBox.Workflow.WorkflowProcessor;

namespace YAESandBox.Workflow.Step;

public record StepProcessorConfig
{
    internal async Task<StepProcessor> ToStepProcessorAsync(
        WorkflowConfigService workflowConfigService,
        WorkflowProcessorContent workflowProcessor,
        Dictionary<string, object> stepInput)
    {
        return await StepProcessor.CreateAsync(workflowConfigService, workflowProcessor, this, stepInput);
    }

    /// <summary>
    /// 步骤的AI配置，如果不存在，则这个模块不需要AI处理
    /// </summary>
    public StepAiConfig? StepAiConfig { get; init; }

    /// <summary>
    /// 按顺序执行的模块ID列表。
    /// 这些ID可以是全局模块的ID，也可以是定义在下方 InnerModuleConfig 中的模块的ID。
    /// 允许ID重复，表示同一个模块配置会被执行多次。
    /// StepProcessor 在执行时会严格按照此列表的顺序和ID来查找并执行模块。
    /// </summary>
    public List<string> ModuleIds { get; init; } = [];


    // ==========================================================================================
    // InnerModuleConfig 机制说明
    // ==========================================================================================
    // 1. 目的与场景：
    //    InnerModuleConfig 用于存储那些与此步骤配置紧密相关、不期望被其他步骤直接复用（或独立管理）的模块的完整配置数据。
    //    这些模块通常是为实现该步骤特定逻辑而定制的。
    //
    // 2. 全局唯一ID：
    //    尽管这些模块配置“内联”存储在此步骤配置中，但字典的 key (即模块ID) 仍然是一个全局唯一的UUID字符串。(直接由GUID的生成机制决定，全宇宙唯一)
    //    这确保了即使在步骤内部，每个模块配置也拥有一个明确的身份标识。
    //    这个ID会在模块初次创建时（例如，在步骤编辑器中为此步骤添加一个新“私有”模块时）生成。
    //
    // 3. ModuleIds 的引用：
    //    上面定义的 `ModuleIds` 列表是模块的实际执行顺序。
    //    此列表中的字符串ID，既可以引用在全局模块库中定义的模块，也可以引用在此 `InnerModuleConfig` 字典中定义的模块。
    //    `StepProcessor` 在解析 `ModuleIds` 列表时，需要一个机制来区分这两者：
    //      - 通常，它会先检查当前 `StepProcessorConfig` 的 `InnerModuleConfig` 是否包含该ID。
    //      - 如果包含，则使用此处的内联配置。
    //      - 如果不包含，则通过 `ConfigLocator` 去全局模块库中查找。
    //
    // 4. 编辑与管理：
    //    - 当在步骤编辑器中为此步骤添加或编辑“私有模块”时，其配置数据会直接存储或更新在此 `InnerModuleConfig` 字典中。
    //    - 这些模块的配置通常不应在“全局模块库”视图中直接可见或可编辑。
    //
    // 5. 生命周期：
    //    - `InnerModuleConfig` 中定义的模块配置的生命周期与所属的 `StepProcessorConfig` 绑定。
    //    - 当此步骤配置被删除时，其内部定义的所有模块配置数据也随之消失（硬删除）。
    //    - 当此步骤配置被复制时，`InnerModuleConfig` 及其包含的所有模块配置也应被深拷贝，并且内部模块的新副本应获得全新的UUID，以确保独立性。
    //      （复制操作会为所有层级的配置都生成新UUID，包括这些内部模块的ID和其配置对象本身的新ID）。
    //
    // 6. 与全局模块的转换：
    //    - 可以提供功能将一个内部模块“发布”为全局模块：
    //      1. 复制该内部模块的配置。
    //      2. 为副本生成一个新的全局UUID。
    //      3. 将副本保存到全局模块库。
    //    - 同样，也可以将一个全局模块的配置“复制为私有副本”并内联到此步骤中（此时也会生成新的UUID作为内部模块的ID）。
    //    - 在保留ID不变的情况下，可以进行模块的移动操作（双向的），此时模块的ID不变。
    //
    // 7. 查找优先级：
    //    当 `StepProcessor` 根据 `ModuleIds` 中的一个ID查找模块配置时，通常会优先查找 `InnerModuleConfig`。
    //    这允许一个步骤“优先使用”其内部定义的同ID模块，即使全局库中可能存在一个同ID的模块（尽管UUID的唯一性应使这种情况不太可能发生，除非ID被错误地重用）。
    //    由于我们约定所有ID都是全局唯一的UUID，所以`StepProcessor`查找时：
    //      `config = TryGetFromInner(id) ?? ConfigLocator.FindGlobalModule(id);`
    // ==========================================================================================

    /// <summary>
    /// 内部的私有模块的配置。
    /// key：内部UUID，不过由于UUID的生成原理，它在全局也是唯一的；value：内部模块的配置
    /// </summary>
    public Dictionary<string, IModuleConfig> InnerModuleConfig { get; init; } = [];
}