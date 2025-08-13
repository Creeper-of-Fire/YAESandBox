using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using YAESandBox.Workflow.Core.Abstractions;

namespace YAESandBox.Workflow.Utility;

/// <summary>
/// 一个结构化XML内容构建器，用于构建和操作结构化内容。
/// 它不是一个服务，不支持并发情况。
/// </summary>
/// <param name="rootElementName"></param>
public partial class StructuredContentBuilder(string rootElementName = "root")
{
    // 内部维护一个XElement作为根节点
    private XElement Root { get; } = new(rootElementName);

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
    /// 底层方法，向指定路径的元素直接设置或附加纯文本内容。
    /// </summary>
    /// <param name="path">点号分隔的XML路径，例如 "thought.plan.step1"。</param>
    /// <param name="content">要设置的文本内容。</param>
    /// <param name="mode">更新模式，决定是替换内容还是附加内容。</param>
    public void SetRawContent(string path, string content, UpdateMode mode = UpdateMode.Incremental)
    {
        // 1. 确定目标元素
        XElement targetElement = this.FindOrCreateElementByPath(path);
        // 2. 更新内容
        if (mode == UpdateMode.FullSnapshot)
        {
            // 替换模式：直接设置元素的值
            targetElement.RemoveAll();
            targetElement.Value = content;
        }
        else // Incremental
        {
            // 增量模式：在现有内容后附加
            targetElement.Add(new XText(content));
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
            var targetElement = FindOrCreateElementByPath(basePath);
            targetElement.RemoveAll();
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
    /// 辅助方法：根据点号分隔的路径查找或创建元素。
    /// </summary>
    private XElement FindOrCreateElementByPath(string path)
    {
        XElement current = this.Root;
        if (string.IsNullOrEmpty(path))
        {
            return current;
        }

        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            XElement? next = current.Elements().FirstOrDefault(element => element.Name == segment);
            if (next == null)
            {
                next = new XElement(segment);
                current.Add(next);
            }

            current = next;
        }

        return current;
    }

    /// <summary>
    /// 获取内部内容的原始、未经格式化的字符串表示形式（不包含根元素）。
    /// </summary>
    /// <returns>格式化后的XML字符串。</returns>
    public override string ToString()
    {
        using var reader = Root.CreateReader();
        reader.MoveToContent();
        return reader.ReadInnerXml();
    }

    [GeneratedRegex(@"<([\p{L}\p{N}_-]+)>(.*?)</\1>", RegexOptions.Singleline)]
    private static partial Regex TagParseRegex();
}