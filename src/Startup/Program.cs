// See https://aka.ms/new-console-template for more information


using FeatureStorage;

// using var tree = new BPlusTree(nodeCapacity: 128, elementCount: 50_000_000);
//             
// tree.Insert(1, 1);
// tree.Insert(3, 3);
// tree.Insert(5, 5);
// tree.Insert(7, 7);
// tree.Insert(0, 0);
// tree.Insert(9, 9);
// tree.Insert(2, 2);
// tree.Insert(4, 4);
// tree.Insert(6, 6);
// tree.Insert(8, 8);
// tree.Insert(10, 10);
//             
// tree.Insert(13, 13);
// tree.Insert(18, 18);
// tree.Insert(11, 11);
// tree.Insert(12, 12);
// tree.Insert(19, 19);
// tree.Insert(15, 15);
// tree.Insert(16, 16);
// tree.Insert(17, 17);
// tree.Insert(14, 14);
// tree.Insert(20, 20);
//             
// tree.Insert(25, 25);
// tree.Insert(30, 30);
// tree.Insert(42, 42);
// tree.Insert(41, 41);
// tree.Insert(21, 21);
//             
// tree.Insert(23, 23);
// tree.Insert(24, 24);
// tree.Insert(27, 27);
//             
// tree.Insert(33, 33);
// tree.Insert(66, 66);
//             
//
//             
// File.Delete("/tmp/bptree.dot");
// using var fs = File.OpenWrite("/tmp/bptree.dot");
// tree.PrintAsDot(fs);

// See https://aka.ms/new-console-template for more information
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;

public class Program
{
    public static unsafe void Main(string[] args)
    {
        const int featureCount = 173;

        var f = 14f;
        var h_f = (Half)f;
        var u_f = (float)h_f;
        
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteHalfLittleEndian(buffer, h_f);
        var r_h_f = BinaryPrimitives.ReadHalfLittleEndian(buffer);
        //     

        var metrics = Utils.CalculateMetrics(featureCount, args[0]);

        // Compress(featureCount, medians, args[0]);

        var metric = new Metric();

        // var data = new float[]
        // {
        //     70, 19, 2, -30, 1, 1, 40, 2, 3, 4, 0, 5, -31, 10, 8, -2, -3, -5, 6, 1, 3, -90, 
        //     2, 2, 2, 2, 2, 2, 2,
        //     2, 2, 2, 2, -1
        // };
        var data = new float[]
        {
            0.13314172f, 0.03942045f, -0.02525475f, -0.01029232f, 0.0234972f, 0.1895888f,
            0.05434934f, 0.157929f, -0.10757348f, -0.06608775f, -0.06454635f, 0.00581403f,
            0.0885079f, -0.03160929f, -0.09212804f, -0.00596565f, 0.07094702f, 0.17584698f,
            0.17342353f, 0.00044307f, 0.03058632f, 0.0521539f, -0.04180727f, -0.0613966f,
            -0.00986443f, 0.14523739f, 0.2544651f, -0.17016642f, -0.05261561f, 0.07056217f,
            -0.1419254f, 0.00870412f, 0.02517105f, 0.17069364f, -0.12384655f, 0.13734154f,
            -0.08841279f, 0.02531786f, -0.00209919f, 0.09422173f, -0.08524741f, 0.02062372f,
            0.05242782f, 0.11132088f, -0.22117073f, -0.02589406f, -0.03071631f, -0.03945543f,
            0.03489581f, 0.00534292f, 0.03079331f, -0.01438953f, 0.08779292f, -0.09378703f,
            -0.02524425f, -0.11677485f, -0.1561607f, 0.0181948f, 0.16798968f, -0.00094636f,
            -0.00054045f, 0.06140958f, -0.10585419f, -0.025873f, -0.12674138f, -0.03786271f,
            0.01339642f, 0.05749667f, -0.11067462f, 0.06433123f, 0.17338282f, 0.12804518f,
            -0.10880235f, -0.06595302f, 0.09041253f, -0.1149877f, -0.0350382f, -0.0008969f,
            -0.07902434f, -0.10987765f, 0.06916858f, -0.00435579f, 0.13469571f, -0.10671673f,
            0.03783854f, 0.01912089f, -0.10147078f, -0.15511576f, 0.07331976f, -0.00845915f,
            0.16865129f, 0.07212817f, -0.16695551f, -0.03520342f, 0.13777626f, -0.20608775f,
            -0.016908f, -0.02062988f, 0.17420155f, -0.013626f,
        };

        using var origin = File.OpenWrite("/tmp/features.bin");
        fixed (void* ptr = data)
        {
            origin.Write(new Span<byte>(ptr, data.Length * sizeof(float)));
        }

        origin.Flush();


        // var blob = new byte[featureCount * sizeof(float)];

        var state = new State
        {
            Count = 0,
            CompressionRate = 0,
            CompressionRateWithZstd = 0
        };

        Utils.Iterate(args[0], featureCount, (ref Entry entry) =>
        {
            Span<byte> buffer = stackalloc byte[700];
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var written = Compress(entry.Features, buffer, metrics, -1f);
            stopwatch.Stop();

            // var compressTime = stopwatch.Elapsed;

            // Span<byte> zstdBuffer = stackalloc byte[featureCount * sizeof(float)];
            // using var compressor = new Compressor();
            //
            // stopwatch.Restart();
            // var compressedData = compressor.Wrap(buffer, zstdBuffer);
            // var zstdTime = stopwatch.Elapsed;
            //
            Span<float> decompressed = stackalloc float[featureCount];
            stopwatch.Restart();
            var read = Decompress(buffer, decompressed, metrics, -1f);
            var decompressTime = stopwatch.Elapsed;

            state.CompressionRate = (state.Count * state.CompressionRate + (buffer.Length / (float)written)) / (state.Count + 1);
            // state.CompressionRateWithZstd = (state.Count * state.CompressionRateWithZstd + buffer.Length / (float)compressedData) / (state.Count + 1);
            state.Count++;
            state.DecompressTime += decompressTime;


            // Console.WriteLine("rate: {0:F}. zstd: {1:F}", buffer.Length / written, buffer.Length / compressedData);
            // Console.WriteLine("timing: \tcompress: {0:c}, \tzstd: {1:c}, \tdecompress: {2:c}", compressTime, zstdTime, decompressTime);

            for (var i = 0; i < featureCount; i++)
            {
                if (Math.Abs(entry.Features[i] - decompressed[i]) > 0.01)
                {
                    Console.WriteLine("{4}\t{3}:\t {0}, {1}, diff {2:F}", entry.Features[i], decompressed[i],
                        entry.Features[i] - decompressed[i],
                        i, state.Count + 1);
                    state.ErrorCount++;
                }
            }


            // foreach (var e in entry.Features)
            // {
            //     Console.Write("{0}, ", e);
            // }
            //
            // Console.WriteLine();
            // return;
            // var source = new byte[entry.Features.Length * sizeof(float)];
            // var destLen = entry.Features.Length * sizeof(float);
            // var dest = stackalloc byte[destLen];
            // fixed (void* srcPtr = entry.Features)
            // {
            //     var encodedLength = LZ4Codec.Encode(
            //         (byte*)srcPtr, entry.Features.Length * sizeof(float),
            //         dest, destLen, LZ4Level.L12_MAX);
            //
            //     var buffer = stackalloc byte[entry.Features.Length * sizeof(float) * 10];
            //     var decoded = LZ4Codec.Decode(dest, encodedLength, 
            //         buffer, entry.Features.Length * sizeof(float) * 10);
            //
            //     Span<float> decompressed = new Span<float>(buffer, entry.Features.Length);
            // }


            // Span<byte> compressed = stackalloc byte[entry.Features.Length * sizeof(float)];
            //
            // var size = ZfpNative.Compress(entry.Features, compressed, 1e-2);
            // unsafe
            // {
            //     Span<float> uncompressed = new float[entry.Features.Length];
            //     fixed (void* ptr = uncompressed)
            //     {
            //         var span = new Span<byte>(ptr, sizeof(float) * uncompressed.Length);
            //         ZfpNative.Decompress(compressed[..(int)size], span, out _, out var cnt);
            //         
            //     }
            // }
        }, 1000_000);

        Console.WriteLine("state: {0}, {4} {1:F}, {2:F}, {3:c}", state.Count, state.CompressionRate, state.CompressionRateWithZstd,
            state.DecompressTime, state.ErrorCount);

        return;


        using var file = File.OpenWrite("/tmp/features.zfp");
        // file.Write(blob[..(int)size]);
        // file.Flush();


        return;
        // for (int i = 0; i < data.Length; i++)
        // {
        //     metric.Update(data[i]);
        // }
        //
        //
        // // Console.WriteLine("variance: {0}, mean: {1}, median: {2}", metric.Variance, metric.Mean, metric.Median);
        //
        //
        // var range = (Math.Sqrt(metric.Variance) * 3);
        // // var median = metric.Median;
        // // var min = median - range;
        // // var max = median + range;
        //
        // var compressed = new byte[data.Length];
        // for (int i = 0; i < data.Length; i++)
        // {
        //     var val = (data[i] - metric.Mean);
        //
        //     // var stdCount = (val * val) / metric.Variance;
        //     var stdCount = val / Math.Sqrt(metric.Variance);
        //     if (stdCount is < -3 or > 3f)
        //     {
        //         Console.WriteLine("{0} is outliner. std_count {1}", data[i], stdCount);
        //         compressed[i] = (byte)(stdCount > 0 ? 127 : -128);
        //     }
        //     else
        //     {
        //         var norm = (data[i] - median) / range;
        //         Console.WriteLine("normalization {0} for {1}", norm, data[i]);
        //         compressed[i] = (byte)(sbyte)(norm * 128f);
        //     }
        // }
        //
        // var ratio = Math.Pow(2, -7f);
        // for (int i = 0; i < compressed.Length; i++)
        // {
        //     var val = (sbyte)compressed[i];
        //     var decompressed = (val * ratio) * range + median;
        //     Console.WriteLine("decopressed {0}, origin: {1}", decompressed, data[i]);
        // }


        // if not - 3 <= (s - stream.mean) / np.sqrt(stream.variance) <= 3:
        // print(i, s)

        // var values = new HashSet<int>();
        //
        // GenerateDataSet(values, 273190820);        
        // Console.WriteLine("data set generation done");


        // foreach (var v in values)
        // {
        //     med.Put(v);
        //     // Console.Write($"{v} ");
        // }

        // Console.WriteLine("-");
        // Console.WriteLine(med.Get());
    }

    private const float MaxVariance = 8f;
    private const float MaxRange = sbyte.MaxValue;

    private static int Compress(ReadOnlySpan<float> src, Span<byte> dest, Metric[] metrics, float defaultValue)
    {
        var maskSize = (int)Math.Ceiling(src.Length / 8f) + 1; // bits per byte, plus size of large values

        for (var i = 0; i < maskSize; i++)
        {
            dest[i] = 0;
        }

        var j = 0;

        Span<byte> largeValueBuffer = stackalloc byte[src.Length * 2];
        var largeValueCount = 0;

        for (var i = 0; i < src.Length; i++)
        {
            // update mask if it is default value
            if (Math.Abs(src[i] - defaultValue) < 0.0001)
            {
                var mask = dest[i >> 3];
                var offset = i - (i >> 3) * 8;
                mask = (byte)(mask | (1 << offset));
                dest[i >> 3] = mask;
            }
            else
            {
                // sqrt((x/8))*127
                // var val = src[i] / metrics[i].Variance;
                var val = src[i] * 4;
                var sign = (byte)(BitConverter.DoubleToInt64Bits(val) >> 63);
                val = Math.Max(val, -val); // rid of sign
                var factor = Math.Round(Math.Sqrt(val / MaxVariance) * MaxRange);
                var boundedFactor = (byte)factor;

                if (factor > MaxRange - 1)
                {
                    // put as two byte value
                    BinaryPrimitives.WriteHalfLittleEndian(largeValueBuffer[(largeValueCount * 2)..], (Half)src[i]);
                    largeValueCount++;
                    boundedFactor = byte.MaxValue;
                }

                // factor = Math.Min(factor, (byte)MaxRange);
                boundedFactor = (byte)(boundedFactor | (0x80 & sign)); // store sign
                dest[maskSize + j++] = boundedFactor;
            }
        }

        dest[maskSize - 1] = (byte)(j + maskSize);
        largeValueBuffer[..(largeValueCount * 2)].CopyTo(dest[(maskSize + j)..]);

        return maskSize + j + largeValueCount * 2;
    }

    private static int Decompress(ReadOnlySpan<byte> src, Span<float> dest, Metric[] metrics, float defaultValue)
    {
        var maskSize = (int)Math.Ceiling(dest.Length / 8f) + 1;
        var largeValueOffset = src[maskSize - 1];
        var j = 0;

        for (var i = 0; i < dest.Length; i++)
        {
            var mask = src[i >> 3];
            var offset = 1 << (i - (i >> 3) * 8);

            if ((offset & mask) == offset)
            {
                dest[i] = defaultValue;
                continue;
            }
            
            // if that value is large
            if (src[maskSize + j] == byte.MaxValue)
            {
                var buffer = src[largeValueOffset..];
                var val = BinaryPrimitives.ReadHalfLittleEndian(buffer);
                dest[i] = (float)val;
                largeValueOffset += 2;
            }
            else
            {
                // 8*(x/127)^2
                var factor = src[maskSize + j];
                var sign = (factor & 0x80) == 0x80 ? -1 : 1;
                factor = (byte)(factor & sbyte.MaxValue);

                var norm = (factor / MaxRange);
                var varianceRatio = MaxVariance * (norm * norm);
                // var val = sign * metrics[i].Variance * varianceRatio;
                var val = sign * varianceRatio * 0.25;
                dest[i] = (float)val;
            }
            j++;
        }

        return maskSize + j;
    }


    private static void Compress(int featuresCount, float[] medians, string path)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);

        try
        {
            // using var src = File.OpenRead(path);
            using var dest = File.OpenWrite(path + ".compressed");
            // var reader = new EntryReader(src, new EntrySchema(featuresCount));
            //
            // var entry = new Entry
            // {
            //     Features = new float[featuresCount]
            // };

            var offset = 0;

            // (Stream, int, byte[]) state = new(dest, offset, buffer);
            //
            // static void IterateAction((Stream, int, ) state, ref Entry entry)
            // {
            //     {
            //         BinaryPrimitives.WriteInt64LittleEndian(new Span<byte>(state.buffer, offset, 8), entry.ItemId);
            //         offset += 8;
            //     
            //         for (var i = 0; i < entry.Features.Length; i++)
            //         {
            //             var compressed = entry.Features[i] / (medians[i] == 0 ? 0e5 : medians[i]);
            //             Debug.Assert(compressed is > sbyte.MinValue and < sbyte.MaxValue);
            //             buffer[offset++] = (byte)(sbyte)compressed;
            //
            //             if (offset == buffer.Length)
            //             {
            //                 dest.Write(buffer, 0, buffer.Length);
            //                 offset = 0;
            //             }
            //         }
            //     }
            // }

            Utils.Iterate(path, featuresCount,
                (ref Entry entry) =>
                {
                    BinaryPrimitives.WriteInt64LittleEndian(new Span<byte>(buffer, offset, 8), entry.ItemId);
                    offset += 8;

                    for (var i = 0; i < entry.Features.Length; i++)
                    {
                        var compressed = entry.Features[i] / (medians[i] == 0 ? 0e5 : medians[i]);
                        Debug.Assert(compressed is > sbyte.MinValue and < sbyte.MaxValue);
                        buffer[offset++] = (byte)(sbyte)compressed;

                        if (offset == buffer.Length)
                        {
                            dest.Write(buffer, 0, buffer.Length);
                            offset = 0;
                        }
                    }

                    if (offset > 0)
                    {
                        dest.Write(buffer, 0, offset);
                    }
                });
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}

public class Metric
{
    public float Min;
    public float Max;

    // private readonly Median _median;
    private double _variance;
    private double _mean;
    private int _count;

    public Metric(double variance = 0, double mean = 0)
    {
        Min = float.MaxValue;
        Max = float.MinValue;
        _variance = variance;
        _mean = mean;
        // _median = new Median(256);
    }

    public double Mean => _mean;

    public double Variance => _variance;

    // public float Median => _median.Get();

    public void Update(float val)
    {
        // _median.Update(val);
        Max = Math.Max(Max, val);
        Min = Math.Min(Min, val);

        // self.variance = ((self.variance + self.mean ** 2) * self.n_elements + element ** 2) / (self.n_elements + 1)
        _variance = ((_variance + _mean * _mean) * _count + val * val) / (_count + 1);

        // self.mean = ((self.mean * self.n_elements) + element) / (self.n_elements + 1)
        _mean = (_mean * _count + val) / (_count + 1);
        // self.variance = self.variance - self.mean ** 2
        _variance -= _mean * _mean;
        // self.n_elements += 1
        _count += 1;
    }
}

public class StreamingMean
{
    private double _result;
    private int _count;


    public StreamingMean()
    {
        _result = 0;
        _count = 0;
    }

    public void Update(float val)
    {
        _result = (_result * _count + val) / (_count + 1);
        _count += 1;
    }

    public double Value => _result;
}

public class Utils
{
    public delegate void IterateAction( /*T state,*/ ref Entry entry);

    public static void Iterate(string path, int featureCount, /*T state,*/ IterateAction action, int? count = null)
    {
        using var src = File.OpenRead(path);
        var reader = new EntryReader(src, new EntrySchema(featureCount));

        var entry = new Entry
        {
            Features = new float[featureCount]
        };

        var i = 0;
        while (reader.ReadNext(ref entry))
        {
            action(ref entry);
            i++;
            if (count == i)
                break;
        }
    }

    public static Metric[] CalculateMetrics(int featuresCount, string path)
    {
        var metricPath = path + ".metric";
        var metrics = new Metric[featuresCount];
        if (File.Exists(metricPath))
        {
            using var file = File.OpenRead(metricPath);
            Span<byte> buffer = stackalloc byte[16];

            for (var i = 0; i < featuresCount; i++)
            {
                file.Read(buffer);

                var mean = BinaryPrimitives.ReadDoubleLittleEndian(buffer);
                var variance = BinaryPrimitives.ReadDoubleLittleEndian(buffer[8..]);
                metrics[i] = new Metric(variance, mean);
            }

            return metrics;
        }

        // var medians = new Median[featuresCount];
        // var means = new StreamingMean[featuresCount];

        for (var i = 0; i < metrics.Length; i++)
        {
            // medians[i] = new Median(256);
            metrics[i] = new Metric();
        }

        // var entry = new Entry
        // {
        //     Features = new float[featuresCount]
        // };

        // calculate medians for each features
        // using (var file = File.OpenRead(path))
        // {
        //     var reader = new EntryReader(file, new EntrySchema(featuresCount));
        //
        //     while (reader.ReadNext(ref entry))
        //     {
        //         for (var i = 0; i < medians.Length; i++)
        //         {
        //             medians[i].Put(entry.Features[i]);
        //         }
        //     }
        // }

        var count = 0;

        Iterate(path, featuresCount, (ref Entry entry) =>
        {
            for (var i = 0; i < featuresCount; i++)
            {
                metrics[i].Update(entry.Features[i]);
            }

            count++;
        });


        using var cache = File.OpenWrite(metricPath);
        Span<byte> writeBuffer = stackalloc byte[16];
        for (var i = 0; i < metrics.Length; i++)
        {
            BinaryPrimitives.WriteDoubleLittleEndian(writeBuffer, metrics[i].Mean);
            BinaryPrimitives.WriteDoubleLittleEndian(writeBuffer[8..], metrics[i].Variance);
            cache.Write(writeBuffer);
        }

        cache.Flush();

        return metrics;
    }


//     private static void GenerateDataSet(HashSet<int> dataSet, int seed = 0)
//     {
//         if (seed == 0)
//         {
//             seed = Environment.TickCount;
//             const int count = 60_000_000;
//             Console.WriteLine($"seed {seed}");
//             var rng = new Random(seed);
//             using var fs = File.Create($"seed_{seed}");
//             Span<byte> buffer = stackalloc byte[4];
//             while (dataSet.Count != count)
//             {
//                 var next = rng.Next(0, count);
//                 if (dataSet.Add(next))
//                 {
//                     BinaryPrimitives.WriteInt32LittleEndian(buffer, next);
//                     fs.Write(buffer);
//
//                     if (dataSet.Count % 1_000_000 == 0)
//                         Console.WriteLine("generated {0}", dataSet.Count);
//                 }
//             }
//         }
//         else
//         {
//             using var fs = File.OpenRead($"seed_{seed}");
//             Span<byte> buffer = stackalloc byte[4];
//             while (fs.Read(buffer) == sizeof(int))
//             {
//                 var val = BinaryPrimitives.ReadInt32LittleEndian(buffer);
//                 dataSet.Add(val);
//             }
//
//             Console.WriteLine("read {0} records", dataSet.Count);
//         }
//     }
// }
}

public static class NumberExtensions
{
    public static bool IsPowerOfTwo(this uint x)
    {
        return (x != 0) && ((x & (x - 1)) == 0);
    }
}

public class Median
{
    private readonly int _bucketSize;
    private readonly float[][] _buckets;
    private int _pointer;
    private int _bucket;


    public Median(uint bucketSize = 8)
    {
        if (bucketSize <= 7) throw new ArgumentOutOfRangeException(nameof(bucketSize));
        if (!bucketSize.IsPowerOfTwo())
            throw new ArgumentOutOfRangeException(nameof(bucketSize), "should be a multiple by 2");

        _bucketSize = (int)bucketSize;
        _buckets = new float[bucketSize >> 1][];

        for (var i = 0; i < _buckets.Length; i++)
        {
            _buckets[i] = new float[bucketSize];
        }
    }

    public void Update(float val)
    {
        if (_pointer == _bucketSize)
        {
            _bucket++;
            _pointer = 0;
        }

        if (_bucket == _buckets.Length && _pointer == 0)
        {
            _bucket = _buckets.Length >> 1; // reset for next value
            var halfSize = _bucketSize >> 1;
            var offset = halfSize >> 1;

            for (var i = 0; i < _buckets.Length; i++)
            {
                Array.Sort(_buckets[i]);
                // var mid = new Span<float>(_buckets[i], offset, halfSize);
                var destBucket = _buckets[i >> 1];
                var srcBucket = _buckets[i];
                var start = (i & 1) == 0 ? 0 : halfSize;

                // copy half of src bucket into previous bucket
                for (var j = 0; j < halfSize; j++)
                {
                    destBucket[start + j] = srcBucket[offset + j];
                }
            }
        }

        _buckets[_bucket][_pointer++] = val;
    }

    public float Get()
    {
        var size = _bucketSize * _bucket + _pointer;
        Span<float> temp = stackalloc float[size];

        for (var i = 0; i < _bucket; i++)
            _buckets[i].CopyTo(temp.Slice(i * _bucketSize, _bucketSize));

        var lastBucket = new Span<float>(_buckets[_bucket], 0, _pointer);
        lastBucket.CopyTo(temp[((_bucket) * _bucketSize)..]);
        temp.Sort();

        if ((temp.Length & 1) == 1)
            return (temp[(temp.Length >> 1) - 1] + temp[temp.Length >> 1]) / 2;

        return temp[temp.Length >> 1];
    }

    // public void Put(float val)
    // {
    //     _left.Enqueue(val, val);
    //
    //     if (_left.Count - _right.Count > 1)
    //     {
    //         val = _left.Dequeue();
    //         _right.Enqueue(val, -1 * val);
    //     }
    //
    //     if (_left.TryPeek(out var maxLeft, out _)
    //         && _right.TryPeek(out var maxRight, out _)
    //         && maxLeft > maxRight)
    //     {
    //         val = _left.Dequeue(); 
    //         _right.Enqueue(val, -1 * val);
    //     }
    //
    //     if (_right.Count - _left.Count > 1)
    //     {
    //         val = _right.Dequeue();
    //         _left.Enqueue(val, val);
    //     }
    // }

    // public float Get()
    // {
    //     if (_right.Count == _left.Count)
    //     {
    //         if (_right.Count == 0)
    //             return 0;
    //         return (_right.Peek() + _left.Peek()) / 2;
    //     }
    //
    //     return _left.Count > _right.Count ? _left.Peek() : _right.Peek();
    // }
}

public struct Entry
{
    public long ItemId;
    public float[] Features;
}

public class EntrySchema
{
    public int FeatureSize { get; }

    public EntrySchema(int featureSize)
    {
        FeatureSize = featureSize;
    }
}

public class EntryReader
{
    private readonly Stream _stream;
    private readonly EntrySchema _schema;
    private readonly int _entrySize;

    public EntryReader(Stream stream, EntrySchema schema)
    {
        _stream = stream;
        _schema = schema;
        _entrySize = schema.FeatureSize * sizeof(float) + sizeof(long);
    }

    public bool ReadNext(ref Entry entry)
    {
        Span<byte> buffer = stackalloc byte[_entrySize];
        if (_stream.Read(buffer) != _entrySize)
        {
            return false;
        }

        entry.ItemId = BinaryPrimitives.ReadInt64LittleEndian(buffer);
        var offset = sizeof(long);

        for (var i = 0; _schema.FeatureSize > i; i++)
        {
            entry.Features[i] = BinaryPrimitives.ReadSingleLittleEndian(buffer[offset..]);
            offset += sizeof(float);
        }

        return true;
    }
}

class State
{
    public int Count;
    public float CompressionRate;
    public float CompressionRateWithZstd;
    public TimeSpan DecompressTime;
    public int ErrorCount;
}