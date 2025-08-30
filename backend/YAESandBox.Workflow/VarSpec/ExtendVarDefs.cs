namespace YAESandBox.Workflow.VarSpec;

/// <summary>
/// 一个简单的数据载体，用于传递事物的信息。
/// </summary>
/// <param name="Name">事物的名称。</param>
/// <param name="Description">事物的描述。</param>
public record ThingInfo(string Name, string Description);

/// <summary>
/// 拓展变量定义
/// </summary>
public static class ExtendVarDefs
{
    /// <summary>
    /// **事物信息 (ThingInfo)**: 代表一个有名字和描述的事物，比如一个物品或者一个角色。
    /// </summary>
    public static VarSpecDef ThingInfo { get; } = new("ThingInfo", 
        "事物信息：代表一个有名字和描述的事物，比如一个物品或者一个角色。");
    
    /// <summary>
    /// **酒馆世界书列表 (SillyTavernWorldInfoJsonList)**: 代表酒馆世界书的json字符串组成的列表
    /// </summary>
    public static VarSpecDef SillyTavernWorldInfoJsonList { get; }= new("SillyTavernWorldInfoJsonList", 
        "酒馆世界书列表: 代表酒馆世界书的json字符串组成的列表");
}