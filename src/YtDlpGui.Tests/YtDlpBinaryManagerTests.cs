using YtDlpGui.Core;

namespace YtDlpGui.Tests;

public class YtDlpBinaryManagerTests
{
    [Theory]
    [InlineData("2024.12.06", "2024.12.05", true)]
    [InlineData("2024.12.06", "2024.12.06", false)]
    [InlineData("2024.01.01", "2024.12.06", false)]
    [InlineData("2025.01.01", null, true)]
    [InlineData(null, "2024.12.06", false)]
    public void IsNewer_ComparesZeroPaddedDateVersionsOrdinally(string? latest, string? current, bool expected)
    {
        Assert.Equal(expected, YtDlpBinaryManager.IsNewer(latest, current));
    }
}
