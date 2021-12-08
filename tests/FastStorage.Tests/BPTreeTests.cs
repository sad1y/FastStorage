using System.IO;
using FastStorage;
using Xunit;
using Xunit.Abstractions;

namespace BPTreeTests;

public class UnitTest1
{
    private readonly ITestOutputHelper _testOutputHelper;

    public UnitTest1(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void Test3()
    {
        using var tree = new BPlusTree(nodeCapacity: 3, nodePerMemBlock: 2);

        tree.Insert(1, 1);
        tree.Insert(3, 3);
        tree.Insert(5, 5);
        tree.Insert(7, 7);
        tree.Insert(9, 9);
        tree.Insert(2, 2);
        tree.Insert(4, 4);
        tree.Insert(6, 6);
        tree.Insert(8, 8);
        tree.Insert(10, 10);
        // tree.Insert(22, 22);
        // tree.Insert(13, 13);
        // tree.Insert(18, 18);
        // tree.Insert(0, 0);
        // tree.Insert(11, 11);

        File.Delete("/tmp/bptree.dot");
        using var fs = File.OpenWrite("/tmp/bptree.dot");
        tree.PrintAsDot(fs);


        // Assert.Equal(1, tree.Search(1));
        // Assert.Equal(2, tree.Search(2));
        // Assert.Equal(3, tree.Search(3));
        // Assert.Equal(4, tree.Search(4));
        // Assert.Equal(9, tree.Search(9));
        // Assert.Equal(8, tree.Search(8));
    }

    [Fact]
    public void InsertIntoTreeWithOddCapacity()
    {
        using var tree = new BPlusTree(nodeCapacity: 3, nodePerMemBlock: 16);

        var numbers = new ulong[]
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
        using var tree = new BPlusTree(nodeCapacity: 4, nodePerMemBlock: 16);

        var numbers = new ulong[]
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
}