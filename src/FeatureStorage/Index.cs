
namespace FeatureStorage;

public interface IIndex<TKey> : IEnumerable<KeyValuePair<TKey, long>>
{
    bool TryGetValue(TKey key, out long ptr);

    void Update(TKey key, long ptr);
    
    int Count { get; }

    bool TrySerialize(TKey key, Span<byte> buffer, out int written);
    bool TryDeserialize(ReadOnlySpan<byte> buffer, out TKey key, out int read);
}