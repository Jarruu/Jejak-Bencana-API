using JejakBencana.Api.Validation;

namespace JejakBencana.Api.Tests;

public class SlugAndUrlRulesTests
{
    [Theory]
    [InlineData("kasus-sumatra")]
    [InlineData("kasus2026")]
    [InlineData("a")]
    public void Valid_Slugs_Are_Accepted(string slug)
    {
        Assert.True(SlugRules.IsValid(slug));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Kasus Sumatra")]
    [InlineData("kasus--ganda")]
    [InlineData("-awal")]
    [InlineData("akhir-")]
    [InlineData("kasus_underscore")]
    [InlineData("kasus/slash")]
    public void Invalid_Slugs_Are_Rejected(string slug)
    {
        Assert.False(SlugRules.IsValid(slug));
    }

    [Theory]
    [InlineData("https://www.bnpb.go.id/berita")]
    [InlineData("http://localhost:3000/x")]
    public void Http_Urls_Are_Accepted(string url)
    {
        Assert.True(UrlRules.IsValid(url));
    }

    [Theory]
    [InlineData("www.bnpb.go.id")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData(null)]
    public void Non_Http_Urls_Are_Rejected(string? url)
    {
        Assert.False(UrlRules.IsValid(url));
    }
}
