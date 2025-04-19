// --- START OF FILE BlockTopologyExporterTests.cs ---
using FluentAssertions;
using YAESandBox.Core.Block;

namespace YAESandBox.Tests.Core.BlockTopologyExporterTests;

public class BlockTopologyExporterTests
{
    // Helper to create mock IBlockNode dictionary
    private static Dictionary<string, IBlockNode> CreateMockNodes(params (string id, string? parent, string[] children)[] nodes)
    {
        var dict = new Dictionary<string, IBlockNode>();
        foreach (var (id, parent, children) in nodes)
        {
            var node = new MockBlockNode(id, parent, children.ToList());
            dict.Add(id, node);
        }
        return dict;
    }

    // Simple mock implementing IBlockNode for testing topology
    private record MockBlockNode(string BlockId, string? ParentBlockId, List<string> ChildrenList) : IBlockNode
    {
        public string BlockId { get; } = BlockId;
        public string? ParentBlockId { get; } = ParentBlockId;
        public List<string> ChildrenList { get; } = ChildrenList;
    }

    [Fact]
    public void GenerateTopologyJson_ShouldReturnNull_ForEmptyDictionary()
    {
        // Arrange
        var emptyBlocks = new Dictionary<string, IBlockNode>();

        // Act
        var result = BlockTopologyExporter.GenerateTopologyJson(emptyBlocks);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GenerateTopologyJson_ShouldReturnNull_IfRootNotFound()
    {
        // Arrange
        var blocks = CreateMockNodes(("child-1", "root", [])); // Root missing

        // Act
        var result = BlockTopologyExporter.GenerateTopologyJson(blocks, "root");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GenerateTopologyJson_ShouldReturnSingleNode_ForRootOnly()
    {
        // Arrange
        var blocks = CreateMockNodes((BlockManager.WorldRootId, null, []));

        // Act
        var result = BlockTopologyExporter.GenerateTopologyJson(blocks);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(BlockManager.WorldRootId);
        result.Children.Should().BeEmpty();
    }

     [Fact]
    public void GenerateTopologyJson_ShouldBuildCorrectTreeStructure()
    {
        // Arrange
        var blocks = CreateMockNodes(
            (BlockManager.WorldRootId, null, ["c1", "c2"]),
            ("c1", BlockManager.WorldRootId, ["gc1a"]),
            ("c2", BlockManager.WorldRootId, []),
            ("gc1a", "c1", [])
        );

        // Act
        var result = BlockTopologyExporter.GenerateTopologyJson(blocks);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(BlockManager.WorldRootId);
        result.Children.Should().HaveCount(2);

        var child1 = result.Children.FirstOrDefault(c => c.Id == "c1");
        child1.Should().NotBeNull();
        child1!.Children.Should().ContainSingle().Which.Id.Should().Be("gc1a");
        child1!.Children.Single().Children.Should().BeEmpty(); // Grandchild has no children

        var child2 = result.Children.FirstOrDefault(c => c.Id == "c2");
        child2.Should().NotBeNull();
        child2!.Children.Should().BeEmpty();
    }

     [Fact]
    public void GenerateTopologyJson_ShouldHandleMissingChildNodesGracefully()
    {
        // Arrange
        var blocks = CreateMockNodes(
            (BlockManager.WorldRootId, null, ["c1", "missing-child"]), // Reference to non-existent child
            ("c1", BlockManager.WorldRootId, [])
        );
        // "missing-child" is not in the dictionary

        // Act
        var result = BlockTopologyExporter.GenerateTopologyJson(blocks);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(BlockManager.WorldRootId);
        result.Children.Should().ContainSingle(); // Only c1 should be present
        result.Children.Single().Id.Should().Be("c1");
         // Should log a warning about missing-child (cannot assert log easily here)
    }
}
// --- END OF FILE BlockTopologyExporterTests.cs ---