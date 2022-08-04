namespace FeatureStorage.Extensions;

internal static class NumberExtensions
{
    public static double InMegabytes(this long num)
    {
        return num / (1024D * 1024D);
    }

    public static double InMegabytes(this int num)
    {
        return ((long)num).InMegabytes();
    }

    public static int Megabytes(this int num)
    {
        return num * 1024 * 1024;
    }
}