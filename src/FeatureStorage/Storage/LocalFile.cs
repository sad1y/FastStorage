namespace FeatureStorage.Storage;

public class LocalFile : IFile
{
    private readonly string _path;

    public LocalFile(string path)
    {
        if (!File.Exists(path))
            throw new IOException($"File `{path}` doesn't exists");
        _path = path;
        
    }

    public string Name => Path.GetFileName(_path);
    
    public ValueTask Delete(CancellationToken token)
    {
        Delete();
        return new ValueTask();
    }

    public void Delete() => File.Delete(_path);

    public Stream OpenRead() => File.OpenRead(_path);

    public Stream OpenWrite() => File.OpenWrite(_path);
}