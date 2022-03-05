namespace FeatureStorage;

public abstract class PairFeatureCodec<T> where T : unmanaged
{
    public abstract bool TryEncode(ref PairFeatureBlock<T> pairFeatureBlock, IntPtr dest, out int written);
    public abstract bool TryDecode(IntPtr src, out PairFeatureBlock<T> pairFeatureBlock, out int read);
}