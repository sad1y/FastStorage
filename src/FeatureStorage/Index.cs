
using FeatureStorage.Storage;

namespace FeatureStorage;

public interface IIndex<in TKey>
{
    bool TryGetValue(TKey key, out long ptr);

    void Update(TKey key, long address);
    
    int Count { get; }

    void Serialize(IDirectory directory);
    
    void Deserialize(IDirectory directory);
}