namespace FeatureStorage.Storage;

public class LocalDirectory : IDirectory
{
    private readonly string _path;

    public LocalDirectory(string path)
    {
        if (!Directory.Exists(path))
            throw new IOException($"Directory `{path}` does not exists");

        _path = path;
    }

    public string FullPath => _path;
    
    public string Name => Path.GetDirectoryName(_path)!;

    public ValueTask Delete(CancellationToken token)
    {
        Delete();
        return new ValueTask();
    }

    public void Delete() => Directory.Delete(_path);

    public ValueTask<IFile?> GetFile(string name, CancellationToken token) => new(GetFile(name));

    public IFile? GetFile(string name)
    {
        var path = Path.Combine(_path, name);
        return File.Exists(path) ? new LocalFile(path) : null;
    }

    public ValueTask<IFile> CreateFile(string name, CancellationToken token) => new(CreateFile(name));

    public IFile CreateFile(string name)
    {
        var path = Path.Combine(_path, name);
        using var _ = File.Create(path);
        return new LocalFile(path);
    }

    public ValueTask<IDirectory> CreateDirectory(string name, CancellationToken token) => new(CreateDirectory(name));

    public IDirectory? GetDirectory(string name)
    {
        var path = Path.Combine(_path, name);
        return Directory.Exists(path) ? new LocalDirectory(path) : null;
    }

    public ValueTask<IDirectory?> GetDirectory(string name, CancellationToken token) => new(GetDirectory(name));

    public IDirectory CreateDirectory(string name)
    {
        var path = Path.Combine(_path, name);
        Directory.CreateDirectory(path);
        return new LocalDirectory(path);
    }

    public IEnumerable<IDirectory> GetDirectories()
    {
        return Directory
            .EnumerateDirectories(_path)
            .Select(f => new LocalDirectory(Path.Combine(_path, f)));
    }

    public ValueTask<IEnumerable<IDirectory>> GetDirectories(CancellationToken token) => new(GetDirectories());

    public IEnumerable<IFile> GetFiles()
    {
        return Directory
            .EnumerateFiles(_path)
            .Select(f => new LocalFile(Path.Combine(_path, f)));
    }

    public ValueTask<IEnumerable<IFile>> GetFiles(CancellationToken token) => new(GetFiles());
}