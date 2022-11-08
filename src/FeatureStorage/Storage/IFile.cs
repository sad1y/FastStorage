namespace FeatureStorage.Storage;

public interface IFile : IResource
{
    Stream OpenRead();
    
    Stream OpenWrite();
}