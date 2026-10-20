using FluentAssertions;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Services;

namespace MiCaminoBJJ.Tests.Services;

public class ExternalMediaParserTests
{
    private readonly ExternalMediaParser _sut;

    public ExternalMediaParserTests()
    {
        _sut = new ExternalMediaParser();
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("http://www.youtube.com/watch?v=abc123DEF_-", "abc123DEF_-")]
    public void ParseUrl_ValidYouTubeUrl_ReturnsValidResult(string url, string expectedVideoId)
    {
        // Act
        var result = _sut.ParseUrl(url);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Provider.Should().Be(ExternalMediaProvider.YouTube);
        result.VideoId.Should().Be(expectedVideoId);
        result.OriginalUrl.Should().Be(url);
        result.EmbedUrl.Should().Be($"https://www.youtube.com/embed/{expectedVideoId}");
    }

    [Theory]
    [InlineData("https://vimeo.com/123456789", "123456789")]
    [InlineData("http://vimeo.com/42", "42")]
    public void ParseUrl_ValidVimeoUrl_ReturnsValidResult(string url, string expectedVideoId)
    {
        // Act
        var result = _sut.ParseUrl(url);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Provider.Should().Be(ExternalMediaProvider.Vimeo);
        result.VideoId.Should().Be(expectedVideoId);
        result.OriginalUrl.Should().Be(url);
        result.EmbedUrl.Should().Be($"https://player.vimeo.com/video/{expectedVideoId}");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseUrl_EmptyUrl_ReturnsInvalidResult(string url)
    {
        // Act
        var result = _sut.ParseUrl(url);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ParseUrl_NullUrl_ReturnsInvalidResult()
    {
        // Act
        var result = _sut.ParseUrl(null!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("https://example.com/video")]
    [InlineData("not-a-url")]
    [InlineData("https://vimeo.com/")]
    [InlineData("https://youtube.com/watch")]
    public void ParseUrl_UnsupportedUrl_ReturnsInvalidResult(string url)
    {
        // Act
        var result = _sut.ParseUrl(url);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ParseUrl_TrimmedUrl_WorksCorrectly()
    {
        // Arrange
        var url = "  https://www.youtube.com/watch?v=dQw4w9WgXcQ  ";

        // Act
        var result = _sut.ParseUrl(url);

        // Assert
        result.IsValid.Should().BeTrue();
        result.VideoId.Should().Be("dQw4w9WgXcQ");
    }
}
