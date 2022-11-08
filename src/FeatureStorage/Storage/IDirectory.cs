namespace FeatureStorage.Storage;

public interface IDirectory : IResource
{
    ValueTask<IFile?> GetFile(string name, CancellationToken token);
    
    IFile? GetFile(string name);
    
    ValueTask<IFile> CreateFile(string name, CancellationToken token);

    IFile CreateFile(string name);

    ValueTask<IDirectory> CreateDirectory(string name, CancellationToken token);
    
    IDirectory? GetDirectory(string name);
    
    ValueTask<IDirectory?> GetDirectory(string name, CancellationToken token);
    
    IDirectory CreateDirectory(string name);

    IEnumerable<IResource> Children { get; }
    
    
}