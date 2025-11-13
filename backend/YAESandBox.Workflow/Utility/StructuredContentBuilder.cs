using System.Collections.Specialized;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Core.Runtime.WorkflowService.Abstractions;

namespace YAESandBox.Workflow.Utility;

/// <summary>
/// 一个结构化内容构建器，用于构建和操作类似XML的结构化文本。
/// 此版本不使用 System.Xml.Linq，以避免任何自动的XML转义。
/// 所有内容都按原样存储和输出。
/// </summary>
/// <param name="rootElementName"></param>
public partial class StructuredContentBuilder(string rootElementName = "root")
{
    /// <summary>
    /// 所有节点的基类。
    /// </summary>
    private abstract class NodeBase
    {
        internal abstract void Serialize(StringBuilder sb);
    }

    /// <summary>
    /// 代表纯文本内容的节点。
    /// </summary>
    private class TextNode(StringBuilder content) : NodeBase
    {
        internal readonly StringBuilder Content = content;

        internal override void Serialize(StringBuilder sb)
        {
            sb.Append(this.Content);
        }
    }

    /// <summary>
    /// 代表一个元素（标签）的节点，可以包含其他子节点（文本或元素）。
    /// </summary>
    private class ElementNode(string name) : NodeBase
    {
        internal readonly string Name = name;

        internal readonly List<NodeBase> Children = [];

        internal override void Serialize(StringBuilder sb)
        {
            sb.Append('<').Append(this.Name).Append('>');
            foreach (var child in this.Children)
            {
                child.Serialize(sb);
            }

            sb.Append("</").Append(this.Name).Append('>');
        }
    }


    // 内部维护一个XElement作为根节点
    private ElementNode Root { get; } = new(rootElementName);

    /// <summary>
    /// 上层方法，根据模式选择直接设置或解析后设置内容。
    /// </summary>
    /// <param name="path">目标路径。</param>
    /// <param name="content">要设置的原始文本内容，可能包含标签。</param>
    /// <param name="mode">更新模式，决定是替换内容还是附加内容。</param>
    /// <param name="mergeTags">是否启用标签归并和路由功能。</param>
    public void SetContent(string path, string content, UpdateMode mode = UpdateMode.Incremental, bool mergeTags = true)
    {
        if (mergeTags)
        {
            // 启用归并模式，调用解析逻辑
            this.ParseAndRouteContent(path, content, mode);
        }
        else
        {
            // 关闭归并模式，直接调用底层方法（只设置纯文本，不附加）
            this.SetRawContent(path, content, mode);
        }
    }

    /// <summary>
    /// 底层方法，向指定路径的节点直接设置或附加纯文本内容。
    /// </summary>
    public void SetRawContent(string path, string content, UpdateMode mode = UpdateMode.Incremental)
    {
        var targetNode = this.FindOrCreateElementByPath(path);

        if (mode == UpdateMode.FullSnapshot)
        {
            // 快照模式：
            if (string.IsNullOrEmpty(content))
            {
                // 如果新内容为空，则意图是清除所有文本。
                targetNode.Children.RemoveAll(child => child is TextNode);
                return;
            }

            // 寻找第一个文本节点。
            var firstTextNode = targetNode.Children.OfType<TextNode>().FirstOrDefault();

            TextNode primaryTextNode;
            if (firstTextNode is not null)
            {
                // a. 如果找到了，更新其内容。它成为我们的“主”文本节点。
                firstTextNode.Content.Clear().Append(content);
                primaryTextNode = firstTextNode;
            }
            else
            {
                // b. 如果没找到，在列表开头插入一个新的文本节点。
                primaryTextNode = new TextNode(new StringBuilder(content));
                targetNode.Children.Insert(0, primaryTextNode);
            }

            // c. 移除所有其他的文本节点，确保主文本节点是唯一的。
            //    我们使用引用比较 (object.ReferenceEquals) 来确保不会移除我们刚刚更新或创建的节点。
            targetNode.Children.RemoveAll(child =>
                child is TextNode && !ReferenceEquals(child, primaryTextNode));
        }
        else // Incremental
        {
            // 增量模式：如果最后一个子节点是文本，则追加到它。否则，创建一个新的文本节点。
            // 这样可以合并连续的文本流，性能更好。
            if (targetNode.Children.LastOrDefault() is TextNode lastTextNode)
            {
                lastTextNode.Content.Append(content);
            }
            else
            {
                targetNode.Children.Add(new TextNode(new StringBuilder(content)));
            }
        }
    }

    /// <summary>
    /// 解析包含标签的文本，并将内容通过递归调用路由到相应的子路径或主路径。
    /// 这实现了真正的 "拆解和路由" 逻辑。
    /// </summary>
    private void ParseAndRouteContent(string basePath, string contentWithTags, UpdateMode mode = UpdateMode.Incremental)
    {
        // 1. 如果是快照模式，首先清空基础路径的元素。
        //    所有后续的递归调用都将是增量式的。
        if (mode == UpdateMode.FullSnapshot)
        {
            // 快照模式下，清空目标节点的所有内容
            var targetNode = this.FindOrCreateElementByPath(basePath);
            targetNode.Children.Clear();
        }

        var tagContentRegex = TagParseRegex();
        var matches = tagContentRegex.Matches(contentWithTags);
        int lastIndex = 0;

        // 2. 迭代所有匹配的 <tag>content</tag>
        foreach (Match match in matches)
        {
            // a. 处理上一个匹配到当前匹配之间的纯文本
            if (match.Index > lastIndex)
            {
                string plainText = contentWithTags.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrWhiteSpace(plainText))
                {
                    // 纯文本直接追加到当前基础路径
                    this.SetRawContent(basePath, plainText);
                }
            }

            // b. 处理当前匹配到的标签
            string tagName = match.Groups[1].Value;
            string tagValue = match.Groups[2].Value;

            // c. 构造子路径，并递归调用 SetContent 进行路由。
            //    这样，标签内的内容也会被同样地解析和路由。
            //    模式强制为 Incremental，因为 FullSnapshot 已在顶层处理。
            string subPath = string.IsNullOrEmpty(basePath) ? tagName : $"{basePath}.{tagName}";
            this.ParseAndRouteContent(subPath, tagValue);

            // d. 更新索引，跳过已处理的部分
            lastIndex = match.Index + match.Length;
        }

        // 3. 处理最后一个匹配之后剩余的纯文本
        if (lastIndex < contentWithTags.Length)
        {
            string remainingPlainText = contentWithTags.Substring(lastIndex);
            if (!string.IsNullOrWhiteSpace(remainingPlainText))
            {
                // 剩余的纯文本也追加到当前基础路径
                this.SetRawContent(basePath, remainingPlainText);
            }
        }
    }

    /// <summary>
    /// 辅助方法：根据点号分隔的路径查找或创建元素节点。
    /// </summary>
    private ElementNode FindOrCreateElementByPath(string path)
    {
        var current = this.Root;
        if (string.IsNullOrEmpty(path))
        {
            return current;
        }

        foreach (string segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            // 直接在列表中查找，而不是用字典
            var nextElement = current.Children
                .OfType<ElementNode>()
                .FirstOrDefault(e => e.Name == segment);

            if (nextElement is null)
            {
                nextElement = new ElementNode(segment);
                current.Children.Add(nextElement);
            }

            current = nextElement;
        }

        return current;
    }

    /// <summary>
    /// 获取内部内容的字符串表示形式（不包含根元素）。
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        // 序列化根节点的所有子节点
        foreach (var childNode in this.Root.Children)
        {
            childNode.Serialize(sb);
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"<([\p{L}\p{N}_-]+)>(.*?)</\1>", RegexOptions.Singleline)]
    private static partial Regex TagParseRegex();


    /// <summary>
    /// 将内部内容序列化为 JSON 字符串。
    /// </summary>
    /// <returns>JSON 字符串。</returns>
    public string ToJson()
    {
        // ToJson 的逻辑完全委托给 ConvertNodeToObject，从根节点开始
        object representation = this.ConvertNodeToObject(this.Root);
        // 如果根节点本身就是个空对象，我们返回一个空JSON对象字符串
        if (representation is SortedDictionary<string, object> { Count: 0 })
        {
            return "{}";
        }

        return JsonSerializer.Serialize(representation, YaeSandBoxJsonHelper.JsonSerializerOptions);
    }

    /// <summary>
    /// 递归地将一个节点转换为可序列化为 JSON 的对象 (Dictionary, List, or string)。
    /// 这个版本修复了文本节点丢失的问题，并使逻辑更健壮。
    /// </summary>
    private object ConvertNodeToObject(ElementNode element)
    {
        var childElements = element.Children.OfType<ElementNode>().ToList();
        var textNodes = element.Children.OfType<TextNode>().ToList();

        // --- 数组检测逻辑 START ---
        // 检查此节点是否应序列化为 JSON 数组。
        // 条件：1. 必须有子元素。 2. 不能有任何有效的文本节点（即非纯空白）。
        if (childElements.Any() && textNodes.All(tn => string.IsNullOrWhiteSpace(tn.Content.ToString())))
        {
            var indexedChildren = new SortedDictionary<int, ElementNode>();
            bool allChildrenAreIntKeys = true;

            // 尝试将所有子元素名称解析为非负整数索引
            foreach (var child in childElements)
            {
                if (int.TryParse(child.Name, out int index) && index >= 0 && !indexedChildren.ContainsKey(index))
                {
                    indexedChildren.Add(index, child);
                }
                else
                {
                    // 如果任何一个子元素名称不是有效的、唯一的非负整数，则它不能是数组。
                    allChildrenAreIntKeys = false;
                    break;
                }
            }

            // 仅当所有子节点都是有效的整数键时，才继续检查序列
            if (allChildrenAreIntKeys && indexedChildren.Any())
            {
                int firstKey = indexedChildren.Keys.First();

                // 规则：我们只接受从 0 或 1 开始的序列作为数组。
                if (firstKey is 0 or 1)
                {
                    int expectedKey = firstKey;
                    bool isSequential = indexedChildren.Keys.All(key => key == expectedKey++);

                    if (isSequential)
                    {
                        // 确认是数组！按索引顺序递归转换子节点，并返回一个列表。
                        return indexedChildren.Values.Select(this.ConvertNodeToObject).ToList();
                    }
                }
            }
        }
        // --- 数组检测逻辑 END ---

        // 使用 OrderedDictionary 来严格保证键的插入顺序
        var jsonObject = new OrderedDictionary();

        // 1. 遍历有序的子元素列表。
        //    由于我们确认了同一层级不会有同名子元素，逻辑大大简化。
        if (childElements.Any())
        {
            foreach (var childNode in childElements)
            {
                jsonObject[childNode.Name] = this.ConvertNodeToObject(childNode);
            }
        }

        // 2. 处理所有文本内容
        string directText = string.Concat(textNodes.Select(tn => tn.Content.ToString())).Trim();

        // 3. 统一处理混合内容和纯文本内容
        //    如果存在有效的文本内容，就将其添加到 "_text" 键中。
        if (!string.IsNullOrEmpty(directText))
        {
            jsonObject["_text"] = directText;
        }

        // 最终，总是返回一个 OrderedDictionary (JSON 对象)。
        // 即使是空元素 <empty/>，也会返回一个空的 {} 对象，而不是 null 或 string。
        // 这保证了数据契约的稳定性。
        return jsonObject;
    }
}