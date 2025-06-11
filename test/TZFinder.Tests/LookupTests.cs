using System.Collections.ObjectModel;

namespace TZFinder.Tests;

public static class LookupTests
{
    [Fact]
    public static void LoadDataFile()
    {
        var timeZoneTrees = new TimeZoneTree[5];
        Thread[] threads = Enumerable
            .Range(0, timeZoneTrees.Length)
            .Select(i => new Thread(() => StartLoadData(i)))
            .ToArray();

        foreach (Thread thread in threads) thread.Start();
        foreach (Thread thread in threads) thread.Join();

        Assert.All(timeZoneTrees, tree => Assert.True(ReferenceEquals(timeZoneTrees[0], tree)));

        void StartLoadData(int i)
        {
            timeZoneTrees[i] = TZLookup.TimeZoneTree;
        }
    }

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
        Assert.Equal(expected, TZLookup.CalculateEtcTimeZoneId(longitude));
    }

    [Theory]
    [InlineData(181f)]
    [InlineData(-181f)]
    public static void EtcTimeZone_Throws(float longitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TZLookup.CalculateEtcTimeZoneId(longitude));
    }
}
