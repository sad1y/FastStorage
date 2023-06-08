using System.Collections;
using System.Collections.Generic;

namespace FeatureStorage.Tests;

public class SimpleIndex<T> : IIndex<T> where T : unmanaged
{
    private readonly Dictionary<T, long> _index = new();
    public IEnumerator<KeyValuePair<T, long>> GetEnumerator() => _index.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool TryGetValue(T key, out long ptr) => _index.TryGetValue(key, out ptr);

    public void Update(T key, long ptr)
    {
        _index[key] = ptr;
    }

    public int Count => _index.Count;
}