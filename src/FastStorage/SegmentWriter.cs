using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FastStorage;

public class SegmentWriter : IDisposable
{
    private readonly int _featureSize;
    private readonly UnmanagedMemory _memory;
    private readonly IntPtr _base;

    public SegmentWriter(int featureSize, int capacity = 1024)
    {
        _featureSize = featureSize;
        _memory = new UnmanagedMemory(capacity * featureSize);
        _base = _memory.Allocate(0);
    }

    public unsafe long Add(ReadOnlySpan<byte> seq)
    {
        var ptr = _memory.Allocate(sizeof(Chain));
        ref var chain = ref *(Chain*)ptr;
        AddNode(ref chain, seq);
        return _base.GetLongOffset(ptr);
    }

    private unsafe void AddNode(ref Chain chain, ReadOnlySpan<byte> seq)
    {
        var ptr = _memory.Allocate(sizeof(Node) + seq.Length);
        var buffer = new Span<byte>((ptr + sizeof(Node)).ToPointer(), seq.Length);
        seq.CopyTo(buffer);
        var offset = _base.GetLongOffset(ptr);

        if (chain.Count != 0)
        {
            ref var latest = ref *(Node*)((byte*)_base.ToPointer() + chain.Tail);
            latest.Next = offset;
        }

        chain.Count += 1;
        chain.Tail = offset;
    }

    public void Extend(long offset, ReadOnlySpan<byte> seq)
    {
        ref var chain = ref GetChain(offset);
        AddNode(ref chain, seq);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe ref Chain GetChain(long offset) => ref *(Chain*)((byte*)_base.ToPointer() + offset);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void* GetChainNodePtr(long offset) => (byte*)_base.ToPointer() + offset + sizeof(Chain);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe ref Node GetChainNode(long offset) => ref *(Node*)GetChainNodePtr(offset);
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _memory.Dispose();
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct Chain
    {
        public long Tail;
        public ushort Count;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Node
    {
        public long Next;

        public bool IsFinal => Next == 0;
    }

    public class FeatureReader
    {
        private readonly FeatureWriter _writer;

        public FeatureReader(FeatureWriter writer)
        {
            _writer = writer;
        }

        // private void Optimize()
        // {
        //     
        //     _writer._base
        //     
        // }
        //
        // public unsafe ReadOnlySpan<byte> Read(long offset)
        // {
        //     ref var chain = ref GetChain(offset);
        //
        //     return new ReadOnlySpan<byte>(GetChainNodePtr(offset), chain.Size * _featureSize);
        // }
        //
        // private struct FeatureBlock
        // {
        //     public uint Count;
        // }
    }
}