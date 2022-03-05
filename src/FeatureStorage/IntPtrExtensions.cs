using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FeatureStorage;

public static class IntPtrExtensions
{
    public static int GetOffset(this IntPtr left, IntPtr right)
    {
        var offset = GetLongOffset(left, right);

        Debug.Assert(offset is > int.MinValue and < int.MaxValue);

        return (int)offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetLongOffset(this IntPtr left, IntPtr right)
    {
        return right.ToInt64() - left.ToInt64();
    }
}