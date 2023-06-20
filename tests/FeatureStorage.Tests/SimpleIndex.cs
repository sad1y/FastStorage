using System.Collections.Generic;
using System.Text.Json;
using FeatureStorage.Storage;

namespace FeatureStorage.Tests;

public class SimpleIndex : IIndex<string>
{
    private Dictionary<string, long> _index = new();

    public bool TryGetValue(string key, out long ptr) => _index.TryGetValue(key, out ptr);

    public void Update(string key, long ptr)
    {
        _index[key] = ptr;
    }

    public int Count => _index.Count;

    private const string FileName = "index";

    public void Serialize(IDirectory directory)
    {
        var file = directory.CreateFile(FileName);
        using var stream = file.OpenWrite();

        JsonSerializer.Serialize(stream, _index);

        stream.Flush();
    }

    public void Deserialize(IDirectory directory)
    {
        var file = directory.GetFile(FileName);
        using var stream = file.OpenRead();

        _index = JsonSerializer.Deserialize<Dictionary<string, long>>(stream)!;
    }
}