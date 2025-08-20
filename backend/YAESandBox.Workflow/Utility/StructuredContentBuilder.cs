using System.Text;
using System.Text.RegularExpressions;
using YAESandBox.Workflow.Core.Abstractions;

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
            // 快照模式：清空所有子节点，然后只添加一个包含新内容的文本节点。
            targetNode.Children.Clear();
            if (!string.IsNullOrEmpty(content))
            {
                targetNode.Children.Add(new TextNode(new StringBuilder(content)));
            }
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
                    this.SetRawContent(basePath, plainText, UpdateMode.Incremental);
                }
            }

            // b. 处理当前匹配到的标签
            string tagName = match.Groups[1].Value;
            string tagValue = match.Groups[2].Value;

            // c. 构造子路径，并递归调用 SetContent 进行路由。
            //    这样，标签内的内容也会被同样地解析和路由。
            //    模式强制为 Incremental，因为 FullSnapshot 已在顶层处理。
            string subPath = string.IsNullOrEmpty(basePath) ? tagName : $"{basePath}.{tagName}";
            this.ParseAndRouteContent(subPath, tagValue, UpdateMode.Incremental);

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
                this.SetRawContent(basePath, remainingPlainText, UpdateMode.Incremental);
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

            if (nextElement == null)
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
}