using Xunit;

namespace FeatureStorage.Tests;

public class PairFeatureBlockTest
{
    [Fact]
    public void Release_ShouldDoNothing_IfNoMemoryWasAllocated()
    {
        var b = new PairFeatureBlock<long>();
        b.Release();
    }
}