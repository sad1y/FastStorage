namespace FeatureStorage;

public interface IPairFeatureCodec<T> where T : unmanaged
{
    bool TryEncode(ref PairFeatureBlock<T> pairFeatureBlock, int featureCount, Span<byte> dest, out int written);
    bool TryDecode(ReadOnlySpan<byte> src, int featureCount, out PairFeatureBlock<T> pairFeatureBlock, out int read);
    
    /// <summary>
    /// should return value that serves as checker for codec settings
    /// </summary>
    int Stamp { get; }
    
    /// <summary>
    /// should return overhead size when data has been compressed  
    /// </summary>
    int MetaSize { get; }
}