namespace FastStorage;

public static class NumberExtensions
{
    public static double InMegabytes(this long num )
    {
        return num / (1024D * 1024D);
    }
}