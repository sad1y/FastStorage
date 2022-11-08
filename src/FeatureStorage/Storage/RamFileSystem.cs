namespace FeatureStorage.Storage;

/// <summary>
/// for test purpose only
/// </summary>
public class RamDirectory : IDirectory
{
    private readonly RamDirectory? _parent;
    private readonly List<IResource> _children;
    public string Name { get; }
    public bool IsDeleted { get; private set; }

    public IEnumerable<IResource> Children => _children;

    public RamDirectory(string name, RamDirectory parent = null)
    {
        Name = name;
        _children = new List<IResource>();
        _parent = parent;
    }

    public void Delete()
    {
        IsDeleted = true;
        _parent?.RemoveChild(this);
    }

    public ValueTask Delete(CancellationToken token)
    {
        Delete();
        return new ValueTask(Task.CompletedTask);
    }

    public ValueTask<IFile?> GetFile(string name, CancellationToken token) => new(GetFile(name));

    public IFile? GetFile(string name)
    {
        var res = Children.FirstOrDefault(f => f is RamFile { IsDeleted: false } file && file.Name == name);
        return res as IFile;
    }

    public ValueTask<IFile> CreateFile(string name, CancellationToken token) => new(CreateFile(name));

    public IFile CreateFile(string name)
    {
        if (_children.Any(f => f.Name == name))
            throw new IOException($"Resource with name {name} already exists");

        var file = new RamFile(this, name);
        _children.Add(file);
        return file;
    }

    public ValueTask<IDirectory?> GetDirectory(string name, CancellationToken token) => new(GetDirectory(name));

    public IDirectory CreateDirectory(string name)
    {
        if (_children.Any(f => f.Name == name))
            throw new IOException($"Resource with name {name} already exists");

        var dir = new RamDirectory(name, _parent);
        _children.Add(dir);
        return dir;
    }

    public ValueTask<IDirectory> CreateDirectory(string name, CancellationToken token) => new(CreateDirectory(name));

    public IDirectory? GetDirectory(string name)
    {
        var res = Children.FirstOrDefault(f => f is RamDirectory { IsDeleted: false } file && file.Name == name);
        return res as IDirectory;
    }

    internal void RemoveChild(IResource resource) => _children.Remove(resource);
}