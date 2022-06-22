namespace FeatureStorage;

public abstract class Index<TKey> : IEnumerable<KeyValuePair<TKey, long>>
{
    public bool TryGetValue(TKey key, out long ptr) 
    {
        
    }

    public void Update(TKey key, long ptr)
    {
        
    }
}