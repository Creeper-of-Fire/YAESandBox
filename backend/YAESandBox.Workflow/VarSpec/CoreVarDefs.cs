namespace YAESandBox.Workflow.VarSpec;

/// <summary>
/// 提供工作流系统中所有核心（内置）变量类型的静态定义。
/// 这些定义是整个类型系统的基础词汇，可被所有符文和模块重用。
/// 使用这些静态属性可以确保类型名称和描述在整个系统中的一致性。
/// </summary>
public static class CoreVarDefs
{
    // --- 基础类型 (Primitives) ---

    /// <summary>
    /// **任意类型 (Any)**: 一个特殊的、动态的类型，可以代表任何数据。
    /// <para>它在类型校验中扮演“万能牌”的角色，可以与任何其他类型互相转换。</para>
    /// <para>主要用于需要极高灵活性的场景，如未经配置的脚本输入/输出，或处理未知结构的JSON数据。</para>
    /// <para><b>注意</b>: 过度使用 `Any` 会削弱静态类型检查带来的好处，应谨慎使用。</para>
    /// </summary>
    public static PrimitiveVarSpecDef Any { get; } = new("Any",
        "任意类型：一个特殊的动态类型，可以代表任何数据，用于需要高度灵活性的场景。");

    /// <summary>
    /// **字符串 (String)**: 代表一段文本数据。
    /// <para>这是工作流中最常用的类型之一，用于处理用户输入、文件名、API响应、提示词模板等。</para>
    /// </summary>
    public static PrimitiveVarSpecDef String { get; } = new("String",
        "字符串：代表一段文本数据，是工作流中最常用的类型。");

    /// <summary>
    /// **整数 (Int)**: 代表一个没有小数部分的整数。
    /// <para>内部通常由 64 位整数 (long) 支持，用于精确计数、ID、索引等场景。</para>
    /// </summary>
    public static PrimitiveVarSpecDef Int { get; } = new("Int",
        "整数：代表一个没有小数部分的整数，用于精确计数、ID等。");

    /// <summary>
    /// **浮点数 (Float)**: 代表一个可能包含小数部分的数字。
    /// <para>内部通常由 64 位双精度浮点数 (double) 支持，用于需要小数精度或科学计算的场景。</para>
    /// </summary>
    public static PrimitiveVarSpecDef Float { get; } = new("Float",
        "浮点数：代表一个可能包含小数部分的数字，用于科学计算等。");

    /// <summary>
    /// **布尔值 (Boolean)**: 代表一个逻辑上的真 (true) 或假 (false)。
    /// <para>常用于条件判断、流程控制、状态标记等。</para>
    /// </summary>
    public static PrimitiveVarSpecDef Boolean { get; } = new("Boolean",
        "布尔值：代表逻辑上的真或假，用于条件判断和流程控制。");

    // --- 集合类型 (Collections) ---

    /// <summary>
    /// **字符串数组 (String[])**: 一个只包含字符串元素的有序列表。
    /// <para>用于处理分行文本、标签列表、文件名集合等。</para>
    /// </summary>
    public static ListVarSpecDef StringList { get; } = new("String[]",
        "字符串数组：一个只包含字符串元素的有序列表。", String);

    /// <summary>
    /// **整数数组 (Int[])**: 一个只包含整数元素的有序列表。
    /// </summary>
    public static ListVarSpecDef IntList { get; } = new("Int[]",
        "整数数组：一个只包含整数元素的有序列表。", Int);

    /// <summary>
    /// **浮点数数组 (Float[])**: 一个只包含浮点数元素的有序列表。
    /// </summary>
    public static ListVarSpecDef FloatList { get; } = new("Float[]",
        "浮点数数组：一个只包含浮点数元素的有序列表。", Float);

    /// <summary>
    /// **任意类型数组 (Any[])**: 一个可以包含任何类型元素的有序列表。
    /// <para>用于处理混合类型的集合，灵活性高，但类型安全性较低。</para>
    /// </summary>
    public static ListVarSpecDef AnyList { get; } = new("Any[]",
        "任意类型数组：一个可以包含任何类型元素的有序列表。", Any);

    /// <summary>
    /// **记录/字典 (Record &lt;String, Any&gt;)**: 一个键为字符串，值为任意类型的键值对集合。
    /// <para>非常适合表示半结构化的数据对象，例如从JSON解析来的数据或需要动态添加属性的实体。</para>
    /// <para>这是表示通用“对象”的首选方式。</para>
    /// </summary>
    public static RecordVarSpecDef RecordStringAny { get; } = new("Record<String, Any>",
        "记录/字典：一个键为字符串，值为任意类型的键值对集合。", new Dictionary<string, VarSpecDef>());

    // --- 特殊业务类型 (Specialized Business Types) ---

    /// <summary>
    /// **JSON字符串 (JsonString)**: 一个内容为合法JSON格式的字符串。
    /// <para>这个类型可以为UI提供特殊提示，例如使用专门的JSON编辑器或高亮显示。</para>
    /// <para>它在语义上比普通 `String` 更具体。</para>
    /// </summary>
    public static PrimitiveVarSpecDef JsonString { get; } = new("JsonString",
        "JSON字符串：一个内容为合法JSON格式的字符串，可以享受特殊的UI渲染。");

    /// <summary>
    /// **图片数据 (Image)**: 代表图像数据。
    /// <para>通常实现为Base64编码的字符串或一个包含URL和元数据的对象。</para>
    /// <para>这个类型可以用于UI预览图片，或作为多模态AI模型的输入。</para>
    /// </summary>
    public static PrimitiveVarSpecDef Image { get; } = new("Image",
        "图片数据：代表一张图片，通常用于UI预览或多模态AI输入。");

    /// <summary>
    /// **提示词 (Prompt)**: 单个带角色的提示词。
    /// <para>这个对象通常包含角色(role)和内容(content)等信息。</para>
    /// <para>它是连接提示词生成符文和AI调用符文关键类型。</para>
    /// </summary>
    public static RecordVarSpecDef Prompt { get; } = new(
        "RoledPromptDto",
        "单个带角色的提示词",
        new Dictionary<string, VarSpecDef>
        {
            { "Role", String },
            { "Content", String },
            { "Name", String }
        });

    /// <summary>
    /// **提示词列表 (PromptList)**: 一个结构化的、用于与AI模型交互的提示词对象列表。
    /// <para>这是一个自定义的业务类型，通常代表 `List&lt;RoledPromptDto&gt;`，包含了角色(role)和内容(content)等信息。</para>
    /// <para>它是连接提示词生成符文和AI调用符文的关键类型。</para>
    /// </summary>
    public static ListVarSpecDef PromptList { get; } = new("PromptList",
        "提示词列表：一个结构化的、用于与AI模型交互的提示词对象列表。", Prompt);
}