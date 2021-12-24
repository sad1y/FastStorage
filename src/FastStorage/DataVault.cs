using System.Buffers;

namespace FastStorage;

public class DataVault
{
    private readonly uint _capacity;
    private readonly uint _elementSize;
    private readonly Dictionary<ulong, long> _index;

    public DataVault(uint capacity, uint elementSize)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (elementSize <= 0) throw new ArgumentOutOfRangeException(nameof(elementSize));
        
        _capacity = capacity;
        _elementSize = elementSize;

        _index = new Dictionary<ulong, long>((int)_capacity);
    }


    public class OptimizedShard
    {
        public static OptimizedShard From(UncommitedShard shard)
        {
            
        }
    }

    public OptimizedShard Compile()
    {
        var optimizedReader = new OptimizedShard();
        foreach (var (key, value) in _index)
        {
               
        }
    }

    public void Add(string key, ReadOnlySequence<byte> data)
    {
        
    }
    
    public void Add(uint key, ReadOnlySequence<byte> data)
    {
        // structure should look like this
        // - first int - space occupied by data
        // - blocks linked with each other
        // - last int - offset to next block or zero if it last block
        
        // var slot = GetVacantSlot();

        // var result = _index.Search(key);
        // if (result.Found)
        //     _data.InsertIntoBlock(result.Value, data);
        // else
        // {
        //     var offset = _data.CreateNewBlock();
        //     _index.Insert(key, offset);
        //     _data.InsertIntoBlock(offset, data);
        // }
    }
    
    
}