using System.Text;

namespace FeatureStorage.Containers;

internal class DotNotationWriter : IDisposable
{
    private readonly Stream _stream;

    public DotNotationWriter(Stream stream)
    {
        _stream = stream;
    }

    public void Write(ref BPlusTree.Node node)
    {
        using var writer = new StreamWriter(_stream, Encoding.UTF8);
        writer.WriteLine("```dot");
        writer.WriteLine("digraph bptree {");
        writer.WriteLine("rankdir=LR");

        var counter = 0;
        PrintLeaf(writer, "root", ref counter, ref node);

        writer.WriteLine("}");
        writer.Write("```");
    }

    private static void PrintLeaf(TextWriter writer, string nodeName, ref int counter, ref BPlusTree.Node node)
    {
        if (node.IsLeaf)
        {
            for (var i = 0; i < node._size; i++)
            {
                var innerNodeName = $"{node.GetLeaf(i).Key}";
                writer.WriteLine($"{nodeName} -> {innerNodeName}");
            }
        }

        else
        {
            var leftNodeName = $"NODE_{counter++}";
            writer.WriteLine($"{nodeName} -> {leftNodeName} [ label = \"<{node.GetNodeRef(0).Key}\"];");

            PrintLeaf(writer, leftNodeName, ref counter, ref node.LeftNode());

            for (var i = 0; i < node._size; i++)
            {
                var innerNodeName = $"NODE_{counter++}";
                writer.WriteLine($"{nodeName} -> {innerNodeName} [ label = \">{node.GetNodeRef(i).Key}\"];");
                PrintLeaf(writer, innerNodeName, ref counter, ref node.RightNode(ref node.GetNodeRef(i)));
            }
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}