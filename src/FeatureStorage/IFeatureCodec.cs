namespace FeatureStorage;

public interface IFeatureCodec
{
    bool TryEncode(ReadOnlySpan<float> features, Span<byte> dest, out int written);
    bool TryDecode(ReadOnlySpan<byte> src, Span<float> features, out int read);
}