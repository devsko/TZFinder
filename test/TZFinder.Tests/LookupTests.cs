namespace TZFinder.Tests;

public static class LookupTests
{
    [Theory]
    [InlineData(0, "Etc/GMT")]
    [InlineData(.1f, "Etc/GMT")]
    [InlineData(-.1f, "Etc/GMT")]
    [InlineData(7.4f, "Etc/GMT")]
    [InlineData(-7.4f, "Etc/GMT")]
    [InlineData(7.6f, "Etc/GMT-1")]
    [InlineData(-7.6f, "Etc/GMT+1")]
    [InlineData(22.4f, "Etc/GMT-1")]
    [InlineData(-22.4f, "Etc/GMT+1")]
    [InlineData(22.6, "Etc/GMT-2")]
    [InlineData(-22.6, "Etc/GMT+2")]
    [InlineData(179.9f, "Etc/GMT-12")]
    [InlineData(-179.9f, "Etc/GMT+12")]
    [InlineData(180f, "Etc/GMT-12")]
    [InlineData(-180f, "Etc/GMT+12")]
    public static void EtcTimeZone(float longitude, string expected)
    {
        Assert.Equal(expected, Lookup.CalculateEtcTimeZoneId(longitude));
    }
}
