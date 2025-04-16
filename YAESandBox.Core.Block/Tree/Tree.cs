namespace YAESandBox.Core.Block.Tree;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 节点接口，定义节点的基本属性
/// </summary>
/// <typeparam name="T">节点存储的数据类型</typeparam>
public interface INode<T>
{
    /// <summary>
    /// 节点的唯一标识符
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// 节点存储的数据
    /// </summary>
    T Data { get; set; } // 允许外部修改数据

    /// <summary>
    /// 父节点引用 (根节点为 null)
    /// </summary>
    INode<T>? Parent { get; }

    /// <summary>
    /// 子节点字典 (ID -> 子节点)
    /// </summary>
    IReadOnlyDictionary<Guid, INode<T>> Children { get; }

    /// <summary>
    /// 当前选择的子节点的 ID (如果没有子节点或未选择，则为 null)
    /// </summary>
    Guid? SelectedChildId { get; }

    /// <summary>
    /// 获取当前选择的子节点
    /// </summary>
    INode<T>? GetSelectedChildNode();
}

/// <summary>
/// 树节点的内部实现
/// </summary>
/// <typeparam name="T">节点存储的数据类型</typeparam>
internal class Node<T> : INode<T>
{
    public Guid Id { get; }
    public T Data { get; set; }

    // 使用 internal set 限制只能在 TreeManager 中修改
    internal Node<T>? ParentNode { get; set; }
    public INode<T>? Parent => this.ParentNode;

    // 使用 Dictionary 实现 O(1) 的子节点查找
    internal Dictionary<Guid, Node<T>> ChildNodes { get; } = new Dictionary<Guid, Node<T>>();

    public IReadOnlyDictionary<Guid, INode<T>> Children => this.ChildNodes.ToDictionary(kvp => kvp.Key, kvp => (INode<T>)kvp.Value); // 返回接口的只读字典视图


    internal Guid? SelectedChildIdInternal { get; set; }
    public Guid? SelectedChildId => this.SelectedChildIdInternal;

    internal Node(Guid id, T data, Node<T>? parent = null)
    {
        this.Id = id;
        this.Data = data;
        this.ParentNode = parent;
        this.SelectedChildIdInternal = null; // 默认不选择
    }

    public INode<T>? GetSelectedChildNode()
    {
        if (this.SelectedChildIdInternal.HasValue && this.ChildNodes.TryGetValue(this.SelectedChildIdInternal.Value, out var selectedChild))
        {
            return selectedChild;
        }

        return null;
    }

    // 为了方便调试和查看，可以重写 ToString
    public override string ToString()
    {
        return
            $"Node(Id={this.Id}, Data={this.Data}, Children={this.ChildNodes.Count}, Selected={this.SelectedChildId?.ToString() ?? "None"})";
    }
}