using System.Diagnostics;

namespace FastStorage;

public static class IntPtrExtensions
{
    public static int GetOffset(this IntPtr left, IntPtr right)
    {
        var offset = right.ToInt64() - left.ToInt64();

        Debug.Assert(offset is > int.MinValue and < int.MaxValue);

        return (int)offset;
    }
}