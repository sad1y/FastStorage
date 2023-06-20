namespace FeatureStorage.Memory;

public readonly struct PinnedMemory
{
    public readonly IntPtr Ptr;
    public readonly long Address;

    public PinnedMemory(IntPtr ptr, long address)
    {
        Ptr = ptr;
        Address = address;
    }

    public long ToInt64() => Ptr.ToInt64();

    public static implicit operator IntPtr(PinnedMemory d) => d.Ptr;
    public static unsafe implicit operator void*(PinnedMemory d) => d.Ptr.ToPointer();

    public static IntPtr operator +(PinnedMemory a, int b) => a.Ptr + b;
    public static IntPtr operator -(PinnedMemory a, int b) => a.Ptr - b;

    public void Deconstruct(out IntPtr ptr, out long address)
    {
        ptr = Ptr;
        address = Address;
    }

    public unsafe Span<byte> AsSpan(int len)
    {
        return new Span<byte>(Ptr.ToPointer(), len);
    }
}