namespace FeatureStorage;

public abstract class PairFeatureCodec<T> where T : unmanaged
{
    public abstract bool TryEncode(ref PairFeatureBlock<T> pairFeatureBlock, int featureCount, IntPtr dest, out int written);
    public abstract bool TryDecode(IntPtr src, int featureCount, out PairFeatureBlock<T> pairFeatureBlock, out int read);
    public abstract int GetVersion();
    public abstract ReadOnlySpan<byte> GetCompressedBlock(IntPtr ptr);
}