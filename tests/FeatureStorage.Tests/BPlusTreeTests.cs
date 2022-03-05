using System;
using System.IO;
using Xunit;

namespace FeatureStorage.Tests;

public class BPlusTreeTests
{
    
    [Fact]
    public void SerializeFromNonEmptyTree_ShouldFeelStream()
    {
        using var tree = new BPlusTree(nodeCapacity: 3, elementCount: 6);
        tree.Insert(5, 5);
        tree.Insert(3, 3);
        tree.Insert(4, 4);
        tree.Insert(0, 0);
        tree.Insert(1, 1);
        tree.Insert(2, 2);
        
        using var stream = new MemoryStream();
        tree.Serialize(stream);
        stream.Position = 0;

        var restoredTree = BPlusTree.Deserialize(stream);
        
        Assert.Equal(6U, restoredTree.Size);
        Assert.Equal(0U, restoredTree.Search(0).Value);
        Assert.Equal(1U, restoredTree.Search(1).Value);
        Assert.Equal(2U, restoredTree.Search(2).Value);
        Assert.Equal(3U, restoredTree.Search(3).Value);
        Assert.Equal(4U, restoredTree.Search(4).Value);
        Assert.Equal(5U, restoredTree.Search(5).Value);
    }
    
    [Fact]
    public void InsertDuplicateKeys_ShouldReturnFalse()
    {
        using var tree = new BPlusTree(nodeCapacity: 3, elementCount: 5);

        tree.Insert(1, 1);

        Assert.False(tree.Insert(1, 2));
        Assert.False(tree.Insert(1, 3));
        
        Assert.Equal(1U, tree.Size);
    }

    [Fact]
    public void InsertIntoTreeWithOddCapacity()
    {
        using var tree = new BPlusTree(nodeCapacity: 3);

        var numbers = new uint[]
        {
            1, 3, 5, 7, 9, 2, 4, 6, 8, 10, 13, 18, 11, 12, 19,
            15, 16, 17, 14, 20, 25, 30, 42, 41, 21, 23, 24, 27,
            33, 66
        };

        for (var i = 0; i < numbers.Length; i++)
            tree.Insert(numbers[i], numbers[i]);

        for (var i = 0; i < numbers.Length; i++)
        {
            var result = tree.Search(numbers[i]);
            Assert.True(result.Found);
            Assert.Equal(numbers[i], result.Value);
        }

        Assert.False(tree.Search(0).Found);
        
        
    }

    [Fact]
    public void InsertIntoTreeWithEventCapacity()
    {
        using var tree = new BPlusTree(nodeCapacity: 4);

        var numbers = new uint[]
        {
            1, 3, 5, 7, 9, 2, 4, 6, 8, 10, 13, 18, 11, 12, 19,
            15, 16, 17, 14, 20, 25, 30, 42, 41, 21, 23, 24, 27,
            33, 66
        };

        for (var i = 0; i < numbers.Length; i++)
            tree.Insert(numbers[i], numbers[i]);

        for (var i = 0; i < numbers.Length; i++)
        {
            var result = tree.Search(numbers[i]);
            Assert.True(result.Found);
            Assert.Equal(numbers[i], result.Value);
        }

        Assert.False(tree.Search(0).Found);
        //
        // File.Delete("/tmp/bptree.dot");
        // using var fs = File.OpenWrite("/tmp/bptree.dot");
        // tree.PrintAsDot(fs);
    }

    [Fact]
    public void SearchInLargeSet()
    {
        var values = new uint[50_000_000];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (uint)i;
        }

        var rng = new Random();
        var n = values.Length;
        while (n > 1)
        {
            var k = rng.Next(n);
            n--;
            (values[n], values[k]) = (values[k], values[n]);
        }
        
        var tree = new BPlusTree(nodeCapacity: 128, (uint)values.Length);
        
        for (var i = 0; i < values.Length; i++)
        {
            tree.Insert((uint)i, values[i]);
        }
        
        Assert.Equal((uint)values.Length, tree.Size); 
        
        for (var i = 0; i < values.Length; i++)
        {
            var r= tree.Search(values[i]);
            Assert.Equal(values[i], r.Value);
        }
    }
}