// =============== INode.cs ===============

using System.Collections.Generic;
using System.Diagnostics;

namespace YAESandBox.Core.Block.Tree;

/// <summary>
/// 定义树节点的接口，仅包含结构和选择逻辑。
/// 实现此接口的类可以作为树中的节点。
/// </summary>
public interface INode
{
    /// <summary>
    /// 节点的唯一标识符 (由外部Guid提供)。
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 父节点的ID。根节点此值为null。
    /// 需要 setter 以便 Manager 在添加/删除时更新。
    /// </summary>
    string? ParentId { get; set; }

    /// <summary>
    /// 子节点列表 (对外只读访问)。
    /// </summary>
    IReadOnlyList<INode> Children { get; }

    /// <summary>
    /// 当前选中的子节点的ID。
    /// 如果没有子节点或没有选择，则为 null。
    /// 需要 setter 以便 Manager 更新选择。
    /// </summary>
    string? SelectedChildId { get; set; }

    // 注意：添加/删除子节点的 *具体实现* 在 Node 类内部，
    // 但 *调用* 这些操作应通过 NodeManager 来确保一致性。
}

/// <summary>
/// INode 接口的一个具体实现。
/// </summary>
[DebuggerDisplay("Id = {Id}, ParentId = {ParentId}, Children = {Children.Count}, Selected = {SelectedChildId}")]
public class Node : INode
{
    public string Id { get; }
    public string? ParentId { get; set; } // 由 Manager 设置

    // 使用 List<INode> 存储子节点，方便增删
    // 对外暴露为 IReadOnlyList<INode>
    private readonly List<INode> _children = new List<INode>();
    public IReadOnlyList<INode> Children => this._children.AsReadOnly();

    private string? _selectedChildId;

    public string? SelectedChildId
    {
        get => this._selectedChildId;
        set
        {
            // 基本验证：如果要设置的ID非null，它必须是当前子节点之一的ID
            // 注意： "必须选择一个" 的规则由 Manager 在操作（Add/Remove）后强制执行，而不是在此 setter 中。
            if (value != null && this._children.All(c => c.Id != value))
            {
                // 或者记录警告，或者根据业务需要决定是否抛出异常
                // 这里我们允许设置可能暂时无效的值，Manager 会修复它
                // throw new ArgumentException($"尝试为节点 '{Id}' 选择一个不存在的子节点 '{value}'.");
                Console.Error.WriteLine($"警告: 尝试为节点 '{this.Id}' 设置一个当前不存在的子节点 ID '{value}'. Manager 应稍后纠正。");
            }

            // 如果子节点列表为空，则选择必须为 null
            if (this._children.Count == 0 && value != null)
            {
                Console.Error.WriteLine($"警告: 节点 '{this.Id}' 没有子节点，SelectedChildId 被强制设为 null (尝试设置的值为 '{value}').");
                this._selectedChildId = null;
                return; // 直接返回，不设置无效值
            }

            this._selectedChildId = value;
        }
    }

    /// <summary>
    /// 创建一个新节点。
    /// </summary>
    /// <param name="id">节点的唯一ID。</param>
    /// <param name="parentId">父节点ID（可选）。</param>
    public Node(string id, string? parentId = null)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentNullException(nameof(id), "节点ID不能为空。");
        this.Id = id;
        this.ParentId = parentId;
    }

    // --- 内部方法，供 NodeManager 调用 ---

    /// <summary>
    /// (内部使用) 添加一个子节点到此节点。
    /// </summary>
    /// <param name="child">要添加的子节点。</param>
    internal void AddChildInternal(INode child)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));
        if (this._children.Any(c => c.Id == child.Id))
        {
            // 通常不应发生，因为 Manager 会检查全局唯一性
            Console.Error.WriteLine($"警告: 节点 '{this.Id}' 尝试添加已存在的子节点 ID '{child.Id}'.");
            return; // 或者抛出异常
        }

        this._children.Add(child);
        child.ParentId = this.Id; // 建立父子关系
    }

    /// <summary>
    /// (内部使用) 从此节点移除一个子节点。
    /// </summary>
    /// <param name="childId">要移除的子节点的ID。</param>
    /// <returns>如果成功移除返回 true，否则 false。</returns>
    internal bool RemoveChildInternal(string childId)
    {
        var childToRemove = this._children.FirstOrDefault(c => c.Id == childId);
        if (childToRemove != null)
        {
            bool removed = this._children.Remove(childToRemove);
            if (removed)
            {
                childToRemove.ParentId = null; // 断开父子关系

                // 如果移除的是当前选中的子节点，需要清除选择状态
                // Manager 将负责后续的选择更新（EnsureSelection）
                if (this.SelectedChildId == childId)
                {
                    this.SelectedChildId = null;
                }
            }

            return removed;
        }

        return false;
    }

    /// <summary>
    /// (内部使用) 清空所有子节点。用于递归删除。
    /// </summary>
    internal void ClearChildrenInternal()
    {
        foreach (var child in this._children)
        {
            child.ParentId = null; // 断开每个子节点的父连接
        }

        this._children.Clear();
        this.SelectedChildId = null; // 没有子节点了，自然没有选择
    }
}

/// <summary>
/// 管理 INode 树结构，处理节点操作、选择逻辑和分页。
/// </summary>
public class NodeManager
{
    /// <summary>
    /// 存储所有节点的字典，键为节点ID，值为节点实例。提供快速查找。
    /// 如果需要线程安全，请使用 ConcurrentDictionary。
    /// </summary>
    private Dictionary<string, INode> nodes { get; } = new Dictionary<string, INode>();
    // private readonly ConcurrentDictionary<string, INode> _nodes = new ConcurrentDictionary<string, INode>();

    /// <summary>
    /// 虚拟根节点的ID。
    /// </summary>
    public const string WorldRootId = "__WORLD__";

    // --- 分页状态 ---

    /// <summary>
    /// 存储每个节点子列表的当前页码。键: NodeId, 值: CurrentPage (从1开始)。
    /// </summary>
    private Dictionary<string, int> nodeCurrentPage { get; } = new Dictionary<string, int>();

    /// <summary>
    /// 存储每个节点子列表的每页项目数。键: NodeId, 值: ItemsPerPage。
    /// </summary>
    private Dictionary<string, int> nodeItemsPerPage { get; } = new Dictionary<string, int>();

    /// <summary>
    ///
    /// 。
    /// </summary>
    public const int DefaultItemsPerPage = 10; // 你可以根据需要调整默认值


    /// <summary>
    /// 初始化 NodeManager，创建虚拟根节点。
    /// </summary>
    public NodeManager()
    {
        // 创建并添加虚拟根节点
        // 注意：这里我们假设使用具体的 'Node' 类作为根节点。
        // 如果允许任何 INode 实现作为根，可能需要工厂模式或不同的初始化方式。
        var root = new Node(WorldRootId, null); // 根节点没有父ID
        this.nodes.Add(WorldRootId, root);

        // 初始化根节点的分页设置
        this.nodeItemsPerPage[WorldRootId] = DefaultItemsPerPage;
        this.nodeCurrentPage[WorldRootId] = 1; // 页码从 1 开始
    }

    /// <summary>
    /// 获取根节点实例。
    /// </summary>
    /// <returns>根节点 INode 实例。</returns>
    /// <exception cref="InvalidOperationException">如果根节点因意外原因丢失。</exception>
    public INode GetRoot()
    {
        if (this.nodes.TryGetValue(WorldRootId, out var root))
        {
            return root;
        }

        // 这理论上不应该发生，因为构造函数会创建它
        throw new InvalidOperationException("致命错误：根节点丢失！");
    }

    /// <summary>
    /// 通过 ID 查找节点。
    /// </summary>
    /// <param name="nodeId">要查找的节点 ID。</param>
    /// <returns>找到的 INode 实例；如果不存在则返回 null。</returns>
    public INode? FindNodeById(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return null;
        this.nodes.TryGetValue(nodeId, out var node);
        return node;
    }

    /// <summary>
    /// 获取从根节点开始的当前选择路径。
    /// </summary>
    /// <returns>包含按顺序选择的节点的列表，从根节点开始。</returns>
    public List<INode> GetSelectedPath()
    {
        var path = new List<INode>();
        var currentNode = this.GetRoot();

        while (currentNode != null)
        {
            path.Add(currentNode);
            string? selectedChildId = currentNode.SelectedChildId;

            if (string.IsNullOrEmpty(selectedChildId))
            {
                break; // 到达选择路径的末端
            }

            // 查找选中的子节点
            var nextNode = this.FindNodeById(selectedChildId);

            if (nextNode == null)
            {
                // 数据不一致：SelectedChildId 指向一个不存在的节点
                Console.Error.WriteLine(
                    $"错误: 节点 '{currentNode.Id}' 的 SelectedChildId '{selectedChildId}' 指向一个不存在的节点。路径中断。");
                // 可以在这里尝试修复，例如将 currentNode.SelectedChildId 设为 null
                // currentNode.SelectedChildId = null; // 可选的修复操作
                break;
            }

            // 安全性检查：选中的节点确实是当前节点的子节点吗？
            if (!currentNode.Children.Any(c => c.Id == selectedChildId))
            {
                Console.Error.WriteLine(
                    $"错误: 节点 '{currentNode.Id}' 的 SelectedChildId '{selectedChildId}' 不是其直接子节点。路径中断。");
                // currentNode.SelectedChildId = null; // 可选的修复操作
                break;
            }


            currentNode = nextNode;
        }

        return path;
    }

    /// <summary>
    /// 添加一个新节点作为指定父节点的子节点。
    /// 新添加的节点将自动被其父节点选中。
    /// </summary>
    /// <param name="parentId">父节点的ID。</param>
    /// <param name="newNode">要添加的新节点实例（其ID必须已设置且全局唯一）。</param>
    /// <returns>返回包含更新后选择路径的节点列表；如果添加失败（如父节点不存在、ID冲突），则返回 null。</returns>
    public List<INode>? AddNode(string parentId, INode newNode)
    {
        if (newNode == null) throw new ArgumentNullException(nameof(newNode));
        if (string.IsNullOrEmpty(newNode.Id)) throw new ArgumentException("新节点的ID不能为空。", nameof(newNode));

        // 检查ID是否已存在
        if (this.nodes.ContainsKey(newNode.Id))
        {
            Console.Error.WriteLine($"错误: 尝试添加的节点ID '{newNode.Id}' 已存在。");
            return null;
        }

        // 查找父节点
        var parentNode = this.FindNodeById(parentId);
        if (parentNode == null)
        {
            Console.Error.WriteLine($"错误: 未找到ID为 '{parentId}' 的父节点。无法添加子节点 '{newNode.Id}'。");
            return null;
        }

        // 检查父节点是否为我们期望的可修改类型 (Node)
        if (parentNode is Node concreteParentNode)
        {
            try
            {
                // 1. 将新节点添加到父节点的子节点列表 (内部方法)
                concreteParentNode.AddChildInternal(newNode);

                // 2. 将新节点添加到全局查找字典
                this.nodes.Add(newNode.Id, newNode);

                // 3. 初始化新节点的分页设置
                this.nodeItemsPerPage[newNode.Id] = DefaultItemsPerPage;
                this.nodeCurrentPage[newNode.Id] = 1;

                // 4. 自动选择新添加的节点
                //    直接设置父节点的 SelectedChildId
                parentNode.SelectedChildId = newNode.Id;

                // 5. 确保父节点满足“有子则必选”规则（虽然这里刚添加并选中了，但保持一致性）
                EnsureSelection(parentNode);

                // 6. 添加成功，返回新的选择路径
                return this.GetSelectedPath();
            }
            catch (Exception ex)
            {
                // 如果 AddChildInternal 或其他步骤出错，进行回滚
                Console.Error.WriteLine($"添加节点 '{newNode.Id}' 到父节点 '{parentId}' 时出错: {ex.Message}");
                this.nodes.Remove(newNode.Id); // 从全局字典移除
                concreteParentNode.RemoveChildInternal(newNode.Id); // 尝试从父节点移除
                // 可能还需要重置父节点的选择状态，取决于错误点
                EnsureSelection(parentNode); // 重新确保父节点选择状态正确
                return null;
            }
        }
        else
        {
            // 父节点不是 Node 类型，无法调用内部方法添加子节点
            Console.Error.WriteLine($"错误: 父节点 '{parentId}' 不是预期的 'Node' 类型，无法添加子节点。考虑检查节点实现或管理器逻辑。");
            return null;
        }
    }

    /// <summary>
    /// 使父节点选择指定的子节点。
    /// </summary>
    /// <param name="childIdToSelect">要被选中的子节点的ID。</param>
    /// <returns>返回包含更新后选择路径的节点列表；如果选择失败（节点不存在、不是子节点等），则返回 null。</returns>
    public List<INode>? SelectNode(string childIdToSelect)
    {
        // 查找要选择的节点
        var nodeToSelect = this.FindNodeById(childIdToSelect);
        if (nodeToSelect == null)
        {
            Console.Error.WriteLine($"错误: 未找到要选择的节点ID '{childIdToSelect}'。");
            return null;
        }

        // 根节点不能被 "选择" (它没有父节点)
        if (nodeToSelect.ParentId == null || nodeToSelect.Id == WorldRootId)
        {
            Console.Error.WriteLine($"错误: 根节点 '{childIdToSelect}' 或无父节点的节点无法被选择。");
            return null;
        }

        // 查找父节点
        var parentNode = this.FindNodeById(nodeToSelect.ParentId);
        if (parentNode == null)
        {
            // 数据不一致：子节点有 ParentId，但父节点找不到了
            Console.Error.WriteLine($"错误: 节点 '{childIdToSelect}' 的父节点ID '{nodeToSelect.ParentId}' 在系统中找不到。");
            return null;
        }

        // 确认要选择的节点确实是父节点的子节点
        if (!parentNode.Children.Any(c => c.Id == childIdToSelect))
        {
            Console.Error.WriteLine($"错误: 节点 '{childIdToSelect}' 不是父节点 '{parentNode.Id}' 的直接子节点。无法选择。");
            return null;
        }

        // 更新父节点的选择
        if (parentNode.SelectedChildId != childIdToSelect)
        {
            parentNode.SelectedChildId = childIdToSelect;
            // 选择改变，返回新的选择路径
            return this.GetSelectedPath();
        }
        else
        {
            // 本来就选中了，路径不变，直接返回当前路径
            return this.GetSelectedPath();
        }
    }

    /// <summary>
    /// 删除指定父节点下的一个子节点及其所有后代。
    /// </summary>
    /// <param name="parentId">父节点的ID。</param>
    /// <param name="childIdToRemove">要删除的子节点的ID。</param>
    /// <returns>如果成功删除返回 true，否则 false。</returns>
    public bool RemoveChildNode(string parentId, string childIdToRemove)
    {
        // 查找父节点
        var parentNode = this.FindNodeById(parentId);
        if (parentNode == null)
        {
            Console.Error.WriteLine($"错误: 未找到父节点ID '{parentId}'。无法删除子节点 '{childIdToRemove}'。");
            return false;
        }

        // 查找要删除的子节点，并确认父子关系
        var childNode = this.FindNodeById(childIdToRemove);
        if (childNode == null || childNode.ParentId != parentId)
        {
            Console.Error.WriteLine($"错误: 未找到子节点ID '{childIdToRemove}'，或者它不属于父节点 '{parentId}'。");
            return false;
        }

        // 调用通用的递归删除方法
        bool deleted = this.DeleteNodeRecursive(childIdToRemove);

        if (deleted)
        {
            // 如果删除成功，需要检查并更新父节点的选择状态
            // 注意：RemoveChildInternal 已经处理了 SelectedChildId == childIdToRemove 的情况，将其设为 null
            EnsureSelection(parentNode); // 确保父节点在有其他子节点时，选择一个

            // 重置父节点的分页到第一页，因为子节点列表变化了
            this.SetNodeCurrentPage(parentId, 1);
        }

        return deleted;
    }

    /// <summary>
    /// 删除指定ID的节点及其所有后代。不能删除根节点。
    /// </summary>
    /// <param name="nodeIdToRemove">要删除的节点的ID。</param>
    /// <returns>如果成功删除返回 true，否则 false。</returns>
    public bool DeleteNode(string nodeIdToRemove)
    {
        if (string.IsNullOrEmpty(nodeIdToRemove) || nodeIdToRemove == WorldRootId)
        {
            Console.Error.WriteLine($"错误: 不能删除根节点或无效ID '{nodeIdToRemove}'。");
            return false;
        }

        // 查找要删除的节点
        var nodeToRemove = this.FindNodeById(nodeIdToRemove);
        if (nodeToRemove == null)
        {
            Console.Error.WriteLine($"错误: 未找到要删除的节点ID '{nodeIdToRemove}'。");
            return false;
        }

        // 获取父节点ID (在删除前获取)
        string? parentId = nodeToRemove.ParentId;

        // 执行递归删除
        bool deleted = this.DeleteNodeRecursive(nodeIdToRemove);

        // 如果删除成功，并且该节点有父节点，则更新父节点状态
        if (deleted && parentId != null)
        {
            var parentNode = this.FindNodeById(parentId);
            if (parentNode != null)
            {
                // DeleteNodeRecursive 内部已经调用了 parent.RemoveChildInternal
                // 我们只需要确保父节点的选择状态是正确的
                EnsureSelection(parentNode);

                // 重置父节点的分页到第一页
                this.SetNodeCurrentPage(parentId, 1);
            }
            else
            {
                // 父节点在删除过程中也可能被删除了（理论上不应直接发生，除非结构混乱）
                Console.Error.WriteLine($"警告: 节点 '{nodeIdToRemove}' 的父节点 '{parentId}' 在删除后找不到了。");
            }
        }

        return deleted;
    }


    // --- Helper Methods ---

    /// <summary>
    /// (私有) 递归地删除指定ID的节点及其所有后代。
    /// </summary>
    /// <param name="nodeId">要删除的起始节点ID。</param>
    /// <returns>如果起始节点被找到并成功从其父节点移除（或无父节点），则返回 true。</returns>
    private bool DeleteNodeRecursive(string nodeId)
    {
        var node = this.FindNodeById(nodeId);
        if (node == null)
        {
            return false; // 节点已不存在
        }

        // 1. 递归删除所有子节点
        //    需要复制子节点ID列表，因为在遍历时会修改 Children 集合
        var childIds = node.Children.Select(c => c.Id).ToList();
        foreach (string childId in childIds)
        {
            this.DeleteNodeRecursive(childId); // 忽略返回值，尽力删除
        }

        // 2. 从全局字典中移除当前节点
        this.nodes.Remove(nodeId);
        this.nodeCurrentPage.Remove(nodeId); // 清理分页状态
        this.nodeItemsPerPage.Remove(nodeId);

        // 3. 从其父节点的子列表中移除当前节点
        if (node.ParentId != null)
        {
            var parentNode = this.FindNodeById(node.ParentId);
            if (parentNode is Node concreteParent)
            {
                concreteParent.RemoveChildInternal(nodeId);
                // 父节点的选择状态由调用 DeleteNode 或 RemoveChildNode 的公共方法负责更新
            }
            else if (parentNode != null)
            {
                // 父节点存在但不是 Node 类型
                Console.Error.WriteLine($"警告: 节点 '{nodeId}' 的父节点 '{node.ParentId}' 不是 'Node' 类型。无法从其内部子列表移除。树结构可能不一致。");
            }
            // 如果 parentNode == null，说明父节点已被删除，无需操作
        }

        // 4. (可选) 如果节点是 Node 类型，清空其内部子列表引用以帮助GC
        if (node is Node concreteNode)
        {
            concreteNode.ClearChildrenInternal();
        }


        return true; // 成功删除（或节点本就不存在）
    }

    /// <summary>
    /// (私有) 确保父节点在有子节点的情况下，有且仅有一个子节点被选中。
    /// 如果没有选中项，则默认选择第一个子节点。
    /// 如果选中的子节点已不存在，则重新选择第一个子节点。
    /// 如果没有子节点，确保选中项为 null。
    /// </summary>
    /// <param name="parentNode">要检查和确保选择的父节点。</param>
    private static void EnsureSelection(INode parentNode)
    {
        var children = parentNode.Children;
        string? currentSelection = parentNode.SelectedChildId;

        if (children.Any()) // 如果有子节点
        {
            // 检查当前选择是否有效（是否存在于子节点中）
            bool selectionIsValid =
                !string.IsNullOrEmpty(currentSelection) && children.Any(c => c.Id == currentSelection);

            // 如果没有选择，或者选择无效，则选择第一个子节点
            if (selectionIsValid)
                return;
            parentNode.SelectedChildId = children.First().Id;
            if (!string.IsNullOrEmpty(currentSelection) && !selectionIsValid)
            {
                Console.Error.WriteLine(
                    $"警告: 节点 '{parentNode.Id}' 的选中子节点 '{currentSelection}' 无效，已重置为第一个子节点 '{parentNode.SelectedChildId}'。");
            }

            return;
        }

        // 如果没有子节点
        // 确保选择为 null
        if (currentSelection != null)
        {
            parentNode.SelectedChildId = null;
        }
    }

    // --- Pagination Methods ---

    /// <summary>
    /// 设置指定节点子列表的每页显示数量。
    /// </summary>
    /// <param name="nodeId">节点ID。</param>
    /// <param name="itemsPerPage">每页的项目数（必须大于0）。</param>
    public void SetNodeItemsPerPage(string nodeId, int itemsPerPage)
    {
        if (itemsPerPage < 1)
        {
            Console.Error.WriteLine($"警告: ItemsPerPage 不能小于1。将对节点 '{nodeId}' 使用默认值 {DefaultItemsPerPage}。");
            itemsPerPage = DefaultItemsPerPage; // 或抛出异常
        }

        if (this.nodes.ContainsKey(nodeId))
        {
            this.nodeItemsPerPage[nodeId] = itemsPerPage;
            // 当每页数量变化时，最好将当前页重置为第一页，以避免页码超出范围
            this.SetNodeCurrentPage(nodeId, 1);
        }
        else
            Console.Error.WriteLine($"警告: 未找到节点 '{nodeId}'，无法设置 ItemsPerPage。");
    }

    /// <summary>
    /// 获取指定节点子列表的每页显示数量。
    /// </summary>
    /// <param name="nodeId">节点ID。</param>
    /// <returns>每页的项目数；如果节点未设置，则返回默认值。</returns>
    public int GetNodeItemsPerPage(string nodeId)
    {
        if (this.nodeItemsPerPage.TryGetValue(nodeId, out int items))
        {
            return items;
        }

        // 如果没有显式设置（理论上AddNode时会设置），返回默认值
        // 或者如果节点不存在也返回默认值
        return DefaultItemsPerPage;
    }

    /// <summary>
    /// 设置指定节点子列表的当前页码。页码会自动约束在有效范围内 [1, TotalPages]。
    /// </summary>
    /// <param name="nodeId">节点ID。</param>
    /// <param name="page">要设置的页码 (从1开始)。</param>
    public void SetNodeCurrentPage(string nodeId, int page)
    {
        var node = this.FindNodeById(nodeId);
        if (node != null)
        {
            int itemsPerPage = this.GetNodeItemsPerPage(nodeId);
            int totalChildren = node.Children.Count;
            int totalPages = (totalChildren == 0) ? 1 : (totalChildren + itemsPerPage - 1) / itemsPerPage;

            // 将页码限制在 [1, totalPages] 范围内
            int validPage = Math.Max(1, Math.Min(page, totalPages));
            this.nodeCurrentPage[nodeId] = validPage;
        }
        else
        {
            Console.Error.WriteLine($"警告: 未找到节点 '{nodeId}'，无法设置 CurrentPage。");
        }
    }

    /// <summary>
    /// 获取指定节点子列表的当前页码。
    /// </summary>
    /// <param name="nodeId">节点ID。</param>
    /// <returns>当前页码 (从1开始)；如果节点未设置或不存在，返回 1。</returns>
    public int GetNodeCurrentPage(string nodeId)
    {
        if (this.nodeCurrentPage.TryGetValue(nodeId, out int page))
        {
            return page;
        }

        // 如果没有显式设置（理论上AddNode时会设置），返回 1
        // 或者如果节点不存在也返回 1
        return 1;
    }

    /// <summary>
    /// 获取指定节点当前页应该显示的子节点列表。
    /// </summary>
    /// <param name="nodeId">父节点的ID。</param>
    /// <returns>只读的子节点列表，对应当前分页设置；如果节点不存在或无子节点，返回空列表。</returns>
    public IReadOnlyList<INode> GetChildrenForCurrentPage(string nodeId)
    {
        var node = this.FindNodeById(nodeId);
        // 如果节点不存在，或者没有子节点，返回空列表
        if (node == null || !node.Children.Any())
        {
            // 返回一个空的只读列表实例
            return Array.AsReadOnly(Array.Empty<INode>()); // 或者 List<INode>().AsReadOnly();
        }

        int itemsPerPage = this.GetNodeItemsPerPage(nodeId);
        int currentPage = this.GetNodeCurrentPage(nodeId); // 已经过 SetNodeCurrentPage 约束

        // 计算需要跳过的项目数
        int skipCount = (currentPage - 1) * itemsPerPage;

        // 使用 LINQ 的 Skip 和 Take 方法进行分页
        // 这在 IReadOnlyList<T> 上也能工作
        var pagedChildren = node.Children.Skip(skipCount).Take(itemsPerPage).ToList();

        // 返回只读版本
        return pagedChildren.AsReadOnly();
    }

    /// <summary>
    /// 获取指定节点的分页信息 (当前页码, 总页数)。
    /// </summary>
    /// <param name="nodeId">节点ID。</param>
    /// <returns>一个元组 (CurrentPage, TotalPages)。如果节点不存在，返回 (1, 1)。</returns>
    public (int CurrentPage, int TotalPages) GetPaginationInfo(string nodeId)
    {
        var node = this.FindNodeById(nodeId);
        if (node == null)
        {
            return (1, 1); // 节点不存在，假设为1页，当前在第1页
        }

        int itemsPerPage = this.GetNodeItemsPerPage(nodeId);
        int totalChildren = node.Children.Count;
        int totalPages = (totalChildren == 0) ? 1 : (totalChildren + itemsPerPage - 1) / itemsPerPage;
        int currentPage = this.GetNodeCurrentPage(nodeId); // 这个值已经被约束过

        return (currentPage, totalPages);
    }

    /// <summary>
    /// 尝试将指定节点的子列表视图切换到下一页。
    /// </summary>
    /// <param name="nodeId">节点ID。</param>
    public void GoToNextPage(string nodeId)
    {
        var (currentPage, totalPages) = this.GetPaginationInfo(nodeId);
        if (currentPage < totalPages)
        {
            this.SetNodeCurrentPage(nodeId, currentPage + 1);
        }
        // else: 已经是最后一页，什么都不做
    }

    /// <summary>
    /// 尝试将指定节点的子列表视图切换到上一页。
    /// </summary>
    /// <param name="nodeId">节点ID。</param>
    public void GoToPreviousPage(string nodeId)
    {
        int currentPage = this.GetNodeCurrentPage(nodeId);
        if (currentPage > 1)
        {
            this.SetNodeCurrentPage(nodeId, currentPage - 1);
        }
        // else: 已经是第一页，什么都不做
    }

    /// <summary>
    /// (辅助) 生成用于UI显示的分页标签字符串。
    /// </summary>
    /// <param name="nodeId">节点ID。</param>
    /// <returns>格式如 "<当前页/总页数>" 的字符串，例如 "<1/5>"。</returns>
    public string GetPaginationLabel(string nodeId)
    {
        var (currentPage, totalPages) = this.GetPaginationInfo(nodeId);
        return $"<{currentPage}/{totalPages}>";
    }
}


// =============== Example Usage (Conceptual) ===============
/*
using System;
using TreeStructure;

public class Example
{
    public static void Main(string[] args)
    {
        // 1. 创建管理器
        var manager = new NodeManager();

        // 2. 获取根节点
        var root = manager.GetRoot();
        Console.WriteLine($"根节点 ID: {root.Id}");

        // 3. 添加一些子节点到根节点下
        var nodeA = new Node(Guid.NewGuid().ToString());
        var nodeB = new Node(Guid.NewGuid().ToString());
        var pathAfterAddA = manager.AddNode(NodeManager.WorldRootId, nodeA); // 添加A，A会被自动选中
        var pathAfterAddB = manager.AddNode(NodeManager.WorldRootId, nodeB); // 添加B，B会被自动选中

        Console.WriteLine($"添加B后，根选择: {root.SelectedChildId}"); // 应该是 nodeB.Id
        Console.WriteLine($"当前选择路径: {string.Join(" -> ", manager.GetSelectedPath().Select(n => n.Id))}");

        // 4. 添加孙子节点
        var nodeB1 = new Node(Guid.NewGuid().ToString());
        manager.AddNode(nodeB.Id, nodeB1); // 添加到B下，B1会被B选中

        Console.WriteLine($"B 节点的选择: {nodeB.SelectedChildId}"); // 应该是 nodeB1.Id
        Console.WriteLine($"当前选择路径: {string.Join(" -> ", manager.GetSelectedPath().Select(n => n.Id))}");

        // 5. 切换根节点的选择到 A
        manager.SelectNode(nodeA.Id);
        Console.WriteLine($"切换后，根选择: {root.SelectedChildId}"); // 应该是 nodeA.Id
        Console.WriteLine($"当前选择路径: {string.Join(" -> ", manager.GetSelectedPath().Select(n => n.Id))}"); // World -> A

        // 6. 测试分页 (假设根节点有很多子节点)
        manager.SetNodeItemsPerPage(NodeManager.WorldRootId, 1); // 每页只显示1个
        for(int i=0; i<5; ++i) manager.AddNode(NodeManager.WorldRootId, new Node(Guid.NewGuid().ToString()));

        Console.WriteLine($"根节点分页: {manager.GetPaginationLabel(NodeManager.WorldRootId)}"); // 应该类似 <1/7> 或更多
        var page1Children = manager.GetChildrenForCurrentPage(NodeManager.WorldRootId);
        Console.WriteLine($"第一页子节点 ({page1Children.Count}): {page1Children.FirstOrDefault()?.Id}");

        manager.GoToNextPage(NodeManager.WorldRootId);
        Console.WriteLine($"根节点分页: {manager.GetPaginationLabel(NodeManager.WorldRootId)}"); // <2/7>
        var page2Children = manager.GetChildrenForCurrentPage(NodeManager.WorldRootId);
        Console.WriteLine($"第二页子节点 ({page2Children.Count}): {page2Children.FirstOrDefault()?.Id}");

        // 7. 删除节点 B1
        manager.DeleteNode(nodeB1.Id);
        Console.WriteLine($"删除 B1 后，B 是否还有选择？ {nodeB.SelectedChildId == null}"); // 应该是 true
        Console.WriteLine($"节点 B1 是否还存在？ {manager.FindNodeById(nodeB1.Id) == null}"); // 应该是 true

        // 8. 删除节点 A 及其所有后代 (这里 A 没有后代)
        manager.DeleteNode(nodeA.Id);
        Console.WriteLine($"删除 A 后，根节点的选择？ {root.SelectedChildId}"); // 可能指向 B 或其他剩余节点
        Console.WriteLine($"节点 A 是否还存在？ {manager.FindNodeById(nodeA.Id) == null}"); // 应该是 true
        Console.WriteLine($"当前选择路径: {string.Join(" -> ", manager.GetSelectedPath().Select(n => n.Id))}");

    }
}
*/