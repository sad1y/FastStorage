namespace FeatureStorage;

public interface IPairFeatureCodec<T> where T : unmanaged
{
    bool TryEncode(ref PairFeatureBlock<T> pairFeatureBlock, Span<byte> dest, out int written);
    bool TryDecode(ReadOnlySpan<byte> src, ref PairFeatureBlock<T> pairFeatureBlock, out int read);
    
    /// <summary>
    /// should return value that serves as checker for codec settings
    /// </summary>
    int Stamp { get; }
    
    /// <summary>
    /// should return overhead size
    /// </summary>
    int MetaSize { get; }
}