namespace FeatureStorage;

internal class Crc32
{
    private const uint Poly = 0xedb88320u;

    private static readonly uint[] Table = new uint[16 * 256];

    static Crc32()
    {
        for (uint i = 0; i < 256; i++)
        {
            var res = i;
            for (var t = 0; t < 16; t++)
            {
                for (var k = 0; k < 8; k++) res = (res & 1) == 1 ? Poly ^ (res >> 1) : (res >> 1);
                Table[(t * 256) + i] = res;
            }
        }
    }
    
    public static uint Append(ReadOnlySpan<byte> input, uint crc = 0)
    {
        var crcLocal = uint.MaxValue ^ crc;

        var table = Table;
        while (input.Length >= 16)
        {
            var a = table[(3 * 256) + input[12]]
                    ^ table[(2 * 256) + input[13]]
                    ^ table[(1 * 256) + input[14]]
                    ^ table[(0 * 256) + input[15]];

            var b = table[(7 * 256) + input[8]]
                    ^ table[(6 * 256) + input[9]]
                    ^ table[(5 * 256) + input[10]]
                    ^ table[(4 * 256) + input[11]];

            var c = table[(11 * 256) + input[4]]
                    ^ table[(10 * 256) + input[5]]
                    ^ table[(9 * 256) + input[6]]
                    ^ table[(8 * 256) + input[7]];

            var d = table[(15 * 256) + ((byte)crcLocal ^ input[0])]
                    ^ table[(14 * 256) + ((byte)(crcLocal >> 8) ^ input[1])]
                    ^ table[(13 * 256) + ((byte)(crcLocal >> 16) ^ input[2])]
                    ^ table[(12 * 256) + ((crcLocal >> 24) ^ input[3])];

            crcLocal = d ^ c ^ b ^ a;
            input = input[16..];
        }

        var i = 0;
        while (i < input.Length)
            crcLocal = table[(byte)(crcLocal ^ input[i++])] ^ crcLocal >> 8;

        return crcLocal ^ uint.MaxValue;
    }
}