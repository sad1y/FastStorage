namespace FeatureStorage;

public interface IPairFeatureCodec<T> where T : unmanaged
{
    bool TryEncode(ref PairFeatureBlock<T> pairFeatureBlock, int featureCount, Span<byte> dest, out int written);
    bool TryDecode(ReadOnlySpan<byte> src, int featureCount, out PairFeatureBlock<T> pairFeatureBlock, out int read);
    byte Version { get; }
    int MetaSize { get; }
}