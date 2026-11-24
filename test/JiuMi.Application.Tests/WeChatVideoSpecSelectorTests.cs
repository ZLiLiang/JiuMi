using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatVideoSpecSelectorTests
{
    [Fact]
    public void BuildOptions_GivenEmptySpecs_ReturnsOnlyDefaultOption()
    {
        IReadOnlyList<WeChatVideoSpecOption> options = WeChatVideoSpecSelector.BuildOptions([], "默认");

        WeChatVideoSpecOption option = Assert.Single(options);
        Assert.True(option.IsDefault);
        Assert.Null(option.FileFormat);
        Assert.Equal("默认", option.Label);
    }

    [Fact]
    public void BuildOptions_GivenMultipleSpecs_PlacesDefaultFirstThenSortsByResolutionAndBitrateDesc()
    {
        WeChatVideoSpec[] specs =
        [
            new() { FileFormat = "xWT113", CodingFormat = "h264", Width = 576, Height = 1024, VideoBitRate = 900 },
            new() { FileFormat = "xWT101", CodingFormat = "h265", Width = 1080, Height = 1920, VideoBitRate = 2200 },
            new() { FileFormat = "xWT111", CodingFormat = "h264", Width = 720, Height = 1280, VideoBitRate = 1600 },
            new() { FileFormat = "xWT102", CodingFormat = "h264", Width = 1080, Height = 1920, VideoBitRate = 3000 },
        ];

        IReadOnlyList<WeChatVideoSpecOption> options = WeChatVideoSpecSelector.BuildOptions(specs, "Default");

        Assert.Equal(5, options.Count);
        Assert.Null(options[0].FileFormat);
        Assert.Equal("Default", options[0].Label);
        Assert.Equal("xWT102", options[1].FileFormat);
        Assert.Equal("1080p · H.264", options[1].Label);
        Assert.Equal("xWT101", options[2].FileFormat);
        Assert.Equal("1080p · H.265", options[2].Label);
        Assert.Equal("xWT111", options[3].FileFormat);
        Assert.Equal("720p · H.264", options[3].Label);
        Assert.Equal("xWT113", options[4].FileFormat);
        Assert.Equal("576p · H.264", options[4].Label);
    }

    [Fact]
    public void BuildOptions_GivenDuplicateResolutionAndCodec_KeepsHighestBitrateVariant()
    {
        WeChatVideoSpec[] specs =
        [
            new() { FileFormat = "xWT200", CodingFormat = "h265", Width = 1080, Height = 1920, VideoBitRate = 1800 },
            new() { FileFormat = "xWT101", CodingFormat = "HEVC", Width = 1080, Height = 1920, VideoBitRate = 2400 },
            new() { FileFormat = "xWT300", CodingFormat = "h265", Width = 1080, Height = 1920, BitRate = 1500 },
        ];

        IReadOnlyList<WeChatVideoSpecOption> options = WeChatVideoSpecSelector.BuildOptions(specs, "默认");

        Assert.Equal(2, options.Count);
        Assert.Equal("xWT101", options[1].FileFormat);
        Assert.Equal("1080p · H.265", options[1].Label);
        Assert.Equal(2400, options[1].EffectiveBitRate);
    }

    [Fact]
    public void BuildOptions_GivenEntriesMissingFileFormatOrMetadata_FiltersAndFormatsGracefully()
    {
        WeChatVideoSpec[] specs =
        [
            new() { FileFormat = null, Width = 1080, Height = 1920, CodingFormat = "h265" },
            new() { FileFormat = "   ", Width = 720, Height = 1280, CodingFormat = "h264" },
            new() { FileFormat = "xWT120", Width = 1920, Height = 1080, CodingFormat = null },
            new() { FileFormat = "xWT999", Width = null, Height = null, CodingFormat = "av1" },
        ];

        IReadOnlyList<WeChatVideoSpecOption> options = WeChatVideoSpecSelector.BuildOptions(specs, "默认");

        Assert.Equal(3, options.Count);
        Assert.Equal("xWT120", options[1].FileFormat);
        Assert.Equal("1080p", options[1].Label);
        Assert.Equal("xWT999", options[2].FileFormat);
        Assert.Equal("xWT999 · AV1", options[2].Label);
    }
}
