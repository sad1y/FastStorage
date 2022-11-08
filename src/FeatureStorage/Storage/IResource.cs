namespace FeatureStorage.Storage;

public interface IResource
{
    string Name { get; }
    
    ValueTask Delete(CancellationToken token);
    
    void Delete();
}