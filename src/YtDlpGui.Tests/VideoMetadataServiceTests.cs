using System.Text.Json;
using YtDlpGui.Core;

namespace YtDlpGui.Tests;

public class VideoMetadataServiceTests
{
    [Fact]
    public void BuildVideoFormats_AlwaysStartsWithBestAvailable()
    {
        var formats = Parse("""[]""");
        var result = VideoMetadataService.BuildVideoFormats(formats);

        Assert.Single(result);
        Assert.Equal("Best available", result[0].Label);
        Assert.Equal("bv*+ba/b", result[0].Selector);
        Assert.Null(result[0].Height);
    }

    [Fact]
    public void BuildVideoFormats_DropsStoryboardsAndAudioOnlyEntries()
    {
        var formats = Parse("""
        [
            { "format_id": "sb0", "vcodec": "none", "acodec": "none", "height": null },
            { "format_id": "251", "vcodec": "none", "acodec": "opus", "height": null },
            { "format_id": "137", "vcodec": "avc1", "acodec": "none", "height": 1080, "fps": 30 }
        ]
        """);

        var result = VideoMetadataService.BuildVideoFormats(formats);

        Assert.Equal(2, result.Count); // Best available + 1080p
        Assert.Equal("1080p", result[1].Label);
        Assert.Equal("bv*[height<=1080]+ba/b[height<=1080]", result[1].Selector);
    }

    [Fact]
    public void BuildVideoFormats_UsesDistinctHeightsDescendingWithNoHardcodedList()
    {
        var formats = Parse("""
        [
            { "format_id": "160", "vcodec": "avc1", "acodec": "none", "height": 144, "fps": 30 },
            { "format_id": "134", "vcodec": "avc1", "acodec": "none", "height": 360, "fps": 30 },
            { "format_id": "137", "vcodec": "avc1", "acodec": "none", "height": 1080, "fps": 30 },
            { "format_id": "137dup", "vcodec": "avc1", "acodec": "none", "height": 1080, "fps": 30 }
        ]
        """);

        var result = VideoMetadataService.BuildVideoFormats(formats);
        var heights = result.Skip(1).Select(f => f.Height).ToList();

        Assert.Equal(new int?[] { 1080, 360, 144 }, heights);
    }

    [Fact]
    public void BuildVideoFormats_AppendsFpsLabelOnlyAboveThirty()
    {
        var formats = Parse("""
        [
            { "format_id": "a", "vcodec": "avc1", "acodec": "none", "height": 1080, "fps": 60 },
            { "format_id": "b", "vcodec": "avc1", "acodec": "none", "height": 720, "fps": 30 }
        ]
        """);

        var result = VideoMetadataService.BuildVideoFormats(formats);

        Assert.Equal("1080p60", result.First(f => f.Height == 1080).Label);
        Assert.Equal("720p", result.First(f => f.Height == 720).Label);
    }

    [Fact]
    public void BuildVideoFormats_ProgressiveOnlyFormatsStillProduceHeightBasedSelectors()
    {
        // A progressive-only extractor (no separate video-only stream): the bv*+ba/b shape
        // still works because yt-dlp falls back to the "/b" half when "bv*" matches nothing.
        var formats = Parse("""
        [
            { "format_id": "18", "vcodec": "avc1", "acodec": "mp4a", "height": 360, "fps": 30 },
            { "format_id": "22", "vcodec": "avc1", "acodec": "mp4a", "height": 720, "fps": 30 }
        ]
        """);

        var result = VideoMetadataService.BuildVideoFormats(formats);

        Assert.Contains(result, f => f.Height == 720 && f.Selector == "bv*[height<=720]+ba/b[height<=720]");
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
