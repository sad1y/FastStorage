namespace FeatureStorage.Extensions;

public static class StreamExtensions
{
    public static void ReadExact(this Stream stream, byte[] buffer, int offset, int count)
    {
        var left = count;

        while (left != 0)
        {
            var read = stream.Read(buffer, offset + (count - left), left);

            if (read == 0)
                throw new IOException("Stream doesn't contain enough data");
            
            left -= read;
        }
    }
}