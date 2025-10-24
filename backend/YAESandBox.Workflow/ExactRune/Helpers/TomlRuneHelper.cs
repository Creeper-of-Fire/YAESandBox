using Tomlyn.Model;
using YAESandBox.Workflow.Core.VarSpec;

namespace YAESandBox.Workflow.ExactRune.Helpers;

/// <summary>
/// 为处理 TOML 的符文提供共享的辅助方法。
/// </summary>
public static class TomlRuneHelper
{
    /// <summary>
    /// 递归地将 TomlObject 转换为适合在 Tuum 中存储的运行时对象。
    /// TomlTable -> Dictionary &lt;string, object&gt;
    /// TomlTableArray -> List&lt;Dictionary&lt;string, object&gt;&gt;
    /// Primitive -> .NET primitive type
    /// </summary>
    public static object ConvertTomlObjectToRuntimeValue(object tomlObject)
    {
        return tomlObject switch
        {
            // 表 -> 字典
            TomlTable table => table.ToDictionary(
                kvp => kvp.Key,
                kvp => ConvertTomlObjectToRuntimeValue(kvp.Value)),

            // 表数组 -> 字典列表
            TomlTableArray tableArray => tableArray
                .Select(ConvertTomlObjectToRuntimeValue)
                .ToList(),

            // 数组 -> 列表
            TomlArray array => array
                .OfType<object>()
                .Select(ConvertTomlObjectToRuntimeValue)
                .ToList(),

            // 其他所有基础类型，Tomlyn 已经为我们转换好了 (long, double, bool, string, etc.)
            _ => tomlObject
        };
    }

    /// <summary>
    /// 递归地将 TomlObject 转换为 VarSpecDef 以进行静态类型分析。
    /// </summary>
    public static VarSpecDef ConvertTomlObjectToVarSpecDef(object tomlObject)
    {
        switch (tomlObject)
        {
            case TomlTable table:
                var properties = table.ToDictionary(
                    kvp => kvp.Key,
                    kvp => ConvertTomlObjectToVarSpecDef(kvp.Value)
                );

                // TOML 表 -> RecordVarSpecDef
                return CoreVarDefs.RecordStringAny with
                {
                    Properties = properties
                };

            case TomlTableArray tableArray:
                var elementDef = tableArray.Count == 0
                    ? CoreVarDefs.Any // 空列表，元素类型未知，设为 Any
                    : ConvertTomlObjectToVarSpecDef(tableArray[0]);

                return CoreVarDefs.AnyList with { ElementDef = elementDef };

            // TOML 基础类型 -> PrimitiveVarSpecDef
            case string: return CoreVarDefs.String;
            case int:
            case long: return CoreVarDefs.Int;
            case float:
            case double: return CoreVarDefs.Float;
            case bool: return CoreVarDefs.Boolean;

            case TomlArray tomlArray:
                VarSpecDef arrayElementDef;
                if (tomlArray.Count == 0)
                {
                    arrayElementDef = CoreVarDefs.Any;
                }
                else
                {
                    // 推断数组的统一类型，这里简化为只看第一个元素
                    arrayElementDef = tomlArray[0] is not { } tomlArrayFirstItem
                        ? CoreVarDefs.Any
                        : ConvertTomlObjectToVarSpecDef(tomlArrayFirstItem);
                }

                return new ListVarSpecDef(
                    $"{arrayElementDef.TypeName}[]", // e.g., "String[]"
                    null,
                    arrayElementDef
                );

            // 其他情况或数组等，可以进一步细化
            default: return CoreVarDefs.Any;
        }
    }
}