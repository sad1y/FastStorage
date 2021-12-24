// See https://aka.ms/new-console-template for more information

using FastStorage;

using var tree = new BPlusTree(nodeCapacity: 128, elementCount: 50_000_000);
            
tree.Insert(1, 1);
tree.Insert(3, 3);
tree.Insert(5, 5);
tree.Insert(7, 7);
tree.Insert(0, 0);
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