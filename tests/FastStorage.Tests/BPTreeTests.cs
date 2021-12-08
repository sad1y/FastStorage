using System;
using System.Collections.Generic;
using System.IO;
using FastStorage;
using Xunit;
using Xunit.Abstractions;

namespace BPTreeTests
{

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
        public void InsertIntoTreeWithEventCapacity()
        {
            using var tree = new BPlusTree(nodeCapacity: 4, nodePerMemBlock: 16);
            
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
            
            tree.Insert(13, 13);
            tree.Insert(18, 18);
            tree.Insert(11, 11);
            tree.Insert(12, 12);
            tree.Insert(19, 19);
            tree.Insert(15, 15);
            tree.Insert(16, 16);
            tree.Insert(17, 17);
            tree.Insert(14, 14);
            tree.Insert(20, 20);
            
            tree.Insert(25, 25);
            tree.Insert(30, 30);
            tree.Insert(42, 42);
            tree.Insert(41, 41);
            tree.Insert(21, 21);
            
            tree.Insert(23, 23);
            tree.Insert(24, 24);
            tree.Insert(27, 27);
            
            tree.Insert(33, 33);
            tree.Insert(66, 66);
            
            
            
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
        public void Test1()
        {
            var tree = new BPlusTreeSimple<long>(3, Comparer<long>.Default);
            
            tree.Insert(3, 3);
            tree.Insert(4, 4);
            tree.Insert(2, 2);
            tree.Insert(1, 1);
            tree.Insert(9, 9);
            tree.Insert(8, 8);
            tree.Insert(8, 8);
            tree.Insert(5, 5);

            Assert.Equal(1, tree.Search(1));
            Assert.Equal(2, tree.Search(2));
            Assert.Equal(3, tree.Search(3));
            Assert.Equal(4, tree.Search(4));
            Assert.Equal(9, tree.Search(9));
            Assert.Equal(8, tree.Search(8));
        }
        
        
        [Fact]
        public void Test2()
        {
            var tree = new BPlusTreeSimple<long>(3, Comparer<long>.Default);
            
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

            Assert.Equal(1, tree.Search(1));
            Assert.Equal(2, tree.Search(2));
            Assert.Equal(3, tree.Search(3));
            Assert.Equal(4, tree.Search(4));
            Assert.Equal(9, tree.Search(9));
            Assert.Equal(8, tree.Search(8));
        }
    }
}