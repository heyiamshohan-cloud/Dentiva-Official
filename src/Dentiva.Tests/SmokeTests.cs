using Dentiva.Core;
using Xunit;

namespace Dentiva.Tests;

public class SmokeTests
{
    [Fact]
    public void Guard_rejects_null_references()
    {
        Assert.Throws<ArgumentNullException>(() => Guard.NotNull((string?)null));
    }

    [Fact]
    public void Guard_rejects_blank_strings()
    {
        Assert.Throws<ArgumentException>(() => Guard.NotNullOrWhiteSpace("   "));
    }
}
