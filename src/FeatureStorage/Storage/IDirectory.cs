namespace FeatureStorage.Storage;

public interface IDirectory : IResource
{
    ValueTask<IFile?> GetFile(string name, CancellationToken token);
    
    IFile? GetFile(string name);
    
    ValueTask<IFile> CreateFile(string name, CancellationToken token);

    IFile CreateFile(string name);

    ValueTask<IDirectory> CreateDirectory(string name, CancellationToken token);
    
    IDirectory? GetDirectory(string name);

    /// <summary>
    /// Create directory with name or existed directory
    /// </summary>
    /// <param name="name">Directory name</param>
    /// <param name="token"></param>
    /// <returns>
    /// Returns created or existed directory
    /// </returns>
    ValueTask<IDirectory?> GetDirectory(string name, CancellationToken token);
    
    /// <summary>
    /// Create directory with name or existed directory
    /// </summary>
    /// <param name="name">Directory name</param>
    /// <returns>
    /// Returns created or existed directory
    /// </returns>
    IDirectory CreateDirectory(string name);

    IEnumerable<IDirectory> GetDirectories();
    
    ValueTask<IEnumerable<IDirectory>> GetDirectories(CancellationToken token);
    
    IEnumerable<IFile> GetFiles();
    
    ValueTask<IEnumerable<IFile>> GetFiles(CancellationToken token);
}