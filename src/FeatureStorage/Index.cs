
namespace FeatureStorage;

public interface IIndex<TKey> : IEnumerable<KeyValuePair<TKey, long>>
{
    bool TryGetValue(TKey key, out long ptr);

    void Update(TKey key, long ptr);
}