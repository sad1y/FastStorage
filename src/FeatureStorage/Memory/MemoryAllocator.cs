namespace FeatureStorage.Memory;

public abstract class MemoryAllocator
{
    /// <summary>
    /// allocate memory for requested size
    /// </summary>
    /// <param name="size"></param>
    /// <returns></returns>
    public abstract IntPtr Allocate(int size);

    /// <summary>
    /// free allocated memory 
    /// </summary>
    /// <param name="memory"></param>
    public abstract void Free(IntPtr memory);
}